namespace Shared.DTOs.Stocks;

public class Stock : Entity
{
    [JsonPropertyName("stockId")]
    public Guid StockId { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; } = string.Empty;
}
