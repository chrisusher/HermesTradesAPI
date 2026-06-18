using Services.Repositories;
using Shared.DTOs.Strategies;

namespace Services;

public class StrategyVersionService
{
    private readonly StrategyVersionRepository _versionRepository;
    private readonly StrategyRepository _strategyRepository;
    private readonly ILogger<StrategyVersionService> _logger;

    public StrategyVersionService(
        StrategyVersionRepository versionRepository,
        StrategyRepository strategyRepository,
        ILoggerFactory loggerFactory)
    {
        _versionRepository = versionRepository;
        _strategyRepository = strategyRepository;
        _logger = loggerFactory.CreateLogger<StrategyVersionService>();
    }

    /// <summary>
    /// Gets the latest version for a strategy.
    /// </summary>
    public async Task<StrategyVersion?> GetLatestVersionAsync(string strategyId)
    {
        var version = await _versionRepository.GetLatestVersionAsync(strategyId);
        return version?.ToStrategyVersion();
    }

    /// <summary>
    /// Gets all versions for a strategy.
    /// </summary>
    public async Task<IEnumerable<StrategyVersion>> GetVersionsAsync(string strategyId)
    {
        var versions = await _versionRepository.GetVersionsAsync(strategyId);
        return versions.Select(v => v.ToStrategyVersion());
    }

    /// <summary>
    /// Creates a new version for a strategy.
    /// </summary>
    public async Task<StrategyVersion> CreateVersionAsync(string strategyId, string version, string? description = null)
    {
        // Verify strategy exists
        var strategy = await _strategyRepository.GetStrategyAsync(strategyId);

        if (strategy is null)
        {
            throw new DataNotFoundException($"Strategy {strategyId} not found.");
        }

        var strategyVersion = new StrategyVersion
        {
            StrategyId = strategyId,
            Version = version,
            Description = description,
            Created = DateTime.UtcNow,
            Status = StatusType.Active,
            Updated = DateTime.UtcNow,
        };

        var created = await _versionRepository.CreateVersionAsync(strategyVersion);
        _logger.LogInformation("Created version {Version} for strategy {StrategyId}", version, strategyId);

        return created.ToStrategyVersion();
    }

    /// <summary>
    /// Gets the latest versions for multiple strategies.
    /// </summary>
    public async Task<Dictionary<string, StrategyVersion>> GetLatestVersionsAsync(IEnumerable<string> strategyIds)
    {
        var versions = await _versionRepository.GetLatestVersionsAsync(strategyIds);
        return versions.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToStrategyVersion()
        );
    }
}
