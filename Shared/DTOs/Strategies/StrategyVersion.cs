namespace Shared.DTOs.Strategies;

/// <summary>
/// Represents a version of a strategy.
/// </summary>
public class StrategyVersion : Entity
{
    /// <summary>
    /// Unique identifier for the strategy version.
    /// </summary>
    [JsonPropertyName("strategyVersionId")]
    public Guid StrategyVersionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The strategy this version belongs to.
    /// </summary>
    [JsonPropertyName("strategyId")]
    public required string StrategyId { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0.0", "v2", etc.).
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>
    /// Optional description of what changed in this version.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
