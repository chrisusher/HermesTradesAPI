namespace Shared.DTOs.Settings;

/// <summary>
/// Represents feature flags for a specific user
/// </summary>
public class UserFeatureFlags
{
    /// <summary>
    /// The user's ID
    /// </summary>
    [JsonPropertyName("userId")]
    public required Guid UserId { get; set; }

    /// <summary>
    /// List of features that are disabled for this user
    /// Features not in this list are enabled by default
    /// </summary>
    [JsonPropertyName("disabledFeatures")]
    public List<FeatureFlag> DisabledFeatures { get; set; } = new();
}
