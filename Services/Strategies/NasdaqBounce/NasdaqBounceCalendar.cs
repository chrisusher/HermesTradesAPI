using Shared.Calendar;

namespace Services.Strategies.NasdaqBounce;

public sealed class NasdaqBounceCalendar(INasdaqBounceMarketData marketData)
{
    // Scoped to one timer invocation, so failures never become a cached empty calendar.
    private readonly Dictionary<int, List<CloseDate>> _closures = [];

    public async Task<NasdaqBounceSession?> GetSessionAsync(DateOnly date, CancellationToken cancellationToken)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return null;
        }

        if (!_closures.TryGetValue(date.Year, out var closures))
        {
            closures = await marketData.GetClosuresAsync(date.Year, cancellationToken);
            _closures.Add(date.Year, closures);
        }
        var closure = closures.FirstOrDefault(x => DateOnly.FromDateTime(x.Date) == date);

        if (closure is not null && closure.Date.TimeOfDay == TimeSpan.Zero)
        {
            return null;
        }
        // API closure times are exchange-local wall times, not UTC timestamps.
        var close = closure?.Date.TimeOfDay ?? TimeSpan.FromHours(16);

        if (close <= TimeSpan.FromHours(10))
        {
            throw new InvalidOperationException("Invalid US session close.");
        }

        return new NasdaqBounceSession(date, NasdaqBounceRules.At(date, new TimeSpan(9, 30, 0)), NasdaqBounceRules.At(date, close));
    }

    public async Task<NasdaqBounceSession> AdjacentAsync(DateOnly date, int direction, CancellationToken cancellationToken)
    {
        for (var index = 0; index < 30; index++)
        {
            date = date.AddDays(direction);
            var session = await GetSessionAsync(date, cancellationToken);

            if (session is not null)
            {
                return session;
            }
        }
        throw new InvalidOperationException("No adjacent US trading session found.");
    }
}
