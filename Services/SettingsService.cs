using Shared.DTOs.Settings;
using Shared.Interfaces;

namespace Services;

/// <summary>
/// Service for managing feature flags and settings from blob storage
/// </summary>
public class SettingsService : IAsyncDisposable
{
    private readonly IStorageService _storageService;
    private readonly ILogger<SettingsService> _logger;
    private const string CONTAINER_NAME = "settings";
    private const string FLAG_FILENAME = "feature-flags.json";

    // Cache to avoid frequent blob storage reads
    private FeatureFlagsConfig? _cachedConfig;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public SettingsService(
        IStorageService storageService,
        ILoggerFactory loggerFactory)
    {
        _storageService = storageService;
        _storageService.FolderName = CONTAINER_NAME;
        _logger = loggerFactory.CreateLogger<SettingsService>();
    }

    /// <summary>
    /// Gets the list of disabled features for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>List of disabled features for the user</returns>
    public async Task<List<FeatureFlag>> GetDisabledFeaturesForUserAsync(Guid userId)
    {
        try
        {
            var config = await GetFeatureFlagsConfigAsync();

            if (config is null)
            {
                _logger.LogDebug("No feature flags configuration found, returning empty list for user {UserId}", userId);
                return new List<FeatureFlag>();
            }

            var userFlags = config.FeatureFlags.FirstOrDefault(uf => uf.UserId == userId);

            if (userFlags is null)
            {
                _logger.LogDebug("No feature flags found for user {UserId}, returning empty list", userId);
                return new List<FeatureFlag>();
            }

            _logger.LogInformation("Retrieved {Count} disabled features for user {UserId}", 
                userFlags.DisabledFeatures.Count, userId);

            return userFlags.DisabledFeatures;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disabled features for user {UserId}", userId);
            // Return empty list on error to fail gracefully
            return new List<FeatureFlag>();
        }
    }

    /// <summary>
    /// Checks if a specific feature is enabled for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="feature">The feature to check</param>
    /// <returns>True if the feature is enabled, false if disabled</returns>
    public async Task<bool> IsFeatureEnabledAsync(Guid userId, FeatureFlag feature)
    {
        var disabledFeatures = await GetDisabledFeaturesForUserAsync(userId);

        return !disabledFeatures.Contains(feature);
    }

    /// <summary>
    /// Gets all feature flags configuration
    /// </summary>
    /// <returns>Feature flags configuration</returns>
    public async Task<FeatureFlagsConfig> GetFeatureFlagsConfigAsync()
    {
        await _cacheLock.WaitAsync();

        try
        {
            // Check if cache is still valid
            if (_cachedConfig is not null && DateTime.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached feature flags configuration");
                return _cachedConfig;
            }

            _logger.LogDebug("Cache expired or empty, loading feature flags from blob storage");

            // Try to read from blob storage
            var config = await _storageService.ReadFileAsync<FeatureFlagsConfig>(FLAG_FILENAME);

            if (config is null)
            {
                _logger.LogWarning("Feature flags configuration file not found in blob storage, creating default configuration");
                config = new FeatureFlagsConfig();
            }

            // Update cache
            _cachedConfig = config;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheTimeout);

            _logger.LogInformation("Loaded feature flags configuration with {Count} user configurations", 
                config.FeatureFlags.Count);

            return config;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Updates the feature flags configuration
    /// </summary>
    /// <param name="config">New feature flags configuration</param>
    public async Task UpdateFeatureFlagsConfigAsync(FeatureFlagsConfig config)
    {
        await _cacheLock.WaitAsync();

        try
        {
            config.LastUpdated = DateTime.UtcNow;

            await _storageService.SaveFileAsync(config, FLAG_FILENAME);

            // Invalidate cache
            _cachedConfig = config;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheTimeout);

            _logger.LogInformation("Updated feature flags configuration with {Count} user configurations", 
                config.FeatureFlags.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature flags configuration");
            throw;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Updates disabled features for a specific user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="disabledFeatures">List of features to disable</param>
    public async Task UpdateUserFeatureFlagsAsync(Guid userId, List<FeatureFlag> disabledFeatures)
    {
        try
        {
            var config = await GetFeatureFlagsConfigAsync();

            var userFlags = config.FeatureFlags.FirstOrDefault(uf => uf.UserId == userId);

            if (userFlags is null)
            {
                userFlags = new UserFeatureFlags
                {
                    UserId = userId,
                    DisabledFeatures = disabledFeatures
                };
                config.FeatureFlags.Add(userFlags);
            }
            else
            {
                userFlags.DisabledFeatures = disabledFeatures;
            }

            await UpdateFeatureFlagsConfigAsync(config);

            _logger.LogInformation("Updated feature flags for user {UserId} with {Count} disabled features", 
                userId, disabledFeatures.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating feature flags for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Clears the cache to force reload from blob storage
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();

        try
        {
            _cachedConfig = null;
            _cacheExpiry = DateTime.MinValue;
            _logger.LogInformation("Feature flags cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Disposes resources including the semaphore lock
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cacheLock?.Dispose();
        await ValueTask.CompletedTask;
    }
}
