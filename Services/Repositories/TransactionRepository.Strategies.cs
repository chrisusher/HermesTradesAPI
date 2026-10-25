using Microsoft.EntityFrameworkCore;
using Services.Database;
using System.Security.Cryptography;
using System.Text;

namespace Services.Repositories;

public partial class TransactionRepository
{
    public async Task<StrategyPurchaseState> GetStrategyPurchaseAsync(StrategyTrade trade, CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var purchase = await FindPurchaseAsync(database, trade, cancellationToken);

        return MapPurchase(purchase, trade.TransactionId.ToString());
    }

    public async Task<decimal> GetRemainingPurchaseQuantityAsync(
        Guid transactionId,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var purchase = await database.Transactions.AsNoTracking().SingleAsync(
            transaction => transaction.Id == transactionId && transaction.PortfolioId == portfolioId,
            cancellationToken);

        return purchase.QuantityRemaining ?? purchase.Quantity;
    }

    public async Task RecordStrategyTransactionAsync(StrategyTrade trade, CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Transactions.SingleOrDefaultAsync(
            transaction => transaction.Id == trade.TransactionId && transaction.StockId == trade.StockId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.PortfolioId != trade.PortfolioId || existing.Price != trade.Price || existing.Quantity != trade.Quantity)
            {
                throw new InvalidOperationException("Strategy transaction identity conflict.");
            }

            return;
        }

        database.Transactions.Add(new TransactionsTable
        {
            Id = trade.TransactionId,
            PartitionKey = trade.StockId.ToString(),
            PortfolioId = trade.PortfolioId,
            StockId = trade.StockId,
            StrategyId = trade.StrategyId,
            StrategyVersionId = CreateId($"{trade.StrategyId}:{trade.StrategyVersion}"),
            OriginalTransactionId = trade.OriginalTransactionId,
            Currency = trade.StockCurrency,
            Price = trade.Price,
            Quantity = trade.Quantity,
            QuantityRemaining = trade.Quantity > 0 ? trade.Quantity : 0,
            Type = trade.Quantity > 0 ? OrderType.MARKET_BUY : OrderType.MARKET_SELL,
            TotalCost = trade.Price * trade.Quantity,
            TotalCostToUser = trade.UserAmount,
            PortfolioCashAmount = trade.PortfolioAmount,
            SourceCandleTime = trade.SourceCandleTime,
            ExecutionKey = trade.TransactionId.ToString(),
            Created = trade.Time.UtcDateTime,
            TransactionDate = trade.Time.UtcDateTime
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStrategyPurchaseAsync(
        StrategyTrade trade,
        Func<StrategyPurchaseState, decimal> calculateRemainingQuantity,
        CancellationToken cancellationToken)
    {
        await using var database = await _strategyContextFactory.CreateDbContextAsync(cancellationToken);
        var purchase = await FindPurchaseAsync(database, trade, cancellationToken);
        var transactionKey = trade.TransactionId.ToString();

        if (purchase.AppliedStrategySales.Contains(transactionKey))
        {
            return;
        }

        purchase.QuantityRemaining = calculateRemainingQuantity(MapPurchase(purchase, transactionKey));
        purchase.AppliedStrategySales.Add(transactionKey);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Task<TransactionsTable> FindPurchaseAsync(
        DatabaseContext database,
        StrategyTrade trade,
        CancellationToken cancellationToken) =>
        database.Transactions.SingleAsync(
            purchase => purchase.Id == trade.OriginalTransactionId &&
                purchase.PortfolioId == trade.PortfolioId &&
                purchase.StockId == trade.StockId &&
                purchase.StrategyId == trade.StrategyId,
            cancellationToken);

    private static StrategyPurchaseState MapPurchase(TransactionsTable purchase, string transactionKey) =>
        new(purchase.QuantityRemaining ?? purchase.Quantity, purchase.AppliedStrategySales.Contains(transactionKey));

    private static Guid CreateId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
}
