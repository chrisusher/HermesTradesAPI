using Shared.DTOs.Strategies.NasdaqBounce;

namespace Shared.Interfaces.Strategies.NasdaqBounce;

public interface INasdaqBounceStore
{
    Task<List<NasdaqBouncePortfolio>> GetEntryPortfoliosAsync(CancellationToken cancellationToken);
    Task<List<NasdaqBounceRun>> GetPendingRunsAsync(CancellationToken cancellationToken);
    Task<List<NasdaqBounceRun>> GetPortfolioRunsAsync(Guid portfolioId, CancellationToken cancellationToken);
    Task<NasdaqBouncePortfolio> GetPortfolioAsync(Guid userId, Guid portfolioId, CancellationToken cancellationToken);
    Task SaveRunAsync(NasdaqBounceRun run, CancellationToken cancellationToken);
    Task<NasdaqBounceReview?> GetReviewAsync(DateTimeOffset slot, CancellationToken cancellationToken);
    Task SaveReviewAsync(NasdaqBounceReview review, CancellationToken cancellationToken);
    Task<NasdaqBounceEntrySignal?> GetEntrySignalAsync(DateOnly date, CancellationToken cancellationToken);
    Task SaveEntrySignalAsync(NasdaqBounceEntrySignal signal, CancellationToken cancellationToken);
    Task ApplyTradeAsync(StrategyTrade trade, CancellationToken cancellationToken);
    Task<decimal> GetRemainingQuantityAsync(NasdaqBounceRun run, CancellationToken cancellationToken);
    Task<bool> HasTradeStartedAsync(StrategyTrade trade, CancellationToken cancellationToken);
}
