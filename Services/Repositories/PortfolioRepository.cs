using Microsoft.EntityFrameworkCore;
using Services.Database;
using Shared.DTOs.Portfolios;
using Shared.DTOs.Stocks;
using Shared.DTOs.Transactions;

namespace Services.Repositories;

public class PortfolioRepository
{
    private readonly DatabaseContext _context;
    private readonly StrategyRepository _strategyRepository;

    public PortfolioRepository(
        DatabaseContext context,
        StrategyRepository strategyRepository)
    {
        _context = context;
        _strategyRepository = strategyRepository;
    }

    public async Task<PortfolioHoldingTable> AddAdhocHoldingAsync(Guid portfolioId, PortfolioStock stock, AddAdhocHoldingRequest request)
    {
        var portfolio = await _context.Portfolio
            .FirstOrDefaultAsync(x => x.Id == portfolioId);

        if (portfolio is null)
        {
            throw new DataNotFoundException($"Portfolio {portfolioId} not found.");
        }

        var holding = await _context.PortfolioHoldings
            .FirstOrDefaultAsync(x => x.PartitionKey == portfolioId.ToString()
                && x.PortfolioId == portfolioId
                && x.StockId == stock.StockId
                && x.StrategyId == request.StrategyId
                && x.Source == HoldingSource.Manual);

        if (!(holding is null))
        {
            return holding;
        }

        holding = new PortfolioHoldingTable
        {
            PartitionKey = portfolioId.ToString(),
            PortfolioId = portfolioId,
            Symbol = stock.Symbol,
            ExchangeName = stock.ExchangeName,
            CurrencyCode = stock.CurrencyCode,
            FirstPurchaseDate = request.DatePurchased,
            StrategyId = request.StrategyId,
            StockId = stock.StockId,
            Source = HoldingSource.Manual,
            Quantity = request.Shares,
            Updated = DateTime.UtcNow
        };

        _context.PortfolioHoldings.Add(holding);
        await _context.SaveChangesAsync();

        return holding;
    }

    public async Task AddStockToPortfolioAsync(Guid userId, Guid portfolioId, PortfolioHolding stock, TransactionResponse response, string strategyId)
    {
        var portfolio = await GetPortfolioAsync(userId, portfolioId);

        // Migration step: clear embedded Stocks list if present to reduce document size going forward.
        var holding = await _context.PortfolioHoldings
            .FirstOrDefaultAsync(x => x.PartitionKey == portfolio.Id.ToString()
                && x.StockId == stock.StockId
                && x.Source == HoldingSource.Strategy);

        if (holding is null)
        {
            holding = new PortfolioHoldingTable
            {
                PartitionKey = portfolio.Id.ToString(),
                PortfolioId = portfolio.Id,
                Symbol = stock.Symbol,
                ExchangeName = stock.ExchangeName,
                CurrencyCode = stock.CurrencyCode,
                FirstPurchaseDate = stock.FirstPurchaseDate,
                StrategyId = strategyId,
                StockId = stock.StockId,
                Source = HoldingSource.Strategy,
                Quantity = response.Quantity,
                Transactions = new List<string>
                {
                    response.TransactionId.ToString()
                },
                Updated = DateTime.UtcNow
            };

            _context.PortfolioHoldings.Add(holding);
        }
        else
        {
            if (holding.Transactions.Contains(response.TransactionId.ToString()))
            {
                // Transaction already processed for this holding
                return;
            }

            holding.Quantity += response.Quantity;
            holding.Transactions.Add(response.TransactionId.ToString());
            holding.Updated = DateTime.UtcNow;
            _context.PortfolioHoldings.Update(holding);
        }

        // FreeCash mutation for buys/sells now handled exclusively in TransactionRepository.
        // (Avoid double adjustment that previously caused negative drift.)
        portfolio.FreeCash -= response.TotalCost;
        portfolio.Updated = DateTime.UtcNow;
        _context.Portfolio.Update(portfolio);

        await _context.SaveChangesAsync();
    }

    public async Task AddStocksToPortfolioAsync(Guid userId, Guid portfolioId, string strategyId, IReadOnlyCollection<(PortfolioHolding Holding, TransactionResponse Response)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var portfolio = await GetPortfolioAsync(userId, portfolioId);
        var partitionKey = portfolio.Id.ToString();

        var holdings = await _context.PortfolioHoldings
            .Where(x => x.PartitionKey == partitionKey && x.Source == HoldingSource.Strategy)
            .ToDictionaryAsync(x => x.StockId);

        foreach (var entry in entries)
        {
            var holdingInput = entry.Holding;
            var response = entry.Response;

            if (holdings.TryGetValue(holdingInput.StockId, out var holding))
            {
                holding.Transactions ??= new List<string>();

                if (holding.Transactions.Contains(response.TransactionId.ToString()))
                {
                    continue;
                }

                holding.Quantity += response.Quantity;
                holding.Transactions.Add(response.TransactionId.ToString());
                holding.Updated = DateTime.UtcNow;
                holding.CurrencyCode = holdingInput.CurrencyCode;
                holding.FirstPurchaseDate = holdingInput.FirstPurchaseDate;
                holding.StrategyId = strategyId;

                _context.PortfolioHoldings.Update(holding);
            }
            else
            {
                holding = new PortfolioHoldingTable
                {
                    PartitionKey = partitionKey,
                    PortfolioId = portfolio.Id,
                    Symbol = holdingInput.Symbol,
                    ExchangeName = holdingInput.ExchangeName,
                    CurrencyCode = holdingInput.CurrencyCode,
                    FirstPurchaseDate = holdingInput.FirstPurchaseDate,
                    StrategyId = strategyId,
                    StockId = holdingInput.StockId,
                    Source = HoldingSource.Strategy,
                    Quantity = response.Quantity,
                    Transactions = new List<string>
                    {
                        response.TransactionId.ToString()
                    },
                    Updated = DateTime.UtcNow
                };

                holdings[holdingInput.StockId] = holding;
                _context.PortfolioHoldings.Add(holding);
            }
        }

        portfolio.FreeCash -= entries.Sum(e => e.Response.TotalCost);
        portfolio.Updated = DateTime.UtcNow;
        _context.Portfolio.Update(portfolio);

        await _context.SaveChangesAsync();
    }

    public async Task<Portfolio> CreateAsync(Guid userId, CreatePortfolioRequest portfolio)
    {
        var entity = new PortfolioTable
        {
            Id = Guid.NewGuid(),
            PartitionKey = userId.ToString(),
            UserId = userId,
            Name = portfolio.Name,
            AlwaysInvest = portfolio.AlwaysInvest,
            Description = portfolio.Description,
            StrategyId = portfolio.StrategyId ?? string.Empty,
            FreeCash = portfolio.FreeCash,
            Created = DateTime.UtcNow,
            Updated = DateTime.UtcNow,
            Status = StatusType.Active
        };

        _context.Portfolio.Add(entity);
        await _context.SaveChangesAsync();

        return entity.ToPortfolioDto();
    }

    public async Task DeleteAsync(Guid userId, Guid portfolioId)
    {
        var portfolio = await _context.Portfolio
            .WithPartitionKey(userId.ToString())
            .FirstOrDefaultAsync(x => x.Id == portfolioId);

        if (portfolio is null)
        {
            throw new DataNotFoundException($"Portfolio {portfolioId} for user {userId} not found.");
        }

        var holdings = await _context.PortfolioHoldings
            .WithPartitionKey(userId.ToString())
            .Where(h => h.PortfolioId == portfolioId)
            .ToListAsync();

        if (holdings.Count > 0)
        {
            _context.PortfolioHoldings.RemoveRange(holdings);
        }

        _context.Portfolio.Remove(portfolio);
        await _context.SaveChangesAsync();
    }

    public async Task CreatePortfolioHistoryAsync(string strategyId)
    {
        var strategy = await _strategyRepository.GetStrategyAsync(strategyId);

        if (strategy is null)
        {
            throw new DataNotFoundException($"Strategy {strategyId} not found.");
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all unique stock symbols that are actively held across all user portfolios.
    /// A holding is considered active if its status is not <see cref="PortfolioStockStatus.FullySold"/>.
    /// </summary>
    /// <returns>A deduplicated list of symbols in <c>{ExchangeName}.{Symbol}</c> format.</returns>
    public async Task<List<string>> GetAllActiveHoldingSymbolsAsync()
    {
        var holdings = await _context.PortfolioHoldings
            .AsNoTracking()
            .Where(h => h.StockStatus != PortfolioStockStatus.FullySold)
            .Select(h => new { h.ExchangeName, h.Symbol })
            .ToListAsync();

        return holdings
            .Select(h => $"{h.ExchangeName}.{h.Symbol}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<PortfolioHistoryTable>> GetAllAsync()
    {
        return await _context.PortfolioHistory
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<PortfolioTable>> GetPortfoliosAsync(Guid userId)
    {
        return await _context.Portfolio
            .AsNoTracking()
            .WithPartitionKey(userId.ToString())
            .ToListAsync();
    }

    /// <summary>
    /// Builds a domain Portfolio object composing holdings from separate documents.
    /// </summary>
    public async Task<Portfolio> GetComposedPortfolioAsync(Guid userId, Guid portfolioId)
    {
        var portfolioEntity = await GetPortfolioAsync(userId, portfolioId);
        var holdings = await GetHoldingsAsync(portfolioEntity.Id);

        return new Portfolio
        {
            PortfolioId = portfolioEntity.Id,
            StrategyId = portfolioEntity.StrategyId,
            FreeCash = portfolioEntity.FreeCash,
            Created = portfolioEntity.Created,
            Updated = portfolioEntity.Updated,
            Status = portfolioEntity.Status,
            Stocks = holdings.Select(h => h.ToPortfolioHolding()).ToList()
        };
    }
    

    public async Task<PortfolioHistoryTable?> GetHistoryAsync(string strategyId)
    {
        return await _context.PortfolioHistory
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PartitionKey == strategyId);
    }

    /// <summary>
    /// Returns holdings for a strategy/backtest.
    /// </summary>
    public async Task<List<PortfolioHoldingTable>> GetHoldingsAsync(Guid portfolioId)
    {
        return await _context.PortfolioHoldings
            .AsNoTracking()
            .Where(h => h.PortfolioId == portfolioId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the most recent trade-related timestamp for a given backtest across all strategies.
    /// Considers both portfolio entity updates and individual holding updates.
    /// </summary>
    /// <param name="backtestId">The backtest identifier.</param>
    /// <returns>Latest UTC DateTime of trade activity, or null if none.</returns>
    // public async Task<DateTime?> GetLastTradeTimeForBacktestAsync(Guid backtestId)
    // {
    //     // Find all portfolios associated with the backtest
    //     var portfolios = await _context.Portfolio
    //         .AsNoTracking()
    //         .Where(p => p.BacktestId == backtestId)
    //         .Select(p => new { p.Id, p.Updated })
    //         .ToListAsync();

    //     if (portfolios.Count == 0)
    //     {
    //         return null;
    //     }

    //     DateTime? latest = null;

    //     foreach (var p in portfolios)
    //     {
    //         if (p.Updated is not null)
    //         {
    //             latest = latest is null || p.Updated > latest ? p.Updated : latest;
    //         }

    //         // For holdings, use the partition key pattern to scope by strategy + backtest
    //         var holdingLatest = await _context.PortfolioHoldings
    //             .AsNoTracking()
    //             .Where(h => h.PartitionKey == p.Id.ToString())
    //             .Select(h => h.Updated)
    //             .OrderByDescending(u => u)
    //             .FirstOrDefaultAsync();

    //         if (holdingLatest != default)
    //         {
    //             latest = latest is null || holdingLatest > latest ? holdingLatest : latest;
    //         }
    //     }

    //     return latest;
    // }

    public async Task<PortfolioTable> GetPortfolioAsync(Guid userId, Guid portfolioId)
    {
        var query = _context.Portfolio.AsQueryable();

        var portfolio = await query
            .WithPartitionKey(userId.ToString())
            .FirstOrDefaultAsync(x => x.Id == portfolioId);  // Cannot use AsNoTracking here as we need to update the entity later

        if (portfolio is null)
        {
            throw new DataNotFoundException($"Portfolio {portfolioId} for user {userId} not found.");
        }

        return portfolio;
    }

    public async Task<PortfolioTable> UpdateAsync(Portfolio portfolio)
    {
        var portfolioEntity = await _context.Portfolio
            .WithPartitionKey(portfolio.UserId.ToString())
            .FirstOrDefaultAsync(x => x.Id == portfolio.PortfolioId);

        if (portfolioEntity is null)
        {
            throw new DataNotFoundException($"Portfolio for strategy {portfolio.StrategyId} not found.");
        }

        var newFreeCash = portfolio.FreeCash;

        if (newFreeCash is not null && newFreeCash < 0)
        {
            newFreeCash = 0m;
        }

        portfolioEntity.FreeCash = newFreeCash;
        portfolioEntity.Updated = DateTime.UtcNow;

        // Persist holdings individually to avoid large portfolio document size.
        // Load existing holdings as tracked entities so we update the same instances
        // and avoid attaching multiple different instances with the same key.
        var existingHoldings = await _context.PortfolioHoldings
            .Where(h => h.PortfolioId == portfolioEntity.Id)
            .ToListAsync();

        // Deduplicate incoming stocks by StockId to avoid processing duplicates
        var incoming = portfolio.Stocks
            .GroupBy(s => s.StockId)
            .Select(g => g.First())
            .ToList();

        foreach (var stock in incoming)
        {
            var holding = existingHoldings.FirstOrDefault(h => h.StockId == stock.StockId);

            if (holding is null)
            {
                holding = new PortfolioHoldingTable
                {
                    PartitionKey = portfolioEntity.Id.ToString(),
                    PortfolioId = portfolioEntity.Id,
                    Symbol = stock.Symbol,
                    ExchangeName = stock.ExchangeName,
                    CurrencyCode = stock.CurrencyCode,
                    FirstPurchaseDate = stock.FirstPurchaseDate,
                    StrategyId = stock.StrategyId,
                    StockId = stock.StockId,
                    Source = stock.Source,
                    Quantity = stock.TotalShares,
                    Transactions = stock.Transactions.Select(t => t.ToString()).ToList(),
                    Updated = DateTime.UtcNow,
                };
                _context.PortfolioHoldings.Add(holding);
            }
            else
            {
                // existingHoldings were loaded as tracked; modify properties directly
                holding.FirstPurchaseDate = stock.FirstPurchaseDate;
                holding.StrategyId = stock.StrategyId;
                holding.ProfitLoss = stock.ProfitLoss ?? 0;
                holding.Quantity = stock.TotalShares;
                holding.SaleAmount = stock.SaleAmount;
                holding.Source = stock.Source;
                holding.StockStatus = stock.StockStatus;
                // Replace transactions list (could be optimized diff later)
                holding.Transactions = stock.Transactions.Select(t => t.ToString()).ToList();
                holding.Updated = DateTime.UtcNow;
                // No explicit Update call required for tracked entities
            }
        }

        // Remove holdings that are no longer present (fully sold positions)
        // var symbolsInPortfolio = portfolio.Stocks.Select(s => s.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // foreach (var stale in existingHoldings.Where(h => !symbolsInPortfolio.Contains(h.Symbol)))
        // {
        //     _context.PortfolioHoldings.Remove(stale);
        // }

        _context.Portfolio.Update(portfolioEntity);
        await _context.SaveChangesAsync();
        return portfolioEntity;
    }

    public async Task UpdatePortfolioHoldingAsync(Guid userId, Guid portfolioId, PortfolioHolding stock, TransactionResponse response)
    {
        var portfolio = await GetPortfolioAsync(userId, portfolioId);

        if (portfolio is null)
        {
            throw new DataNotFoundException($"Portfolio {portfolioId} for user {userId} not found.");
        }

        var holding = await _context.PortfolioHoldings
            .FirstOrDefaultAsync(x => x.PartitionKey == portfolio.Id.ToString()
            && x.StockId == stock.StockId
            && x.Source == HoldingSource.Strategy);

        if (holding is null)
        {
            throw new DataNotFoundException($"Holding for {stock.Symbol} not found in portfolio {portfolioId}.");
        }

        if (holding.Transactions.Contains(response.TransactionId.ToString()))
        {
            // Transaction already processed for this holding
            return;
        }

        holding.CurrencyCode = stock.CurrencyCode;
        holding.FirstPurchaseDate = stock.FirstPurchaseDate;

        holding.Quantity += response.Quantity;
        holding.SaleAmount = stock.SaleAmount;
        holding.StockStatus = stock.StockStatus;

        if (holding.Quantity <= 0)
        {
            holding.StockStatus = PortfolioStockStatus.FullySold;
        }

        holding.Transactions.Add(response.TransactionId.ToString());
        holding.Updated = DateTime.UtcNow;
        _context.PortfolioHoldings.Update(holding);

        portfolio.FreeCash -= response.TotalCost;
        portfolio.Updated = DateTime.UtcNow;
        _context.Portfolio.Update(portfolio);

        await _context.SaveChangesAsync();
    }
}
