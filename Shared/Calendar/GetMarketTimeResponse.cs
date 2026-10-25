namespace Shared.Calendar;

public class GetMarketTimeResponse
{
    [JsonPropertyName("isClosed")]
    public bool IsClosed { get; set; } = true;

    [JsonPropertyName("tradingTime")]
    public TradingTimeType TradingTime { get; set; }
}
