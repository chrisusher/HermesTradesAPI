using Services.Clients;
using Shared.Data;
using Shared.DTOs.Portfolios;
using Shared.DTOs.Reports;
using Shared.DTOs.Reports.Portfolio;
using Shared.Interfaces;

namespace Services.Reports;

public class PortfolioROIReporter : ROIReporter, IStrategyReporter
{
    private readonly StrategyService _strategyService;
    private readonly TransactionService _transactionService;
    private readonly ILogger<PortfolioROIReporter> _logger;

    public PortfolioROIReporter(
        PortfolioService portfolioService,
        StockClient stockClient,
        StrategyService strategyService,
        TransactionService transactionService,
        FxRateClient fxRateClient,
        ILoggerFactory loggerFactory
    ) : base(fxRateClient, portfolioService, stockClient, loggerFactory)
    {
        _strategyService = strategyService;
        _transactionService = transactionService;
        _logger = loggerFactory.CreateLogger<PortfolioROIReporter>();
    }

    public async Task<StrategyReport> CreateReportAsync(Dictionary<string, object> parameters, DateTime? now = null)
    {
        if (!parameters.TryGetValue("portfolioId", out object? value))
        {
            throw new ArgumentException("Parameter 'portfolioId' is required");
        }

        var portfolioId = Guid.Parse(value.ToString()!);

        if (!parameters.TryGetValue("userId", out value))
        {
            throw new ArgumentException("Parameter 'userId' is required");
        }

        var userId = Guid.Parse(value.ToString()!);

        var portfolio = await CalculatePortfolioRoiAsync(portfolioId, userId);

        return new PortfolioROIReport
        {
            ReportName = $"Portfolio ROI Report for {portfolioId} (user {userId})",
            // PortfolioName = portfolio.Name,
            // CurrentValue = portfolio.TotalCurrentValue ?? 0m,
            // ProfitLoss = portfolio.CurrentProfitLoss ?? 0m,
            // ProfitLossPercentage = portfolio.CurrentRoiPercent ?? 0m,
            // TotalInvested = portfolio.TotalInvested ?? 0m,
        };
    }

    /// <summary>
    /// Calculates ROI and P&L for a specific user portfolio
    /// Only considers stocks with open trades and uses yesterday's closing prices
    /// </summary>
    /// <param name="portfolio">The user portfolio to calculate metrics for</param>
    /// <returns>The portfolio with updated ROI and P&L metrics</returns>
    public async Task<Portfolio> CalculatePortfolioRoiAsync(Guid portfolioId, Guid userId)
    {
        try
        {
            _logger.LogDebug("Calculating ROI for portfolio {PortfolioId}", portfolioId);

            var portfolio = await PortfolioService.GetPortfolioAsync(userId, portfolioId);

            if (portfolio == null)
            {
                _logger.LogWarning("Portfolio {PortfolioId} not found", portfolioId);
                throw new DataNotFoundException($"Portfolio with ID {portfolioId} not found for user {userId}");
            }

            // if (portfolio.Strategies.Count == 0)
            // {
            //     _logger.LogDebug("No strategies in portfolio {PortfolioId}, setting metrics to zero", portfolio.PortfolioId);
            //     portfolio.TotalInvested = 0;
            //     portfolio.TotalCurrentValue = 0;
            //     return portfolio;
            // }

            // var totalInvested = 0.0m;
            // var totalCurrentValue = 0.0m;

            // // Calculate metrics for each strategy in the portfolio
            // foreach (var strategy in portfolio.Strategies)
            // {
            //     try
            //     {
            //         var strategyMetrics = await CalculateStrategyRoiAsync(portfolio.PortfolioId, strategy.StrategyId, userId, portfolio.Currency);

            //         totalInvested += strategyMetrics.TotalInvested;
            //         totalCurrentValue += strategyMetrics.TotalCurrentValue;
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger.LogWarning(ex, "Failed to calculate ROI for strategy {StrategyId} in portfolio {PortfolioId}",
            //             strategy.StrategyId, portfolio.PortfolioId);
            //     }
            // }

            // portfolio.TotalInvested = totalInvested;
            // portfolio.TotalCurrentValue = totalCurrentValue;

            // _logger.LogDebug("Portfolio {PortfolioId} metrics calculated: Invested={TotalInvested}, Current={TotalCurrentValue}, ROI={Roi}%",
            //     portfolio.PortfolioId, totalInvested, totalCurrentValue, portfolio.CurrentRoiPercent);

            return portfolio;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating ROI for portfolio {PortfolioId}", portfolioId);
            throw;
        }
    }

    public async Task<AllPortfolioROIsReport> CreateReportAsync(Dictionary<string, object> parameters)
    {
        var report = new AllPortfolioROIsReport
        {
            ReportName = "All Portfolio ROIs Report",
        };

        if (!parameters.TryGetValue("userId", out object? value))
        {
            throw new ArgumentException("Parameter 'userId' is required");
        }

        var userId = Guid.Parse(value.ToString()!);

        var portfolios = await PortfolioService.GetPortfoliosAsync(userId);

        foreach (var portfolio in portfolios)
        {
            try
            {
                var updatedPortfolio = await CalculatePortfolioRoiAsync(portfolio.PortfolioId, portfolio.UserId);

                // var portfolioReport = new PortfolioROIReport
                // {
                //     ReportName = $"Portfolio ROI Report for {updatedPortfolio.PortfolioId} (user {updatedPortfolio.UserId})",
                //     PortfolioName = updatedPortfolio.Name,
                //     CurrentValue = updatedPortfolio.TotalCurrentValue ?? 0m,
                //     ProfitLoss = updatedPortfolio.CurrentProfitLoss ?? 0m,
                //     ProfitLossPercentage = updatedPortfolio.CurrentRoiPercent ?? 0m,
                //     TotalInvested = updatedPortfolio.TotalInvested ?? 0m,
                // };

                // await UpdatePortfolioRoiAsync(portfolio.UserId, updatedPortfolio);

                report.UpdatedPortfolioCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate ROI for portfolio {PortfolioId}", portfolio.PortfolioId);
            }
        }

        return report;
    }

    /// <summary>
    /// Calculates ROI metrics for a specific strategy
    /// </summary>
    /// <param name="strategyId">The strategy identifier</param>
    /// <returns>Total invested and current value for the strategy</returns>
    private async Task<(decimal TotalInvested, decimal TotalCurrentValue)> CalculateStrategyRoiAsync(Guid portfolioId, string strategyId, Guid userId, CurrencyCode currency)
    {
        // Get all transactions for the strategy (from the beginning)
        var fromDate = new DateTime(2020, 1, 1); // Start from a reasonable past date
        var toDate = DateTime.UtcNow;

        var strategy = await _strategyService.GetStrategyAsync(strategyId);

        if (strategy == null)
        {
            _logger.LogWarning("Strategy {StrategyId} not found for ROI calculation", strategyId);
            return (0, 0);
        }

        var transactions = await _transactionService.GetHistoricalTradesAsync(userId, strategyId, fromDate, toDate);

        var buyTransactions = transactions.ConfirmedTrades
            .Where(t => StaticData.IsBuyOrder(t.Type))
            .ToList();

        if (buyTransactions.Count == 0)
        {
            return (0, 0);
        }

        decimal totalInvested = 0;
        decimal totalCurrentValue = 0;

        // Group by stock to calculate current holdings
        var stockGroups = buyTransactions.GroupBy(t => t.StockId);

        foreach (var stockGroup in stockGroups)
        {
            var stockId = stockGroup.Key;

            if (!stockGroup.Any())
            {
                _logger.LogWarning("No transactions found for stock ID {StockId} in strategy {StrategyId}", stockId, strategyId);
                continue;
            }

            var stockTransactions = stockGroup.ToList();
            var firstTransaction = stockTransactions.FirstOrDefault();

            if (firstTransaction == null)
            {
                _logger.LogWarning("No transactions found for stock ID {StockId} in strategy {StrategyId}", stockId, strategyId);
                continue;
            }

            var symbol = firstTransaction.Symbol;

            // Calculate total shares and cost for this stock
            var totalShares = stockTransactions.Sum(t => t.Quantity);

            // Only include if there are open positions
            if (totalShares > 0)
            {
                var holding = await CalculateHoldingAsync(stockId, stockTransactions, currency);

                if (holding is null)
                {
                    _logger.LogWarning("Holding calculation returned null for stock {StockSymbol} in strategy {StrategyId}", symbol, strategyId);
                    continue;
                }

                holding.PortfolioId = portfolioId;
                totalInvested += holding.TotalInvested;
                totalCurrentValue += holding.CurrentValue;

                // // If Strategy supports Stop Loss, update the StopLoss on the PortfolioHolding Table using PortfolioService
                // if (strategy.Config.StopLosses)
                // {
                //     _logger.LogDebug("Updating Stop Loss for stock {StockSymbol} in strategy {StrategyId}", symbol, strategyId);
                //     var stopLoss = await _strategyRunner.GetStockStopLossAsync(strategyId, stockId);

                //     if (stopLoss.HasValue)
                //     {
                //         if (firstTransaction?.StopLoss != stopLoss.Value)
                //         {
                //             firstTransaction!.StopLoss = stopLoss.Value;

                //             await _transactionService.UpdateConfirmedTradeAsync(firstTransaction!);
                //         }
                //     }
                // }
            }
        }
        return (totalInvested, totalCurrentValue);
    }

    /// <summary>
    /// Updates the portfolio ROI metrics in the database
    /// </summary>
    private async Task UpdatePortfolioRoiAsync(Guid userId, Portfolio portfolio)
    {
        // Update the database entity with the calculated ROI metrics
        // await PortfolioService.UpdatePortfolioAsync(portfolio.PortfolioId, userId, new UpdatePortfolioRequest
        // {
        //     AlwaysInvest = portfolio.AlwaysInvest,
        //     Name = portfolio.Name,
        //     Description = portfolio.Description,
        //     IsDefault = portfolio.IsDefault,
        //     StartingBalance = portfolio.StartingBalance,
        //     TotalInvested = portfolio.TotalInvested,
        //     TotalCurrentValue = portfolio.TotalCurrentValue,
        // });
    }
}
