using Services.Repositories;
using Shared.DTOs.Portfolios;

namespace Services.Strategies.NasdaqBounce;

public sealed class NasdaqBounceService(
    INasdaqBounceRepository executions,
    PortfolioService portfolios,
    StrategyRepository strategies,
    UserService users,
    TransactionService transactions) : INasdaqBounceStore
{
    public async Task<List<NasdaqBouncePortfolio>> GetEntryPortfoliosAsync(CancellationToken cancellationToken)
    {
        var strategy = await strategies.GetStrategyAsync(NasdaqBounceRules.StrategyId, cancellationToken, trackChanges: false);

        if (strategy?.Status != StatusType.Active)
        {
            return [];
        }

        var activePortfolios = await portfolios.GetStrategyPortfoliosAsync(
            NasdaqBounceRules.StrategyId,
            StatusType.Active,
            cancellationToken);
        var result = new List<NasdaqBouncePortfolio>();

        foreach (var portfolio in activePortfolios)
        {
            result.Add(await ComposeAsync(portfolio, cancellationToken));
        }

        return result;
    }

    public async Task<NasdaqBouncePortfolio> GetPortfolioAsync(Guid userId, Guid portfolioId, CancellationToken cancellationToken)
    {
        var portfolio = await portfolios.GetStrategyPortfolioAsync(userId, portfolioId, cancellationToken);

        return await ComposeAsync(portfolio, cancellationToken);
    }

    private async Task<NasdaqBouncePortfolio> ComposeAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        var user = await users.GetUserByIdAsync(portfolio.UserId, cancellationToken)
            ?? throw new DataNotFoundException($"User {portfolio.UserId} not found.");

        return new NasdaqBouncePortfolio(
            portfolio.PortfolioId,
            portfolio.UserId,
            portfolio.Name,
            portfolio.FreeCash ?? 0,
            portfolio.Currency ?? CurrencyCode.Unknown,
            user.CurrencyCode);
    }

    public Task<List<NasdaqBounceRun>> GetPendingRunsAsync(CancellationToken cancellationToken) =>
        executions.GetPendingRunsAsync(cancellationToken);

    public Task<List<NasdaqBounceRun>> GetPortfolioRunsAsync(Guid portfolioId, CancellationToken cancellationToken) =>
        executions.GetPortfolioRunsAsync(portfolioId, cancellationToken);

    public Task SaveRunAsync(NasdaqBounceRun run, CancellationToken cancellationToken) =>
        executions.SaveRunAsync(run, cancellationToken);

    public Task<NasdaqBounceReview?> GetReviewAsync(DateTimeOffset slot, CancellationToken cancellationToken) =>
        executions.GetReviewAsync(slot, cancellationToken);

    public Task SaveReviewAsync(NasdaqBounceReview review, CancellationToken cancellationToken) =>
        executions.SaveReviewAsync(review, cancellationToken);

    public Task<NasdaqBounceEntrySignal?> GetEntrySignalAsync(DateOnly date, CancellationToken cancellationToken) =>
        executions.GetEntrySignalAsync(date, cancellationToken);

    public Task SaveEntrySignalAsync(NasdaqBounceEntrySignal signal, CancellationToken cancellationToken) =>
        executions.SaveEntrySignalAsync(signal, cancellationToken);

    public Task ApplyTradeAsync(StrategyTrade trade, CancellationToken cancellationToken) =>
        transactions.ApplyStrategyTradeAsync(trade, cancellationToken);

    public async Task<decimal> GetRemainingQuantityAsync(NasdaqBounceRun run, CancellationToken cancellationToken)
    {
        var purchaseQuantity = await transactions.GetRemainingStrategyPurchaseQuantityAsync(
            run.Entry.TransactionId,
            run.Portfolio.Id,
            cancellationToken);
        var holding = await portfolios.GetStrategyHoldingAsync(run.Entry, cancellationToken);

        return Math.Max(0, Math.Min(purchaseQuantity, holding?.Quantity ?? 0));
    }

    public Task<bool> HasTradeStartedAsync(StrategyTrade trade, CancellationToken cancellationToken) =>
        portfolios.HasStrategyTradeStartedAsync(trade, cancellationToken);
}
