using Services.Repositories;
using Shared.DTOs.Strategies;

namespace Services;

public class StrategyService
{
    private readonly StrategyRepository _strategyRepository;
    private readonly StrategyVersionService _versionService;
    private readonly SettingsService _settingsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StrategyService> _logger;

    public StrategyService(
        IServiceProvider serviceProvider,
        StrategyRepository strategyRepository,
        StrategyVersionService versionService,
        SettingsService settingsService,
        ILoggerFactory loggerFactory)
    {
        _strategyRepository = strategyRepository;
        _versionService = versionService;
        _settingsService = settingsService;
        _serviceProvider = serviceProvider;
        _logger = loggerFactory.CreateLogger<StrategyService>();
    }

    public async Task<Strategy> GetStrategyAsync(string strategyId)
    {
        var strategy = await _strategyRepository.GetStrategyAsync(strategyId);

        if (strategy is null)
        {
            throw new DataNotFoundException($"Strategy {strategyId} not found.");
        }

        var result = strategy.ToStrategy();

        // Attach latest version
        var latestVersion = await _versionService.GetLatestVersionAsync(strategyId);
        result.LatestVersion = latestVersion;

        return result;
    }

    /// <summary>
    /// Gets all strategies, optionally filtered by user feature flags
    /// </summary>
    /// <param name="userId">Optional user ID to filter by feature flags</param>
    /// <returns>List of strategies the user has access to</returns>
    public async Task<IEnumerable<Strategy>> GetStrategiesAsync(Guid? userId = null)
    {
        var strategies = await _strategyRepository.GetStrategiesAsync();

        var strategiesList = strategies
            .Select(s => s.ToStrategy())
            .ToList();

        // Get latest versions for all strategies
        var strategyIds = strategiesList.Select(s => s.StrategyId).ToList();
        var latestVersions = await _versionService.GetLatestVersionsAsync(strategyIds);

        var strategiesWithVersion = strategiesList
            .Where(s => latestVersions.ContainsKey(s.StrategyId));

        foreach (var strategy in strategiesWithVersion)
        {
            var version = latestVersions[strategy.StrategyId];
            strategy.LatestVersion = version;
        }

        // If no user ID provided, return all strategies
        if (userId is null)
        {
            return strategiesList;
        }

        // Check if Strategies feature is disabled for this user
        var disabledFeatures = await _settingsService.GetDisabledFeaturesForUserAsync(userId.Value);

        if (disabledFeatures.Count == 0)
        {
            _logger.LogInformation("No disabled features for user {UserId}, returning all strategies", userId);
            return strategiesList;
        }

        // Filter strategies based on disabled features
        strategiesList = strategiesList
            .Where(s => !disabledFeatures.Any(df => df.ToString().Contains(s.StrategyId.ToUpper())))
            .ToList();

        return strategiesList;
    }

    public async Task<List<StrategySummary>> GetStrategySummariesAsync(List<string> strategies)
    {
        if (strategies == null || strategies.Count == 0)
        {
            return new List<StrategySummary>();
        }

        var strategyTables = await _strategyRepository.GetStrategiesAsync(strategies);

        return strategyTables
            .Select(s => s.ToSummary())
            .ToList();
    }

    public async Task UpdateStrategyAsync(Strategy strategy)
    {
        await _strategyRepository.UpdateStrategyAsync(strategy);
    }
}
