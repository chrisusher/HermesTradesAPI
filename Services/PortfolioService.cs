using Services.Clients;
using Services.Repositories;
using Shared.Data;
using Shared.DTOs.Portfolios;
using Shared.DTOs.Transactions;

namespace Services;

public class PortfolioService
{
    private readonly PortfolioRepository _portfolioRepository;
    private readonly TransactionService _transactionService;
    private readonly CandleClient _candleClient;
    private readonly StockClient _stockClient;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(
        PortfolioRepository portfolioRepository,
        TransactionService transactionService,
        CandleClient candleClient,
        StockClient stockClient,
        ILoggerFactory loggerFactory)
    {
        _portfolioRepository = portfolioRepository;
        _transactionService = transactionService;
        _candleClient = candleClient;
        _stockClient = stockClient;
        _logger = loggerFactory.CreateLogger<PortfolioService>();
    }

    public async Task AddStockToPortfolioAsync(Guid userId, Guid portfolioId, PortfolioHolding stock, TransactionResponse response, string strategyId = "")
    {
        await _portfolioRepository.AddStockToPortfolioAsync(userId, portfolioId, stock, response, strategyId);
    }

    public async Task<Portfolio> CreatePortfolioAsync(Guid userId, CreatePortfolioRequest portfolio, CurrencyCode defaultCurrency = CurrencyCode.USD)
    {
        return await _portfolioRepository.CreateAsync(userId, portfolio, defaultCurrency);
    }

    public async Task DeletePortfolioAsync(Guid userId, Guid portfolioId)
    {
        await _portfolioRepository.DeleteAsync(userId, portfolioId);
    }

    public async Task<List<PortfolioHolding>> GetComposedPortfolioAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await GetPortfolioSummaryAsync(userId, portfolioId);

        var holdings = new List<PortfolioHolding>();

        for (var index = 0; index < portfolio.Stocks.Count; index++)
        {
            holdings.Add(await GetHoldingDetailAsync(portfolio.Stocks[index], CancellationToken.None));
        }

        return holdings;
    }

    public async Task<List<PortfolioHoldingSummary>> GetHoldingSummariesAsync(Guid portfolioId)
    {
        var holdings = await _portfolioRepository.GetHoldingsAsync(portfolioId);

        return holdings
            .Select(h => h.ToPortfolioHoldingSummary())
            .ToList();
    }

    public async Task<Portfolio> GetPortfolioAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await GetPortfolioSummaryAsync(userId, portfolioId);

        var portfolioDto = Portfolio.FromPortfolioSummaryDto(portfolio);

        portfolioDto.Stocks = await GetHoldingDetailsAsync(portfolio.Stocks, CancellationToken.None);
        return portfolioDto;
    }

    public async Task<PortfolioSummary> GetPortfolioSummaryAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await _portfolioRepository.GetPortfolioAsync(userId, portfolioId);

        if (portfolio is null)
        {
            throw new DataNotFoundException($"Portfolio {portfolioId} for user {userId} was not found.");
        }

        var portfolioDto = portfolio.ToPortfolioSummaryDto();

        portfolioDto.Stocks = await GetHoldingSummariesAsync(portfolioId);

        return portfolioDto;
    }

    public async Task<IEnumerable<Portfolio>> GetPortfoliosAsync(Guid userId)
    {
        var portfolios = await _portfolioRepository.GetPortfoliosAsync(userId);

        return portfolios.Select(p => p.ToPortfolioDto());
    }

    public async Task<PortfolioHolding> GetPortfolioHoldingAsync(Guid userId, Guid portfolioId, Guid stockId, CancellationToken cancellationToken)
    {
        var holding = await _portfolioRepository.GetPortfolioHoldingAsync(userId, portfolioId, stockId, cancellationToken);

        if (holding is null)
        {
            throw new DataNotFoundException($"Holding for stock {stockId} not found in portfolio {portfolioId} for user {userId}.");
        }

        var holdingDto = holding.ToPortfolioHolding();

        holdingDto = await GetHoldingDetailAsync(holdingDto, cancellationToken);

        return holdingDto;
    }

    /// <summary>
    /// Gets detailed holdings for a strategy, including average purchase prices and current values.
    /// </summary>
    /// <param name="strategyId">The strategy identifier</param>
    /// <returns>List of portfolio holdings with average prices and current values</returns>
    public async Task<List<PortfolioHolding>> GetStrategyHoldingsAsync(string strategyId, DateTime fromDate, DateTime toDate)
    {
        var holdings = new List<PortfolioHolding>();

        try
        {
            // Get all buy transactions for this strategy
            var transactions = await _transactionService.GetTransactionsByStrategyAsync(strategyId, fromDate, toDate);

            var buyTransactions = transactions
                .Where(t => StaticData.IsBuyOrder(t.Type))
                .ToList();

            if (buyTransactions.Count == 0)
            {
                return holdings;
            }

            // Group by stock and calculate holdings
            var stockGroups = buyTransactions.GroupBy(t => t.StockId);

            foreach (var stockGroup in stockGroups)
            {
                var stockId = stockGroup.Key;
                var stockTransactions = stockGroup.ToList();

                var sellTransactions = transactions
                    .Except(buyTransactions)
                    .Where(t => t.StockId == stockId)
                    .ToList();

                // Calculate total shares and weighted average price
                // Sell transactions have negative quantity, so adding them subtracts from total
                var totalShares = stockTransactions.Sum(t => t.Quantity) + sellTransactions.Sum(t => t.Quantity);
                var totalInvested = stockTransactions.Sum(t => t.TotalCost);
                var averagePrice = totalInvested / totalShares;

                // Get stock details
                var stock = await _stockClient.GetStockAsync(stockId);

                if (stock == null)
                {
                    _logger.LogWarning("Stock with ID {StockId} not found when calculating holdings", stockId);
                    continue;
                }

                // Get current price
                decimal currentValue = 0;

                try
                {
                    if (stock.PreviousClosePrice.HasValue)
                    {
                        currentValue = totalShares * stock.PreviousClosePrice.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get current price for {Symbol}", stock.Symbol);
                }

                // Sell transactions have negative total cost, so negate to get positive proceeds
                var soldAmount = -sellTransactions.Sum(t => t.TotalCost);

                holdings.Add(new PortfolioHolding
                {
                    StockId = stock.StockId,
                    AveragePurchasePrice = Math.Round(averagePrice, 2),
                    AverageSalePrice = sellTransactions.Count > 0 ? Math.Round(sellTransactions.Average(t => t.Price), 2) : 0,
                    CurrencyCode = stock.CurrencyCode,
                    CurrentValue = Math.Round(currentValue, 2),
                    ProfitLoss = Math.Round((currentValue + soldAmount) - totalInvested, 2),
                    SaleAmount = soldAmount,
                    TotalInvested = Math.Round(totalInvested, 2),
                    TotalShares = totalShares,
                    Symbol = stock.Symbol,
                });
            }

            return holdings.OrderBy(h => h.Symbol).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating strategy holdings for {StrategyId}", strategyId);
            return holdings;
        }
    }

    public async Task UpdateHoldingAsync(Guid userId, Guid portfolioId, TransactionResponse transaction, CancellationToken cancellationToken)
    {
        var holding = await GetPortfolioHoldingAsync(userId, portfolioId, transaction.StockId, cancellationToken);

        await _portfolioRepository.UpdatePortfolioHoldingAsync(userId, portfolioId, holding, transaction);

        // Update the current value based on the latest price
        if (holding.PreviousClosePrice.HasValue)
        {
            holding.CurrentValue = holding.PreviousClosePrice.Value * holding.TotalShares;
        }
    }

    public async Task<Portfolio> UpdatePortfolioAsync(Portfolio portfolio)
    {
        // Prevent persisting negative FreeCash; clamp and log.
        if (portfolio.FreeCash.HasValue && portfolio.FreeCash < 0)
        {
            _logger.LogWarning("Portfolio update for {StrategyId} attempted with negative FreeCash {FreeCash}. Clamping to 0.", portfolio.StrategyId, portfolio.FreeCash);
            portfolio.FreeCash = 0m;
        }

        var updatedPortfolio = await _portfolioRepository.UpdateAsync(portfolio);
        return updatedPortfolio.ToPortfolioDto();
    }

    private async Task<List<PortfolioHolding>> GetHoldingDetailsAsync(List<PortfolioHoldingSummary> holdings, CancellationToken cancellationToken)
    {
        var detailedHoldings = new List<PortfolioHolding>();

        foreach (var holding in holdings)
        {
            detailedHoldings.Add(await GetHoldingDetailAsync(holding, cancellationToken));
        }

        return detailedHoldings;
    }

    private async Task<PortfolioHolding> GetHoldingDetailAsync(PortfolioHoldingSummary holding, CancellationToken cancellationToken)
    {
        var transactions = await _transactionService.GetTransactionsByIdsAsync(holding.Transactions);

        var candleTask = _candleClient.GetCandlesAsync(holding.StockId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(-1));

        var portfolioHolding = PortfolioHolding.FromPortfolioHoldingSummary(holding);

        var buyTransactions = transactions
            .Where(t => StaticData.IsBuyOrder(t.Type))
            .ToList();

        var sellTransactions = transactions
            .Except(buyTransactions)
            .ToList();

        var totalShares = buyTransactions.Sum(t => t.Quantity) + sellTransactions.Sum(t => t.Quantity);

        portfolioHolding.AveragePurchasePrice = buyTransactions
            .Average(t => t.Price);

        portfolioHolding.FirstPurchaseDate = buyTransactions.Min(t => t.TransactionDate ?? t.Created);
        portfolioHolding.TotalInvested = buyTransactions.Sum(t => t.TotalCost);
        portfolioHolding.TotalShares = totalShares;

        if (sellTransactions.Count > 0)
        {
            portfolioHolding.AverageSalePrice = sellTransactions
                .Average(t => t.Price);

            portfolioHolding.SaleAmount = -sellTransactions
                .Sum(t => t.TotalCost);

            portfolioHolding.ProfitLoss = (portfolioHolding.CurrentValue + portfolioHolding.SaleAmount) - portfolioHolding.TotalInvested;
        }

        var candles = await candleTask;

        if (candles.Count > 0)
        {
            portfolioHolding.CurrentValue = candles.Last().Close * portfolioHolding.TotalShares;
            portfolioHolding.PreviousClosePrice = candles.Last().Close;
        }

        return portfolioHolding;
    }

    private async Task<PortfolioHolding> GetHoldingDetailAsync(PortfolioHolding holding, CancellationToken cancellationToken)
    {
        var transactions = await _transactionService.GetTransactionsByIdsAsync(holding.Transactions);

        var candleTask = _candleClient.GetCandlesAsync(holding.StockId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(-1));

        var buyTransactions = transactions
            .Where(t => StaticData.IsBuyOrder(t.Type))
            .ToList();

        var sellTransactions = transactions
            .Except(buyTransactions)
            .ToList();

        var totalShares = buyTransactions.Sum(t => t.Quantity) + sellTransactions.Sum(t => t.Quantity);

        holding.AveragePurchasePrice = buyTransactions
            .Average(t => t.Price);

        holding.FirstPurchaseDate = buyTransactions.Min(t => t.TransactionDate ?? t.Created);
        holding.TotalInvested = buyTransactions.Sum(t => t.TotalCost);
        holding.TotalShares = totalShares;

        if (sellTransactions.Count > 0)
        {
            holding.AverageSalePrice = sellTransactions
                .Average(t => t.Price);

            holding.SaleAmount = -sellTransactions
                .Sum(t => t.TotalCost);

            holding.ProfitLoss = (holding.CurrentValue + holding.SaleAmount) - holding.TotalInvested;
        }

        var candles = await candleTask;

        if (candles.Count > 0)
        {
            holding.CurrentValue = candles.Last().Close * holding.TotalShares;
            holding.PreviousClosePrice = candles.Last().Close;
        }

        return holding;
    }
}
