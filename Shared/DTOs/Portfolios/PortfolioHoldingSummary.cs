using Shared.DTOs.Stocks;

namespace Shared.DTOs.Portfolios;

/// <summary>
/// Summary of holdings for a specific stock in a strategy portfolio.
/// </summary>
public class PortfolioHoldingSummary : Entity
{
    [JsonPropertyName("portfolioId")]
    public Guid PortfolioId { get; set; }

    [JsonPropertyName("stockId")]
    public required Guid StockId { get; set; }

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }

    [JsonPropertyName("source")]
    public HoldingSource Source { get; set; } = HoldingSource.Strategy;

    /// <summary>
    /// Currency code for the stock being held
    /// </summary>
    [JsonPropertyName("currencyCode")]
    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; } = string.Empty;

    // Rename JSON property to avoid collision with Entity.Status ("status")
    [JsonPropertyName("stockStatus")]
    public PortfolioStockStatus StockStatus { get; set; } = PortfolioStockStatus.Active;

    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Company name for this stock.
    /// </summary>
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    /// <summary>
    /// Market sector for this stock (e.g. "Technology", "Healthcare").
    /// </summary>
    [JsonPropertyName("sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("transactions")]
    public List<Guid> Transactions { get; set; } = new();

    /// <summary>
    /// Indicates whether this position has been fully closed (all shares sold).
    /// </summary>
    [JsonPropertyName("isClosed")]
    public bool IsClosed => StockStatus == PortfolioStockStatus.FullySold;

    public static PortfolioHoldingSummary FromPortfolioStock(PortfolioStock stock)
    {
        return new PortfolioHoldingSummary
        {
            StockId = stock.StockId,
            ExchangeName = stock.ExchangeName,
            Symbol = stock.Symbol,
            CompanyName = stock.CompanyName,
            Created = stock.Created,
            CurrencyCode = stock.CurrencyCode,
            Updated = stock.Updated
        };
    }
}
