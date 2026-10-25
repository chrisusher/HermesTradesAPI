using Shared.Calendar;
using Shared.DTOs.Strategies.NasdaqBounce;

namespace Shared.Interfaces.Strategies.NasdaqBounce;

public interface INasdaqBounceMarketData
{
    Task<List<CloseDate>> GetClosuresAsync(int year, CancellationToken cancellationToken);
    Task<bool> IsOpenAsync(CancellationToken cancellationToken);
    Task<NasdaqBounceMarketSnapshot> GetSnapshotAsync(
        NasdaqBounceSession session,
        DateOnly previousSession,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<decimal> GetUsdRateAsync(CurrencyCode currency, CancellationToken cancellationToken);
}
