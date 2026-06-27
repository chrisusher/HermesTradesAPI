namespace Shared.DTOs.Yahoo;

public class PeriodCandle
{
    [JsonIgnore]
    public long Id { get; set; } = -1;

    [JsonPropertyName("DateTime")]
    public DateTime Date { get; set; }

    [JsonPropertyName("Open")]
    public decimal Open { get; set; }

    [JsonPropertyName("High")]
    public decimal High { get; set; }

    [JsonPropertyName("Low")]
    public decimal Low { get; set; }

    [JsonPropertyName("Close")]
    public decimal Close { get; set; }

    [JsonPropertyName("Volume")]
    public long Volume { get; set; }

    [JsonPropertyName("AdjustedClose")]
    public decimal AdjustedClose { get; set; }

    [JsonPropertyName("CandleType")]
    public CandleType CandleType { get; set; } = CandleType.Neutral;
}
