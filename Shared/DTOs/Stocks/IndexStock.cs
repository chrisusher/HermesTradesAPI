namespace Shared.DTOs.Stocks;

public class IndexStock
{
    [JsonPropertyName("addedSecurity")]
    public string CompanyName { get; set; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public CurrencyCode CurrencyCode { get; set; }

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("marketCap")]
    public decimal? MarketCap { get; set; }

    public StockSummary ToSummary()
    {
        return new StockSummary
        {
            CompanyName = CompanyName,
            CurrencyCode = CurrencyCode,
            ExchangeName = ExchangeName,
            Symbol = Symbol,
        };
    }

    public PortfolioStock ToPortfolioStock()
    {
        return new PortfolioStock
        {
            CompanyName = CompanyName,
            CurrencyCode = CurrencyCode,
            ExchangeName = ExchangeName,
            Symbol = Symbol,
        };
    }
}
