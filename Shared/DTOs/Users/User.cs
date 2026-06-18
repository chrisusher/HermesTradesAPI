using Shared.DTOs;

namespace Shared.DTOs.Users;

/// <summary>
/// Represents user information stored in the application database
/// </summary>
public class User : Entity
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    /// <summary>
    /// List of features that are disabled for this user
    /// Features not in this list are enabled by default
    /// </summary>
    [JsonPropertyName("disabledFeatures")]
    public List<FeatureFlag> DisabledFeatures { get; set; } = new();
}