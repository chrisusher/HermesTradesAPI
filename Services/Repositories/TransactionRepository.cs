using Microsoft.EntityFrameworkCore;
using Services.Database;
using Shared.Data;
using Shared.DTOs.Transactions;

namespace Services.Repositories;

public class TransactionRepository
{
    private readonly DatabaseContext _dbContext;
    private readonly PortfolioRepository _portfolioRepository;

    public TransactionRepository(
        DatabaseContext dbContext,
        PortfolioRepository portfolioRepository)
    {
        _dbContext = dbContext;
        _portfolioRepository = portfolioRepository;
    }

    public async Task<TransactionsTable> AddTransactionAsync(TransactionObject transaction, Guid portfolioId, Guid userId, string? strategyId, Guid? strategyVersionId = null)
    {
        await _portfolioRepository.GetPortfolioAsync(userId, portfolioId);

        var transactionTable = TransactionsTable.FromTransaction(transaction, portfolioId, strategyId, strategyVersionId);

        _dbContext.Transactions.Add(transactionTable);
        await _dbContext.SaveChangesAsync();

        var isBuy = StaticData.IsBuyOrder(transaction.Type);

        // if (portfolio != null)
        // {
        //     portfolio = await _dbContext.Portfolio.FirstOrDefaultAsync(x => x.Id == portfolio!.Id);
        // }
        if (!isBuy)
        {
            await ProcessSaleOrderAsync(transaction, transactionTable);
        }
        else
        {
            // await ProcessBuyOrderAsync(transaction, portfolio, transactionTable);
        }

        return transactionTable;
    }

    public async Task<List<TransactionsTable>> AddTransactionsBatchAsync(IEnumerable<TransactionObject> transactions, Guid portfolioId, Guid userId, string? strategyId = null, Guid? strategyVersionId = null)
    {
        var transactionList = transactions.ToList();

        if (transactionList.Count == 0)
        {
            return new List<TransactionsTable>();
        }

        var portfolio = await _portfolioRepository.GetPortfolioAsync(userId, portfolioId);

        var transactionTables = new List<TransactionsTable>(transactionList.Count);

        foreach (var transaction in transactionList)
        {
            var table = TransactionsTable.FromTransaction(transaction, portfolioId, strategyId, strategyVersionId);
            transactionTables.Add(table);
            _dbContext.Transactions.Add(table);
        }

        await _dbContext.SaveChangesAsync();

        return transactionTables;
    }

    /// <summary>
    /// Deletes all transactions for a specific strategy (used for backtesting reset).
    /// </summary>
    /// <param name="strategyId">The strategy ID</param>
    /// <returns>Number of transactions deleted</returns>
    public async Task<int> DeleteTransactionsByStrategyAsync(string strategyId)
    {
        var transactions = await _dbContext.Transactions
            .Where(t => t.StrategyId == strategyId)
            .ToListAsync();

        if (transactions.Count != 0)
        {
            _dbContext.Transactions.RemoveRange(transactions);
            await _dbContext.SaveChangesAsync();
        }

        return transactions.Count;
    }

    public async Task<TransactionsTable?> FindOriginalTransactionAsync(Guid stockId, decimal quantityToSell)
    {
        var transactions = await GetOpenBuyTransactionsForStockAsync(stockId);

        // quantityToSell is negative for sell transactions, so take absolute value
        var absoluteQuantityToSell = Math.Abs(quantityToSell);

        foreach (var transactionTable in transactions)
        {
            var quantityRemaining = transactionTable.QuantityRemaining ?? transactionTable.Quantity;

            if (quantityRemaining <= 0)
            {
                continue;
            }

            if (absoluteQuantityToSell <= quantityRemaining)
            {
                return transactionTable;
            }
        }

        return null;
    }

    public async Task<IEnumerable<TransactionsTable>> GetAllTransactionsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        if (!fromDate.HasValue)
        {
            fromDate = DateTime.UtcNow.AddYears(-1);
        }

        var query = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.TransactionDate >= fromDate);

        if (toDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= toDate.Value);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Guid>> GetClosedStocksAsync()
    {
        var closedStockIds = new List<Guid>();
        var transactions = await GetAllTransactionsAsync();

        var uniqueStockIds = transactions
            .Select(t => t.StockId)
            .Distinct()
            .ToList();

        foreach (var stockId in uniqueStockIds)
        {
            var openBuyTransactions = await GetOpenBuyTransactionsForStockAsync(stockId);

            if (openBuyTransactions.Count == 0)
            {
                closedStockIds.Add(stockId);
            }
        }

        return closedStockIds.ToList();
    }

    public async Task<List<Guid>> GetClosedStocksAsync(int skip, int take)
    {
        var allClosedStocks = await GetClosedStocksAsync();

        return allClosedStocks
            .Skip(skip)
            .Take(take)
            .ToList();
    }

    public async Task<List<TransactionsTable>> GetOpenBuyTransactionsForStockAsync(Guid stockId)
    {
        var openBuyTransactions = new List<TransactionsTable>();

        var openTransactions = await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.StockId == stockId
                && (t.QuantityRemaining ?? t.Quantity) > 0)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        foreach (var transaction in openTransactions)
        {
            if (StaticData.IsBuyOrder(transaction.Type))
            {
                openBuyTransactions.Add(transaction);
            }
        }

        return openBuyTransactions;
    }

    public async Task<TransactionsTable?> GetTransactionByIdAsync(Guid transactionId)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transactionId);
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByPortfolioIdAsync(Guid portfolioId, DateTime fromDate, DateTime toDate)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.PortfolioId == portfolioId
                && t.TransactionDate >= fromDate
                && t.TransactionDate <= toDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByStockIdAsync(Guid stockId)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.StockId == stockId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByStockIdAsync(Guid stockId, DateTime fromDate)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.StockId == stockId
                && t.TransactionDate >= fromDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByStrategyAsync(string strategyId)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.StrategyId == strategyId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByStrategyAsync(string strategyId, DateTime fromDate, DateTime toDate)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.StrategyId == strategyId && t.TransactionDate >= fromDate && t.TransactionDate <= toDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TransactionsTable>> GetTransactionsByIdsAsync(List<Guid> transactionIds, OrderGroupType orderGroup)
    {
        var query = await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => transactionIds.Contains(t.Id))
            .ToListAsync();

        var filteredQuery = query.AsEnumerable();

        if (orderGroup == OrderGroupType.Buys)
        {
            filteredQuery = filteredQuery.Where(t => StaticData.IsBuyOrder(t.Type));
        }
        else if (orderGroup == OrderGroupType.Sales)
        {
            filteredQuery = filteredQuery.Where(t => !StaticData.IsBuyOrder(t.Type));
        }

        return filteredQuery
            .OrderByDescending(t => t.TransactionDate)
            .ToList();
    }

    public async Task<TransactionsTable?> TransactionExistsAsync(Guid stockId, decimal price, decimal quantity, decimal totalCost, OrderType orderType)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.StockId == stockId &&
                t.Price == price &&
                t.Quantity == quantity &&
                t.TotalCost == totalCost &&
                t.Type == orderType);
    }

    public async Task UpdateTransactionAsync(Guid portfolioId, string? strategyId, TransactionObject transaction, Guid? strategyVersionId = null)
    {
        _dbContext.Transactions.Update(TransactionsTable.FromTransaction(transaction, portfolioId, strategyId, strategyVersionId));
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateTransactionAsync(TransactionsTable existingTransaction)
    {
        var trackedEntity = await _dbContext.Transactions
            .FirstOrDefaultAsync(x => x.Id == existingTransaction.Id && x.PartitionKey == existingTransaction.PartitionKey);

        if (trackedEntity != null)
        {
            // Update only the changed properties
            _dbContext.Entry(trackedEntity).CurrentValues.SetValues(existingTransaction);
        }
        else
        {
            _dbContext.Transactions.Update(existingTransaction);
        }

        await _dbContext.SaveChangesAsync();
    }

    // private async Task ProcessBuyOrderAsync(TransactionObject transaction, PortfolioTable? portfolio, TransactionsTable transactionTable)
    // {
    //     if (portfolio is null)
    //     {
    //         return;
    //     }

    //     var existingStock = portfolio.Stocks.FirstOrDefault(x => x.StockId == transaction.StockId);

    //     if (existingStock is null)
    //     {
    //         portfolio.Stocks.Add(new()
    //         {
    //             StockId = transaction.StockId,
    //             Quantity = transaction.Quantity,
    //             // Ensure new holdings default to Active status
    //             StockStatus = PortfolioStockStatus.Active,
    //             Transactions = new()
    //             {
    //                 transactionTable.Id.ToString()
    //             }
    //         });
    //     }
    //     else
    //     {
    //         existingStock.Quantity += transaction.Quantity;
    //         existingStock.Transactions.Add(transactionTable.Id.ToString());

    //         existingStock.StockStatus = PortfolioStockStatus.Active;

    //         portfolio.Stocks.Remove(existingStock);
    //         portfolio.Stocks.Add(existingStock);
    //     }

    //     portfolio.FreeCash -= transaction.TotalCost;

    //     _dbContext.Portfolio.Update(portfolio);

    //     await _dbContext.SaveChangesAsync();
    // }

    private async Task ProcessSaleOrderAsync(TransactionObject transaction, TransactionsTable transactionTable)
    {
        // if (portfolio != null)
        // {
        //     var existingStock = portfolio.Stocks.FirstOrDefault(x => x.StockId == transaction.StockId);

        //     if (existingStock is null)
        //     {
        //         return;
        //     }

        //     // Always record the sale transaction on the holding
        //     existingStock.Transactions.Add(transactionTable.Id.ToString());
        //     // Sale transactions have negative quantity, so we add to reduce the holding
        //     existingStock.Quantity += transaction.Quantity;

        //     if (existingStock.Quantity <= 0)
        //     {
        //         // Keep the holding for reporting; mark FullySold and clamp quantity to 0
        //         existingStock.Quantity = 0;
        //         existingStock.StockStatus = PortfolioStockStatus.FullySold;
        //     } 

        //     portfolio.Stocks.Remove(existingStock);
        //     portfolio.Stocks.Add(existingStock);

        //     // Sale transactions have negative total cost, so we subtract to increase cash
        //     // e.g., FreeCash - (-750) = FreeCash + 750
        //     portfolio.FreeCash -= transaction.TotalCost;

        //     _dbContext.Portfolio.Update(portfolio);

        //     await _dbContext.SaveChangesAsync();
        // }

        // Find the original transaction, update the transaction and also set the original transaction as sold.

        var originalTransaction = await FindOriginalTransactionAsync(transaction.StockId, transaction.Quantity);

        if (originalTransaction is null)
        {
            return;
        }

        transactionTable.OriginalTransactionId = originalTransaction.Id;
        await UpdateTransactionAsync(transactionTable);

        // Sale transaction.Quantity is negative, so adding it reduces QuantityRemaining
        // e.g., QuantityRemaining + (-5) = QuantityRemaining - 5 (reducing available shares)
        originalTransaction.QuantityRemaining += transaction.Quantity;
        await UpdateTransactionAsync(originalTransaction);
    }
}
