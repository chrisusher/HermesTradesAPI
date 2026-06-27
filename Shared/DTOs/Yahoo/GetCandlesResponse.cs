namespace Shared.DTOs.Yahoo;

public class GetCandlesResponse
{
    [JsonPropertyName("stockId")]
    public Guid StockId { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; } = string.Empty;

    [JsonPropertyName("candles")]
    public List<PeriodCandle> Candles { get; set; } = new List<PeriodCandle>();
}
