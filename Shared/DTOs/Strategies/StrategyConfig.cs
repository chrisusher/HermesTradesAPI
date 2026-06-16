namespace Shared.DTOs.Strategies;

public class StrategyConfig
{
    [JsonPropertyName("stopLosses")]
    public bool StopLosses { get; set; }
}
