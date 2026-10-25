using Shared.DTOs.Strategies.NasdaqBounce;

namespace Shared.Interfaces.Strategies.NasdaqBounce;

public interface INasdaqBounceSupportAnalyser
{
    Task<SupportDecision> AnalyseAsync(
        NasdaqBounceMarketSnapshot snapshot,
        NasdaqBounceSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
