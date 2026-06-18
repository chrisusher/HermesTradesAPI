using Microsoft.EntityFrameworkCore;
using Services.Database;
using Shared.DTOs.Strategies;

namespace Services.Repositories;

public class StrategyRepository
{
    private readonly DatabaseContext _dbContext;

    public StrategyRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StrategyTable?> GetStrategyAsync(string strategyId)
    {
        return await _dbContext.Strategies
            .FirstOrDefaultAsync(s => s.PartitionKey == strategyId);
    }

    public async Task<IEnumerable<StrategyTable>> GetStrategiesAsync()
    {
        return await _dbContext.Strategies
            .AsNoTracking()
            .Where(s => s.Status == StatusType.Active)
            .ToListAsync();
    }

    public async Task<StrategyTable> CreateStrategyAsync(Strategy strategy)
    {
        var strategyTable = StrategyTable.FromStrategy(strategy);
        await _dbContext.Strategies.AddAsync(strategyTable);
        await _dbContext.SaveChangesAsync();

        return strategyTable;
    }

    public async Task DeleteStrategyAsync(string strategyId)
    {
        var strategy = await GetStrategyAsync(strategyId);

        if (strategy == null)
        {
            throw new DataNotFoundException($"Strategy {strategyId} not found");
        }

        _dbContext.Strategies.Remove(strategy);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<StrategyTable>> GetStrategiesAsync(List<string> strategies)
    {
        return await _dbContext.Strategies
            .AsNoTracking()
            .Where(s => strategies.Contains(s.PartitionKey))
            .ToListAsync();
    }

    public async Task UpdateStrategyAsync(Strategy strategy)
    {
        // Ensure we are not reusing a locally tracked Strategy entity that may lack Cosmos document metadata.
        var localTrackedStrategy = _dbContext.Strategies.Local
            .FirstOrDefault(s => s.PartitionKey == strategy.StrategyId);

        if (localTrackedStrategy != null)
        {
            _dbContext.Entry(localTrackedStrategy).State = EntityState.Detached;
        }

        var existingStrategy = await _dbContext.Strategies
            .FirstOrDefaultAsync(s => s.PartitionKey == strategy.StrategyId);

        if (existingStrategy == null)
        {
            throw new DataNotFoundException($"Strategy {strategy.StrategyId} not found");
        }

        existingStrategy.Name = strategy.Name;
        existingStrategy.Description = strategy.Description;
        existingStrategy.AlwaysInvest = strategy.AlwaysInvest;
        existingStrategy.StartingBalance = strategy.StartingBalance;
        existingStrategy.MaxPositionPercentage = strategy.MaxPositionPercentage;
        existingStrategy.Config = strategy.Config ?? existingStrategy.Config ?? new StrategyConfig();
        existingStrategy.Status = strategy.Status;
        existingStrategy.Updated = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
    }
}
