namespace Shared.DTOs.Stocks;

public class StockSummary : Stock
{
    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyName("type")]
    public StockType StockType { get; set; } = StockType.Unknown;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("previousClosePrice")]
    public decimal? PreviousClosePrice { get; set; }

    [JsonPropertyName("currencyCode")]
    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    [JsonPropertyName("firstTradeDate")]
    public DateTime? FirstTradeDate { get; set; }

    [JsonPropertyName("marketCap")]
    public decimal? MarketCap { get; set; }

    [JsonPropertyName("nextEarningsDate")]
    public DateTime? NextEarningsDate { get; set; }

    public IndexStock ToIndexStock()
    {
        return new IndexStock
        {
            CompanyName = CompanyName,
            ExchangeName = ExchangeName,
            Symbol = Symbol,
        };
    }
}
