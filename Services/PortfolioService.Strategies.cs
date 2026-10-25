using Shared.DTOs.Portfolios;

namespace Services;

public partial class PortfolioService
{
    public Task<List<Portfolio>> GetStrategyPortfoliosAsync(
        string strategyId,
        StatusType status,
        CancellationToken cancellationToken) =>
        _portfolioRepository.GetStrategyPortfoliosAsync(strategyId, status, cancellationToken);

    public async Task<Portfolio> GetStrategyPortfolioAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        // Strategy writes use their own contexts, so snapshots must bypass previously tracked portfolio state.
        var portfolio = await _portfolioRepository.GetPortfolioAsync(userId, portfolioId, cancellationToken, trackChanges: false);

        return portfolio.ToPortfolioDto();
    }

    public Task<StrategyHoldingState?> GetStrategyHoldingAsync(StrategyTrade trade, CancellationToken cancellationToken) =>
        _portfolioRepository.GetStrategyHoldingStateAsync(trade, cancellationToken);

    public Task<bool> HasStrategyTradeStartedAsync(StrategyTrade trade, CancellationToken cancellationToken) =>
        // Cash is always the first committed document in the strategy trade workflow.
        _portfolioRepository.HasStrategyCashAppliedAsync(trade, cancellationToken);
}
