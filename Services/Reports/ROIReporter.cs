using Shared.DTOs.Portfolios;
using Services.Clients;
using Shared.DTOs.Transactions;
using Shared.Data;

namespace Services.Reports;

public class ROIReporter
{
    private readonly PortfolioService _portfolioService;
    private readonly FxRateClient _fxRateClient;
    private readonly StockClient _stockClient;
    private readonly ILogger<ROIReporter> _logger;

    public ROIReporter(
        FxRateClient fxRateClient,
        PortfolioService portfolioService,
        StockClient stockClient,
        ILoggerFactory loggerFactory)
    {
        _portfolioService = portfolioService;
        _fxRateClient = fxRateClient;
        _stockClient = stockClient;
        _logger = loggerFactory.CreateLogger<ROIReporter>();
    }

    protected PortfolioService PortfolioService => _portfolioService;

    // TODO Refactor this class to reduce duplication between overloads.

    /// <summary>
    /// Calculates a holding summary from a set of transactions. Correctly handles partial / full sales by
    /// deriving remaining shares and invested capital based on BUY orders only. SELL transactions contribute
    /// to realised cash (already reflected in portfolio.FreeCash) and therefore are excluded from the remaining
    /// invested capital. Fully liquidated positions (remaining shares <= 0) return null to exclude from holdings.
    /// Uses an average cost basis (weighted by BUY quantity) for simplicity – FIFO could be introduced later if needed.
    /// </summary>
    /// <param name="stockHolding">The portfolio holding metadata.</param>
    /// <param name="transactions">All transactions (buys & sells) for the stock.</param>
    /// <param name="currency">Target currency for the report (e.g., backtest currency).</param>
    /// <returns>A tuple of (holding, stock) or (null, stock) if fully sold / missing data.</returns>
    protected async Task<PortfolioHoldingResponse?> CalculateHoldingAsync(Guid stockId, IEnumerable<TransactionResponse> transactions, CurrencyCode userCurrency)
    {
        var stock = await _stockClient.GetStockAsync(stockId);

        if (stock is null)
        {
            _logger.LogWarning("Stock with ID {StockId} not found when calculating holdings", stockId);
            return null;
        }

        var holding = PortfolioHolding.FromPortfolioStock(stock);

        return await CalculateHoldingAsync(holding, transactions, userCurrency);
    }

    /// <summary>
    /// Calculates a holding summary from a set of transactions. Correctly handles partial / full sales by
    /// deriving remaining shares and invested capital based on BUY orders only. SELL transactions contribute
    /// to realised cash (already reflected in portfolio.FreeCash) and therefore are excluded from the remaining
    /// invested capital. Fully liquidated positions (remaining shares <= 0) return null to exclude from holdings.
    /// Uses an average cost basis (weighted by BUY quantity) for simplicity – FIFO could be introduced later if needed.
    /// </summary>
    /// <param name="stockHolding">The portfolio holding metadata.</param>
    /// <param name="transactions">All transactions (buys & sells) for the stock.</param>
    /// <param name="currency">Target currency for the report (e.g., backtest currency).</param>
    /// <returns>A tuple of (holding, stock) or (null, stock) if fully sold / missing data.</returns>
    protected async Task<PortfolioHoldingResponse?> CalculateHoldingAsync(PortfolioHolding stockHolding, IEnumerable<TransactionResponse> transactions, CurrencyCode userCurrency)
    {
        var stock = await _stockClient.GetStockAsync(stockHolding.StockId);

        if (stock is null)
        {
            _logger.LogWarning("Stock with ID {StockId} not found when calculating holdings", stockHolding.StockId);
            return null;
        }

        if (!stock.PreviousClosePrice.HasValue)
        {
            _logger.LogWarning("Stock {Symbol} does not have a previous close price set", stock.Symbol);
            return null;
        }

        try
        {
            var buyTransactions = transactions
                .Where(t => StaticData.IsBuyOrder(t.Type))
                .ToList();
            var sellTransactions = transactions
                .Except(buyTransactions)
                .ToList();

            // Sell transactions have negative quantity, so sum gives negative total
            var soldQuantity = sellTransactions.Sum(x => x.Quantity);
            var boughtQuantity = buyTransactions.Sum(x => x.Quantity);

            // Since soldQuantity is negative, adding it to boughtQuantity gives net position
            if (boughtQuantity + soldQuantity <= 0)
            {
                // Fully sold position
                return await CalculateClosedHoldingAsync(stockHolding, transactions, userCurrency);
            }

            decimal remainingShares = 0m;
            decimal investedRemaining = 0m;
            decimal averageBuyPrice = 0m;

            if (soldQuantity == 0)
            {
                remainingShares = boughtQuantity;
                investedRemaining = buyTransactions.Sum(t => t.TotalCost);
                averageBuyPrice = boughtQuantity > 0 ? investedRemaining / boughtQuantity : 0m;
            }
            else
            {
                // Sell transactions have negative quantity, so we add (which subtracts the absolute value)
                var totalSoldShares = sellTransactions.Sum(t => t.Quantity);
                remainingShares = boughtQuantity + totalSoldShares;

                var totalBuyCost = buyTransactions.Sum(t => t.TotalCost);
                averageBuyPrice = boughtQuantity > 0 ? totalBuyCost / boughtQuantity : 0m;
                investedRemaining = averageBuyPrice * remainingShares;
            }

            // Calculate realized P/L for closed positions
            var currentValue = remainingShares * stock.PreviousClosePrice.Value;

            var holding = new PortfolioHoldingResponse
            {
                StockId = stockHolding.StockId,
                Symbol = stockHolding.Symbol,
                CompanyName = string.IsNullOrWhiteSpace(stock.CompanyName) ? stockHolding.CompanyName : stock.CompanyName,
                ExchangeName = stockHolding.ExchangeName,
                CurrencyCode = stockHolding.CurrencyCode,
                FirstPurchaseDate = stockHolding.FirstPurchaseDate,
                StockStatus = stockHolding.StockStatus,
                Transactions = stockHolding.Transactions,
                Created = stockHolding.Created,
                Updated = stockHolding.Updated,
                Status = stockHolding.Status,
                ProfitLoss = 0 // Will be set below
            };

            holding.AveragePurchasePrice = Math.Round(averageBuyPrice, 2);
            holding.AverageSalePrice = soldQuantity < 0 ? Math.Round(sellTransactions.Average(s => s.TotalCost / s.Quantity), 2) : 0m;
            holding.CurrentValue = currentValue;
            holding.ProfitLoss = currentValue - investedRemaining;
            holding.SaleAmount = soldQuantity < 0 ? Math.Round(-sellTransactions.Sum(s => s.TotalCost), 2) : null;
            holding.TotalInvested = buyTransactions.Sum(t => t.TotalCost);
            holding.TotalShares = remainingShares;

            // FX conversion if stock currency differs from report currency
            // Current Value
            decimal? converted = await _fxRateClient.ConvertAsync(currentValue, stock.CurrencyCode, userCurrency, stock.Updated ?? DateTime.UtcNow);

            if (converted.HasValue)
            {
                holding.CurrentValue = converted.Value;
            }

            // Profit/Loss
            converted = await _fxRateClient.ConvertAsync(holding.ProfitLoss ?? 0m, stock.CurrencyCode, userCurrency, stock.Updated ?? DateTime.UtcNow);

            if (converted.HasValue)
            {
                holding.ProfitLoss = converted.Value;
            }

            // Sale Amount
            if (holding.SaleAmount.HasValue)
            {
                converted = await _fxRateClient.ConvertAsync(holding.SaleAmount.Value, stock.CurrencyCode, userCurrency, stock.Updated ?? DateTime.UtcNow);

                if (converted.HasValue)
                {
                    holding.SaleAmount = converted.Value;
                }
            }

            // Total Invested
            converted = await _fxRateClient.ConvertAsync(holding.TotalInvested, stock.CurrencyCode, userCurrency, stock.Updated ?? DateTime.UtcNow);

            if (converted.HasValue)
            {
                holding.TotalInvested = converted.Value;
            }

            return holding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating holding for stock ID {StockId}", stockHolding.StockId);
            throw;
        }
    }

    /// <summary>
    /// Calculates a holding summary from a set of transactions. Correctly handles partial / full sales by
    /// deriving remaining shares and invested capital based on BUY orders only. SELL transactions contribute
    /// to realised cash (already reflected in portfolio.FreeCash) and therefore are excluded from the remaining
    /// invested capital. Fully liquidated positions (remaining shares <= 0) return null to exclude from holdings.
    /// Uses an average cost basis (weighted by BUY quantity) for simplicity – FIFO could be introduced later if needed.
    /// </summary>
    /// <param name="transactions">All transactions (buys & sells) for the stock.</param>
    /// <param name="currency">Target currency for the report (e.g., backtest currency).</param>
    /// <returns>A tuple of (holding, stock) or (null, stock) if fully sold / missing data.</returns>
    // protected async Task<PortfolioHoldingResponse?> CalculateHoldingAsync(Guid stockId, IEnumerable<ConfirmedTrade> transactions, CurrencyCode targetCurrency)
    // {
    //     PortfolioStock? stock = await _stockService.GetStockAsync(stockId);

    //     if (stock is null)
    //     {
    //         _logger.LogWarning("Stock with ID {StockId} not found when calculating holdings", stockId);
    //         return null;
    //     }

    //     if (!stock.PreviousClosePrice.HasValue)
    //     {
    //         _logger.LogWarning("Stock {Symbol} does not have a previous close price set", stock.Symbol);
    //         return null;
    //     }

    //     try
    //     {
    //         var buyTransactions = transactions
    //             .Where(t => t.BuyTransaction)
    //             .ToList();
    //         var sellTransactions = transactions
    //             .Except(buyTransactions)
    //             .ToList();

    //         // Sell transactions have negative quantity, so sum gives negative total
    //         var soldQuantity = sellTransactions.Sum(x => x.Quantity);
    //         var boughtQuantity = buyTransactions.Sum(x => x.Quantity);

    //         // Since soldQuantity is negative, adding it to boughtQuantity gives net position
    //         if (boughtQuantity + soldQuantity <= 0)
    //         {
    //             // Fully sold position
    //             return await CalculateClosedHoldingAsync(stock, transactions, targetCurrency);
    //         }

    //         decimal remainingShares = 0m;
    //         decimal investedRemaining = 0m;
    //         decimal costToUser = buyTransactions.Sum(t => t.TotalCostToUser);
    //         decimal averageBuyPrice = 0m;
    //         decimal saleAmount = 0m;

    //         if (soldQuantity == 0)
    //         {
    //             remainingShares = boughtQuantity;
    //             investedRemaining = buyTransactions.Sum(t => t.TotalCost);
    //             averageBuyPrice = boughtQuantity > 0 ? investedRemaining / boughtQuantity : 0m;
    //         }
    //         else
    //         {
    //             // Sell transactions have negative quantity, so we add (which subtracts the absolute value)
    //             var totalSoldShares = sellTransactions.Sum(t => t.Quantity);
    //             remainingShares = boughtQuantity + totalSoldShares;

    //             var totalBuyCost = buyTransactions.Sum(t => t.TotalCost);
    //             averageBuyPrice = boughtQuantity > 0 ? totalBuyCost / boughtQuantity : 0m;
    //             investedRemaining = averageBuyPrice * remainingShares;

    //             saleAmount = -sellTransactions.Sum(t => t.TotalCostToUser);

    //             costToUser -= saleAmount;
    //         }

    //         // Calculate realized P/L for closed positions
    //         var currentValue = remainingShares * stock.PreviousClosePrice.Value;

    //         var currentValueConverted = await _fxConverter.ConvertAsync(currentValue, targetCurrency, stock.Updated ?? DateTime.UtcNow, stock.CurrencyCode);

    //         if (!currentValueConverted.HasValue)
    //         {
    //             _logger.LogWarning("FX conversion failed for stock {Symbol} when calculating holdings", stock.Symbol);
    //             return null;
    //         }

    //         var holding = new PortfolioHoldingResponse
    //         {
    //             StockId = stockId,
    //             Symbol = stock.Symbol,
    //             CompanyName = stock.CompanyName,
    //             Sector = stock.Sector,
    //             ExchangeName = stock.ExchangeName,
    //             CurrencyCode = stock.CurrencyCode,
    //             StockStatus = PortfolioStockStatus.Active,
    //             Transactions = transactions.Select(t => t.TransactionId).ToList(),
    //             ProfitLoss = 0 // Will be set below
    //         };

    //         holding.AveragePurchasePrice = Math.Round(averageBuyPrice, 2);
    //         holding.AverageSalePrice = soldQuantity < 0 ? Math.Round(sellTransactions.Average(s => s.TotalCost / s.Quantity), 2) : 0m;
    //         holding.CurrentValue = currentValueConverted.Value;
    //         holding.ProfitLoss = currentValueConverted.Value - costToUser;
    //         holding.SaleAmount = Math.Round(saleAmount, 2);
    //         holding.TotalInvested = costToUser;
    //         holding.TotalShares = remainingShares;

    //         return holding;
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Error calculating holding for stock ID {StockId}", stockId);
    //         throw;
    //     }
    // }

    // Legacy method retained for backward compatibility with other reporters (if any). Delegates to new implementation
    // assuming aggregated values represent net remaining shares & invested capital. NOTE: This will not correctly handle
    // partial sales if called directly; callers should migrate to the transaction-aware overload.
    protected async Task<PortfolioHolding?> CalculateHoldingAsync(PortfolioHolding stockHolding, decimal totalShares, decimal totalInvested, CurrencyCode userCurrency)
    {
        // Construct a synthetic BUY transaction representing remaining position so downstream logic is consistent.
        var synthetic = new TransactionResponse
        {
            StockId = stockHolding.StockId,
            Quantity = totalShares,
            TotalCost = totalInvested,
            Type = OrderType.MARKET_BUY,
        };
        return await CalculateHoldingAsync(stockHolding, new[] { synthetic }, userCurrency);

    }

    private async Task<PortfolioHoldingResponse?> CalculateClosedHoldingAsync(PortfolioHolding stock, IEnumerable<TransactionResponse> transactions, CurrencyCode userCurrency)
    {
        var buyTransactions = transactions
            .Where(t => StaticData.IsBuyOrder(t.Type))
            .ToList();
        var sellTransactions = transactions
            .Except(buyTransactions)
            .ToList();

        // For closed positions, calculate realized P/L
        var totalBuyCost = buyTransactions.Sum(t => t.TotalCost);
        // Sell transactions have negative total cost, so negate to get positive proceeds
        var totalSaleProceeds = -sellTransactions.Sum(t => t.TotalCost);
        var realisedProfitLoss = totalSaleProceeds - totalBuyCost;

        var holding = new PortfolioHoldingResponse
        {
            StockId = stock.StockId,
            Symbol = stock.Symbol,
            CompanyName = stock.CompanyName,
            Sector = stock.Sector,
            ExchangeName = stock.ExchangeName,
            CurrencyCode = stock.CurrencyCode,
            FirstPurchaseDate = stock.FirstPurchaseDate,
            StockStatus = stock.StockStatus,
            Transactions = stock.Transactions,
            Created = stock.Created,
            Updated = stock.Updated,
            Status = stock.Status,
            ProfitLoss = 0 // Will be set below
        };

        holding.AveragePurchasePrice = Math.Round(buyTransactions.Average(b => b.TotalCost / b.Quantity), 2);
        // Sell transactions have negative quantity and cost, so division gives positive price
        holding.AverageSalePrice = Math.Round(sellTransactions.Average(s => s.TotalCost / s.Quantity), 2);
        holding.ProfitLoss = realisedProfitLoss;
        holding.SaleAmount = Math.Round(totalSaleProceeds, 2);

        // Set Total Shares to the total bought quantity for reporting purposes
        holding.TotalShares = buyTransactions.Sum(t => t.Quantity);
        holding.TotalInvested = totalBuyCost;

        decimal? converted = null;

        if (sellTransactions.Any(x => x.Currency != userCurrency) ||
           buyTransactions.Any(x => x.Currency != userCurrency))
        {
            converted = await _fxRateClient.ConvertAsync(realisedProfitLoss, stock.CurrencyCode, userCurrency, sellTransactions.Max(t => t.TransactionDate!.Value));

            if (converted.HasValue)
            {
                holding.ProfitLoss = converted.Value;
            }

            if (holding.SaleAmount.HasValue)
            {
                converted = await _fxRateClient.ConvertAsync(holding.SaleAmount.Value,  stock.CurrencyCode, userCurrency, sellTransactions.Max(t => t.TransactionDate!.Value));

                if (converted.HasValue)
                {
                    holding.SaleAmount = converted.Value;
                }
            }

            converted = await _fxRateClient.ConvertAsync(holding.TotalInvested, stock.CurrencyCode, userCurrency, sellTransactions.Max(t => t.TransactionDate!.Value));

            if (converted.HasValue)
            {
                holding.TotalInvested = converted.Value;
            }
        }

        return holding;
    }

    // private async Task<PortfolioHoldingResponse?> CalculateClosedHoldingAsync(PortfolioStock stock, IEnumerable<ConfirmedTrade> transactions, CurrencyCode userCurrency)
    // {
    //     var buyTransactions = transactions
    //         .Where(t => t.BuyTransaction)
    //         .ToList();
    //     var sellTransactions = transactions
    //         .Except(buyTransactions)
    //         .ToList();

    //     // For closed positions, calculate realized P/L
    //     var totalBuyCost = buyTransactions.Sum(t => t.TotalCostToUser);
    //     // Sell transactions have negative total cost, so negate to get positive proceeds
    //     var totalSaleProceeds = -sellTransactions.Sum(t => t.TotalCostToUser);
    //     var realisedProfitLoss = totalSaleProceeds - totalBuyCost;

    //     var holding = new PortfolioHoldingResponse
    //     {
    //         StockId = stock.StockId,
    //         Symbol = stock.Symbol,
    //         CompanyName = stock.CompanyName,
    //         Sector = stock.Sector,
    //         ExchangeName = stock.ExchangeName,
    //         CurrencyCode = stock.CurrencyCode,
    //         StockStatus = PortfolioStockStatus.FullySold,
    //         Transactions = transactions.Select(t => t.TransactionId).ToList(),
    //         Created = stock.Created,
    //         Updated = stock.Updated,
    //         Status = stock.Status,
    //         ProfitLoss = 0 // Will be set below
    //     };

    //     holding.AveragePurchasePrice = Math.Round(buyTransactions.Average(b => b.TotalCost / b.Quantity), 2);

    //     // Sell transactions have negative quantity and cost, so division gives positive price
    //     holding.AverageSalePrice = Math.Round(sellTransactions.Average(s => s.TotalCost / s.Quantity), 2);
    //     holding.ProfitLoss = realisedProfitLoss;
    //     holding.SaleAmount = Math.Round(totalSaleProceeds, 2);
    //     holding.TotalShares = buyTransactions.Sum(t => t.Quantity) + sellTransactions.Sum(t => t.Quantity);
    //     holding.TotalInvested = totalBuyCost;

    //     return holding;
    // }
}
