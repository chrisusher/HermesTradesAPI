namespace Shared.DTOs.Strategies;

public class Strategy : StrategySummary
{
    [JsonPropertyName("startingBalance")]
    public decimal? StartingBalance { get; set; }

    [JsonPropertyName("maxPositionPercentage")]
    public decimal? MaxPositionPercentage { get; set; }

    [JsonPropertyName("alwaysInvest")]
    public bool AlwaysInvest { get; set; } = false;

    [JsonPropertyName("config")]
    public StrategyConfig Config { get; set; } = new StrategyConfig();

    /// <summary>
    /// The latest version of this strategy, if available.
    /// </summary>
    [JsonPropertyName("latestVersion")]
    public StrategyVersion? LatestVersion { get; set; }
}
