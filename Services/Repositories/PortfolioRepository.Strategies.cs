using Microsoft.EntityFrameworkCore;
using Services.Database;
using Shared.DTOs.Portfolios;
using System.Security.Cryptography;
using System.Text;

namespace Services.Repositories;

public partial class PortfolioRepository
{
    public async Task<StrategyHoldingState?> GetStrategyHoldingStateAsync(StrategyTrade trade, CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var holding = await FindHoldingAsync(database, trade, cancellationToken);

        return holding is null ? null : MapHolding(holding, trade.TransactionId.ToString());
    }

    public async Task<bool> HasStrategyCashAppliedAsync(StrategyTrade trade, CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var portfolio = await FindPortfolioAsync(database, trade, cancellationToken);

        return portfolio.AppliedStrategyTransactions.Contains(trade.TransactionId.ToString());
    }

    // Use a fresh context per document so retries cannot flush state left by a failed earlier step.
    // The service calculation runs against the loaded document; EF retains its ETag for the save.
    public async Task UpdateStrategyCashAsync(
        StrategyTrade trade,
        Func<StrategyCashState, decimal> calculateBalance,
        CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var portfolio = await FindPortfolioAsync(database, trade, cancellationToken);
        var transactionKey = trade.TransactionId.ToString();

        if (portfolio.AppliedStrategyTransactions.Contains(transactionKey))
        {
            return;
        }

        portfolio.FreeCash = calculateBalance(new StrategyCashState(portfolio.Currency, portfolio.FreeCash));
        portfolio.AppliedStrategyTransactions.Add(transactionKey);
        portfolio.Updated = trade.Time.UtcDateTime;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStrategyHoldingAsync(
        StrategyTrade trade,
        Func<StrategyHoldingState?, StrategyHoldingUpdate> calculateHolding,
        CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var holding = await FindHoldingAsync(database, trade, cancellationToken);
        var transactionKey = trade.TransactionId.ToString();

        if (holding?.Transactions.Contains(transactionKey) == true)
        {
            return;
        }

        var update = calculateHolding(holding is null ? null : MapHolding(holding, transactionKey));

        if (holding is null)
        {
            holding = new PortfolioHoldingTable
            {
                Id = CreateId($"holding:{trade.PortfolioId}:{trade.StockId}:{trade.StrategyId}"),
                PartitionKey = trade.PortfolioId.ToString(),
                PortfolioId = trade.PortfolioId,
                StockId = trade.StockId,
                Symbol = trade.Symbol,
                ExchangeName = trade.Exchange,
                CurrencyCode = CurrencyCode.USD,
                StrategyId = trade.StrategyId,
                Source = HoldingSource.Strategy,
                FirstPurchaseDate = trade.Time.UtcDateTime
            };
            database.PortfolioHoldings.Add(holding);
        }

        holding.Quantity = update.Quantity;
        holding.StockStatus = update.Status;
        holding.Transactions.Add(transactionKey);
        holding.Updated = trade.Time.UtcDateTime;
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Task<PortfolioTable> FindPortfolioAsync(
        DatabaseContext database,
        StrategyTrade trade,
        CancellationToken cancellationToken) =>
        database.Portfolio.SingleAsync(
            portfolio => portfolio.Id == trade.PortfolioId && portfolio.PartitionKey == trade.UserId.ToString(),
            cancellationToken);

    private static Task<PortfolioHoldingTable?> FindHoldingAsync(
        DatabaseContext database,
        StrategyTrade trade,
        CancellationToken cancellationToken) =>
        database.PortfolioHoldings.SingleOrDefaultAsync(
            holding => holding.PartitionKey == trade.PortfolioId.ToString() &&
                holding.StockId == trade.StockId &&
                holding.StrategyId == trade.StrategyId &&
                holding.Source == HoldingSource.Strategy,
            cancellationToken);

    private static StrategyHoldingState MapHolding(PortfolioHoldingTable holding, string transactionKey) =>
        new(holding.Quantity, holding.Transactions.Contains(transactionKey));

    private static Guid CreateId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));

    public async Task<List<Portfolio>> GetStrategyPortfoliosAsync(
        string strategyId,
        StatusType status,
        CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var portfolios = await database.Portfolio.AsNoTracking()
            .Where(portfolio => portfolio.StrategyId == strategyId && portfolio.Status == status)
            .ToListAsync(cancellationToken);

        return portfolios.Select(portfolio => portfolio.ToPortfolioDto()).ToList();
    }
}
