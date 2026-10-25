using System.Security.Cryptography;
using System.Text;

namespace Services.Strategies.NasdaqBounce;

public static class NasdaqBounceRules
{
    public const string StrategyId = "nasdaq-bounce-overnight";
    public const string Version = "1.0.0";
    public static TimeZoneInfo NewYork { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public static DateOnly LocalDate(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, NewYork).DateTime);

    public static DateTimeOffset At(DateOnly date, TimeSpan time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.FromTimeSpan(time)), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, NewYork));
    }

    // The decline thresholds map directly to the maximum share of available portfolio cash.
    public static decimal Allocation(decimal decline) => decline >= 2m ? .50m : decline >= 1.5m ? .40m : decline >= 1m ? .25m : 0m;

    public static decimal Quantity(decimal cash, decimal allocation, decimal usdPrice, decimal usdToPortfolioRate)
    {
        if (cash <= 0 || allocation <= 0 || usdPrice <= 0 || usdToPortfolioRate <= 0)
        {
            return 0;
        }
        // Round down to whole micro-shares so the cash budget cannot be exceeded.

        return decimal.Floor(cash * allocation / (usdPrice * usdToPortfolioRate) * 1_000_000m) / 1_000_000m;
    }

    public static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    public static DateTimeOffset? ReviewSlot(NasdaqBounceSession session, DateTimeOffset now)
    {
        var first = session.Open.AddMinutes(30);

        if (now < first || now >= session.Deadline)
        {
            return null;
        }
        // Round down to a stable half-hour slot so overlapping timer invocations share one review.
        var elapsedSlots = Math.Floor((now - first).TotalMinutes / 30);

        return first.AddMinutes(elapsedSlots * 30);
    }
}
