namespace Shared.DTOs.Strategies;

public class StrategySummary : Entity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("strategyId")]
    public string StrategyId { get; set; } = string.Empty;
}
