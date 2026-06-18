namespace Shared.DTOs.Settings;

/// <summary>
/// Root configuration for feature flags stored in blob storage
/// </summary>
public class FeatureFlagsConfig
{
    /// <summary>
    /// List of user-specific feature flag configurations
    /// </summary>
    [JsonPropertyName("userFeatureFlags")]
    public List<UserFeatureFlags> FeatureFlags { get; set; } = new();

    /// <summary>
    /// When the configuration was last updated
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
