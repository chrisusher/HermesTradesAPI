
using Microsoft.EntityFrameworkCore;
using Services.Database;
using Shared.DTOs.Strategies;

namespace Services.Repositories;

public class StrategyVersionRepository
{
    private readonly DatabaseContext _dbContext;

    public StrategyVersionRepository(DatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates a new strategy version.
    /// </summary>
    public async Task<StrategyVersionTable> CreateVersionAsync(StrategyVersion version)
    {
        var versionTable = StrategyVersionTable.FromStrategyVersion(version);
        await _dbContext.StrategyVersions.AddAsync(versionTable);
        await _dbContext.SaveChangesAsync();

        return versionTable;
    }

    /// <summary>
    /// Gets the latest strategy version for a given strategy ID.
    /// </summary>
    public async Task<StrategyVersionTable?> GetLatestVersionAsync(string strategyId)
    {
        return await _dbContext.StrategyVersions
            .AsNoTracking()
            .Where(sv => sv.PartitionKey == strategyId && sv.Status == StatusType.Active)
            .OrderByDescending(sv => sv.Created)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets all versions for a given strategy ID.
    /// </summary>
    public async Task<IEnumerable<StrategyVersionTable>> GetVersionsAsync(string strategyId)
    {
        return await _dbContext.StrategyVersions
            .AsNoTracking()
            .Where(sv => sv.PartitionKey == strategyId && sv.Status == StatusType.Active)
            .OrderByDescending(sv => sv.Created)
            .ToListAsync();
    }

    /// <summary>
    /// Gets the latest active strategy version that was created on or before the specified date.
    /// This is useful for determining which version was active at a specific point in time (e.g., when a backtest was run).
    /// </summary>
    /// <param name="strategyId">The strategy identifier.</param>
    /// <param name="asOfDate">The date to query against. Returns the latest version created on or before this date.</param>
    /// <returns>The strategy version that was active at the specified date, or null if none found.</returns>
    public async Task<StrategyVersionTable?> GetVersionAsOfAsync(string strategyId, DateTime asOfDate)
    {
        return await _dbContext.StrategyVersions
            .AsNoTracking()
            .Where(sv => sv.PartitionKey == strategyId 
                      && sv.Status == StatusType.Active 
                      && sv.Created <= asOfDate)
            .OrderByDescending(sv => sv.Created)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets a specific version by its ID.
    /// </summary>
    public async Task<StrategyVersionTable?> GetVersionByIdAsync(Guid versionId)
    {
        return await _dbContext.StrategyVersions
            .FirstOrDefaultAsync(sv => sv.Id == versionId);
    }

    /// <summary>
    /// Gets the latest version for multiple strategies.
    /// </summary>
    public async Task<Dictionary<string, StrategyVersionTable>> GetLatestVersionsAsync(IEnumerable<string> strategyIds)
    {
        var versions = await _dbContext.StrategyVersions
            .AsNoTracking()
            .Where(sv => strategyIds.Contains(sv.PartitionKey) && sv.Status == StatusType.Active)
            .ToListAsync();

        return versions
            .GroupBy(sv => sv.StrategyId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(sv => sv.Created).First()
            );
    }
}
