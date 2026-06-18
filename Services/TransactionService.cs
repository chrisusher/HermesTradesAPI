using Services.Clients;
using Services.Database;
using Services.Repositories;
using Shared.Data;
using Shared.DTOs.Portfolios;
using Shared.DTOs.Stocks;
using Shared.DTOs.Transactions;

namespace Services;

public class TransactionService
{
    private readonly PortfolioRepository _portfolioRepository;
    private readonly StockClient _stockClient;
    private readonly TransactionRepository _transactionRepository;
    private readonly FxRateClient _fxRateClient;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        PortfolioRepository portfolioRepository, // Has to use Repository to avoid circular dependencies
        StockClient stockClient,
        TransactionRepository transactionRepository,
        FxRateClient fxRateClient,
        ILoggerFactory loggerFactory)
    {
        _portfolioRepository = portfolioRepository;
        _stockClient = stockClient;
        _transactionRepository = transactionRepository;
        _fxRateClient = fxRateClient;
        _logger = loggerFactory.CreateLogger<TransactionService>();
    }

    public async Task<TransactionResponse?> BuyStockAsync(Guid userId, Guid portfolioId, PortfolioHolding stock, TransactionObject transaction, string strategyId = "")
    {
        if (!StaticData.IsBuyOrder(transaction.Type))
        {
            return null;
        }

        // Validate available FreeCash before committing the buy.
        // Fetch the latest composed portfolio (includes up-to-date FreeCash and holdings).
        var portfolio = await _portfolioRepository.GetPortfolioAsync(userId, portfolioId);

        var availableCash = portfolio.FreeCash ?? 0m;

        if (transaction.TotalCost <= 0)
        {
            _logger.LogWarning("Buy rejected for {Symbol}: Non-positive TotalCost {TotalCost}.", stock.Symbol, transaction.TotalCost);
            return null;
        }

        var existingTransaction = await _transactionRepository.TransactionExistsAsync(
            stock.StockId,
            transaction.Price,
            transaction.Quantity,
            transaction.TotalCost,
            transaction.Type);

        TransactionResponse response;

        // Check for duplicate transaction
        if (existingTransaction != null)
        {
            _logger.LogDebug("Duplicate transaction detected for stock {Symbol} with price {Price}, quantity {Quantity}, total cost {TotalCost}.",
                stock.Symbol, transaction.Price, transaction.Quantity, transaction.TotalCost);

            response = existingTransaction.ToTransactionResponse(transaction);

            // Even if Transaction already exists make sure the portfolio has the transaction.
            await _portfolioRepository.AddStockToPortfolioAsync(userId, portfolioId, stock, response, strategyId);
            return response;
        }

        var transactionResponse = await _transactionRepository.AddTransactionAsync(new()
        {
            Created = transaction.Created,
            Currency = transaction.Currency,
            Price = transaction.Price,
            Quantity = transaction.Quantity,
            QuantityRemaining = transaction.Quantity,
            StockId = stock.StockId,
            Symbol = stock.Symbol,
            TotalCost = transaction.TotalCost,
            TransactionDate = transaction.TransactionDate ?? transaction.Created,
            Type = transaction.Type,
        }, portfolioId, userId, strategyId);

        response = transactionResponse.ToTransactionResponse(transaction);

        await _portfolioRepository.AddStockToPortfolioAsync(userId, portfolioId, stock, response, strategyId);

        return response;
    }

    public async Task<List<TransactionResponse>> BuyStocksBatchAsync(Guid userId, Guid portfolioId, List<(PortfolioHolding Holding, TransactionObject Transaction)> buys, string strategyId = "")
    {
        var responses = new List<TransactionResponse>();

        if (buys.Count == 0)
        {
            return responses;
        }

        var portfolio = await _portfolioRepository.GetPortfolioAsync(userId, portfolioId);
        var availableCash = portfolio.FreeCash ?? 0m;
        var alwaysInvest = portfolio.AlwaysInvest;

        var filteredBuys = new List<(PortfolioHolding Holding, TransactionObject Transaction)>();

        foreach (var buy in buys)
        {
            if (!StaticData.IsBuyOrder(buy.Transaction.Type))
            {
                continue;
            }

            if (buy.Transaction.TotalCost <= 0)
            {
                _logger.LogWarning("Buy rejected for {Symbol}: Non-positive TotalCost {TotalCost}.", buy.Holding.Symbol, buy.Transaction.TotalCost);
                continue;
            }

            filteredBuys.Add(buy);
        }

        if (filteredBuys.Count == 0)
        {
            return responses;
        }

        var transactionTables = await _transactionRepository.AddTransactionsBatchAsync(
            filteredBuys.Select(b => b.Transaction),
            portfolioId,
            userId);

        var portfolioEntries = new List<(PortfolioHolding Holding, TransactionResponse Response)>();

        for (var index = 0; index < transactionTables.Count; index++)
        {
            var table = transactionTables[index];
            var buy = filteredBuys[index];

            var response = table.ToTransactionResponse(buy.Transaction);
            response.Symbol = buy.Holding.Symbol;

            responses.Add(response);
            portfolioEntries.Add((buy.Holding, response));
        }

        await _portfolioRepository.AddStocksToPortfolioAsync(userId, portfolioId,  strategyId, portfolioEntries);

        return responses;
    }

    // public async Task<TransactionResponse> BuyStockAsync(BuyTransactionRequestBody requestBody, string? strategyId = null)
    // {
    //     var stock = await _stockClient.GetStockAsync(requestBody.Symbol);

    //     if (stock is null)
    //     {
    //         _logger.LogError("Stock with symbol {Symbol} not found.", requestBody.Symbol);
    //         throw new DataNotFoundException($"Stock with symbol {requestBody.Symbol} not found.");
    //     }

    //     var transaction = new TransactionObject
    //     {
    //         Created = DateTime.UtcNow,
    //         Price = requestBody.Price,
    //         Quantity = requestBody.Quantity,
    //         QuantityRemaining = requestBody.Quantity,
    //         StockId = stock.StockId,
    //         Symbol = stock.Symbol,
    //         TotalCost = requestBody.TotalCost,
    //         TransactionDate = requestBody.Date ?? DateTime.UtcNow,
    //         Type = requestBody.Type,
    //     };

    //     var existingTransaction = await _transactionRepository.TransactionExistsAsync(
    //         stock.StockId,
    //         requestBody.Price,
    //         requestBody.Quantity,
    //         requestBody.TotalCost,
    //         transaction.Type);

    //     TransactionResponse transactionResponse;

    //     // Check for duplicate transaction
    //     if (existingTransaction != null)
    //     {
    //         _logger.LogDebug("Duplicate transaction detected for stock {Symbol} with price {Price}, quantity {Quantity}, total cost {TotalCost}.",
    //             requestBody.Symbol, requestBody.Price, requestBody.Quantity, requestBody.TotalCost);
    //         transactionResponse = existingTransaction.ToTransactionResponse(transaction);

    //         transactionResponse.Symbol = stock.Symbol;
    //         return transactionResponse;
    //     }

    //     var response = await _transactionRepository.AddTransactionAsync(transaction, strategyId);

    //     transactionResponse = response.ToTransactionResponse(transaction);
    //     transactionResponse.Symbol = stock.Symbol;

    //     return transactionResponse;
    // }

    public async Task<int> DeleteTransactionsByStrategyAsync(string strategyId)
    {
        return await _transactionRepository.DeleteTransactionsByStrategyAsync(strategyId);
    }

    public async Task<List<PortfolioStock>> GetClosedStocksAsync(int skip = 0, int take = 100)
    {
        var stockIds = await _transactionRepository.GetClosedStocksAsync(skip, take);

        if (stockIds.Count == 0)
        {
            return new List<PortfolioStock>();
        }

        return await _stockClient.GetStocksAsync(stockIds);
    }

    public async Task<TransactionResponse?> GetTransactionByIdAsync(Guid transactionId)
    {
        var transaction = await _transactionRepository.GetTransactionByIdAsync(transactionId);
        return transaction?.ToTransactionResponse();
    }

    public async Task<List<TransactionResponse>> GetTransactionsByStockAsync(Guid stockId, DateTime? fromDate = null)
    {
        if (!fromDate.HasValue)
        {
            fromDate = DateTime.UtcNow.AddYears(-1); // Default to one year ago if no date is provided
        }

        var transactions = await _transactionRepository.GetTransactionsByStockIdAsync(stockId, fromDate.Value);

        return transactions
            .Select(t => t.ToTransactionResponse())
            .ToList();
    }

    public async Task<List<TransactionResponse>> GetTransactionsByStrategyAsync(string strategyId)
    {
        var transactions = await _transactionRepository.GetTransactionsByStrategyAsync(strategyId);

        return transactions
            .Select(t => t.ToTransactionResponse())
            .ToList();
    }

    public async Task<List<TransactionResponse>> GetTransactionsByStrategyAsync(string strategyId, DateTime fromDate, DateTime toDate)
    {
        var transactions = await _transactionRepository.GetTransactionsByStrategyAsync(strategyId, fromDate, toDate);

        return transactions
            .Select(t => t.ToTransactionResponse())
            .ToList();
    }

    public async Task<List<TransactionResponse>> GetTransactionsByStockAsync(Guid stockId)
    {
        var transactions = await _transactionRepository.GetTransactionsByStockIdAsync(stockId);

        return transactions
            .Select(t => t.ToTransactionResponse())
            .ToList();
    }

    public async Task<List<TransactionResponse>> GetTransactionsByIdsAsync(List<Guid> transactionIds, OrderGroupType orderGroup = OrderGroupType.All)
    {
        var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, orderGroup);
        return transactions.Select(t => t.ToTransactionResponse()).ToList();
    }

    public async Task<List<TransactionResponse>> GetAllTransactionsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var transactions = await _transactionRepository.GetAllTransactionsAsync(fromDate, toDate);

        return transactions
            .Select(t => t.ToTransactionResponse())
            .ToList();
    }

    public async Task<TransactionResponse> SellStockAsync(Guid userId, Guid portfolioId, SellTransactionRequestBody requestBody)
    {
        var stock = await _stockClient.GetStockAsync(requestBody.Symbol);

        if (stock is null)
        {
            _logger.LogError("Stock with symbol {Symbol} not found.", requestBody.Symbol);
            throw new DataNotFoundException($"Stock with symbol {requestBody.Symbol} not found.");
        }

        var quantity = -Math.Abs(requestBody.Quantity); // Ensure quantity is negative for sell
        var totalCost = -Math.Abs(requestBody.TotalCost); // Ensure total cost is negative for sell

        Guid? originalTransactionId = requestBody.OriginalTransactionId;
        var transaction = new TransactionObject
        {
            Currency = stock.CurrencyCode,
            Created = DateTime.UtcNow,
            OriginalTransactionId = requestBody.OriginalTransactionId,
            Price = requestBody.Price,
            Quantity = quantity,
            StockId = stock.StockId,
            Symbol = stock.Symbol,
            TotalCost = totalCost,
            TransactionDate = requestBody.Date ?? DateTime.UtcNow,
            Type = requestBody.Type
        };

        TransactionsTable? originalTransaction;

        if (originalTransactionId.HasValue)
        {
            originalTransaction = await _transactionRepository.GetTransactionByIdAsync(originalTransactionId.Value);
        }
        else
        {
            originalTransaction = await _transactionRepository.FindOriginalTransactionAsync(stock.StockId, requestBody.Quantity);
        }

        var existingTransaction = await _transactionRepository.TransactionExistsAsync(
            stock.StockId,
            requestBody.Price,
            quantity,
            totalCost,
            transaction.Type);

        TransactionResponse transactionResponse;

        // Check for duplicate transaction
        if (existingTransaction != null)
        {
            _logger.LogDebug("Duplicate transaction detected for stock {Symbol} with price {Price}, quantity {Quantity}, total cost {TotalCost}.",
                requestBody.Symbol, requestBody.Price, quantity, totalCost);

            transactionResponse = existingTransaction.ToTransactionResponse(transaction);

            transactionResponse.Symbol = stock.Symbol;
            return transactionResponse;
        }

        string? strategyId = null;

        if (originalTransaction != null)
        {
            transaction.OriginalTransactionId = originalTransaction.Id;
            strategyId = originalTransaction.StrategyId;
        }

        var response = await _transactionRepository.AddTransactionAsync(transaction, portfolioId, userId, strategyId);

        transactionResponse = response.ToTransactionResponse(transaction);
        transactionResponse.Symbol = stock.Symbol;

        if (originalTransaction != null)
        {
            var originalTransactionResponse = originalTransaction.ToTransactionResponse(transaction);
            transactionResponse.ProfitLoss = CalculateProfitLoss(originalTransactionResponse, transactionResponse);
        }

        return transactionResponse;
    }

    public async Task<TransactionResponse?> SellStockAsync(Guid userId, Guid portfolioId, PortfolioHolding stock, TransactionObject transaction, string strategyId = "")
    {
        if (StaticData.IsBuyOrder(transaction.Type))
        {
            return null;
        }

        var existingTransaction = await _transactionRepository.TransactionExistsAsync(
            stock.StockId,
            transaction.Price,
            transaction.Quantity,
            transaction.TotalCost,
            transaction.Type);

        TransactionResponse transactionResponse;

        // Check for duplicate transaction
        if (existingTransaction != null)
        {
            _logger.LogDebug("Duplicate transaction detected for stock {Symbol} with price {Price}, quantity {Quantity}, total cost {TotalCost}.",
                transaction.Symbol, transaction.Price, transaction.Quantity, transaction.TotalCost);

            transactionResponse = existingTransaction.ToTransactionResponse(transaction);
            transactionResponse.Symbol = stock.Symbol;

            await _portfolioRepository.UpdatePortfolioHoldingAsync(userId, portfolioId, stock, transactionResponse);

            return transactionResponse;
        }

        var quantity = -Math.Abs(transaction.Quantity); // Ensure quantity is negative for sell
        var totalCost = -Math.Abs(transaction.TotalCost); // Ensure total cost is negative for sell
        var totalCostToUser = -Math.Abs(transaction.TotalCostToUser); // Ensure total cost to user is negative for sell

        var response = await _transactionRepository.AddTransactionAsync(new()
        {
            Currency = transaction.Currency,
            Created = DateTime.UtcNow,
            OriginalTransactionId = transaction.OriginalTransactionId,
            Price = transaction.Price,
            Quantity = quantity,
            StockId = stock.StockId,
            Symbol = stock.Symbol,
            TotalCost = totalCost,
            TotalCostToUser = totalCostToUser,
            TransactionDate = transaction.TransactionDate ?? DateTime.UtcNow,
            Type = transaction.Type,
        }, portfolioId, userId, strategyId);

        transactionResponse = response.ToTransactionResponse(transaction);
        transactionResponse.Symbol = stock.Symbol;

        await _portfolioRepository.UpdatePortfolioHoldingAsync(userId, portfolioId, stock, transactionResponse);

        return transactionResponse;
    }

    public async Task<TransactionResponse> UpdateAsync(Guid transactionId, UpdateTransactionRequestBody requestBody)
    {
        var existingTransaction = await _transactionRepository.GetTransactionByIdAsync(transactionId);

        if (existingTransaction is null)
        {
            _logger.LogError("Transaction with ID {TransactionId} not found.", transactionId);
            throw new DataNotFoundException($"Transaction with ID {transactionId} not found.");
        }

        if (!StaticData.IsBuyOrder(existingTransaction.Type))
        {
            existingTransaction.OriginalTransactionId = requestBody.OriginalTransactionId;
        }

        existingTransaction.Price = requestBody.Price;
        existingTransaction.Quantity = requestBody.Quantity;
        existingTransaction.StrategyId = requestBody.StrategyId;
        existingTransaction.TotalCost = requestBody.TotalCost;

        await _transactionRepository.UpdateTransactionAsync(existingTransaction);

        return existingTransaction.ToTransactionResponse(requestBody);
    }

    private static decimal? CalculateProfitLoss(TransactionResponse originalTransactionResponse, TransactionResponse transactionResponse)
    {
        if (originalTransactionResponse.Quantity == 0 || transactionResponse.Quantity == 0)
        {
            return null; // Avoid division by zero
        }

        var originalPricePerShare = originalTransactionResponse.TotalCost / originalTransactionResponse.Quantity;

        var currentPricePerShare = transactionResponse.TotalCost / transactionResponse.Quantity;
        var quantity = Math.Abs(transactionResponse.Quantity);
        var profitLoss = (currentPricePerShare - originalPricePerShare) * quantity;

        return Math.Round(profitLoss, 2); // Round to 2 decimal places for currency
    }

    /// <summary>
    /// Converts a transaction amount to USD based on the stock's currency and transaction date
    /// </summary>
    /// <param name="amount">The amount in the stock's currency</param>
    /// <param name="stock">The stock containing currency information</param>
    /// <param name="transactionDate">The date of the transaction for FX rate lookup</param>
    /// <returns>The amount converted to USD, or the original amount if USD or conversion not available</returns>
    private async Task<decimal> ConvertToUsdAsync(decimal amount, PortfolioStock stock, DateTime transactionDate)
    {
        // If already USD or currency unknown, return original amount
        if (stock.CurrencyCode == CurrencyCode.USD || stock.CurrencyCode == CurrencyCode.Unknown)
        {
            return amount;
        }

        try
        {
            var currencyCode = stock.CurrencyCode;

            // For GBX (pence), first convert to GBP
            if (stock.CurrencyCode == CurrencyCode.GBX)
            {
                amount = amount / 100; // Convert pence to pounds
                currencyCode = CurrencyCode.GBP;
            }

            // Get conversion rate from the stock currency to USD.
            var conversionRate = await _fxRateClient.GetConversionRateAsync(currencyCode, CurrencyCode.USD);

            if (conversionRate.HasValue)
            {
                // Since we store USD to target currency rates, we need to divide to get USD
                var usdAmount = amount / conversionRate.Value;

                _logger.LogDebug(
                    "Converted {Amount} {Currency} to {UsdAmount} USD using rate {Rate} for date {Date}",
                    amount, currencyCode, usdAmount, conversionRate.Value, transactionDate.Date);

                return usdAmount;
            }
            else
            {
                _logger.LogWarning(
                    "No FX conversion rate found for {Currency} on {Date}, using original amount",
                    currencyCode, transactionDate.Date);

                return amount;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error converting {Amount} {Currency} to USD for date {Date}, using original amount",
                amount, stock.CurrencyCode, transactionDate);

            return amount;
        }
    }
}
