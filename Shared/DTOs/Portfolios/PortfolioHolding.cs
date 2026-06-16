using Shared.DTOs.Stocks;

namespace Shared.DTOs.Portfolios;

/// <summary>
/// Summary of holdings for a specific stock in a strategy portfolio.
/// </summary>
public class PortfolioHolding : Entity
{
    [JsonPropertyName("portfolioId")]
    public Guid PortfolioId { get; set; }

    [JsonPropertyName("stockId")]
    public required Guid StockId { get; set; }

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }

    [JsonPropertyName("source")]
    public HoldingSource Source { get; set; } = HoldingSource.Strategy;

    [JsonPropertyName("averagePurchasePrice")]
    public decimal AveragePurchasePrice { get; set; }

    [JsonPropertyName("averageSalePrice")]
    public decimal AverageSalePrice { get; set; }

    /// <summary>
    /// Currency code for the stock being held
    /// </summary>
    [JsonPropertyName("currencyCode")]
    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("exchangeName")]
    public string ExchangeName { get; set; } = string.Empty;

    [JsonPropertyName("firstPurchaseDate")]
    public DateTime FirstPurchaseDate { get; set; }

    [JsonPropertyName("previousClosePrice")]
    public decimal? PreviousClosePrice { get; set; }

    [JsonPropertyName("saleAmount")]
    public decimal? SaleAmount { get; set; }

    [JsonPropertyName("stopLoss")]
    public decimal? StopLoss { get; set; }

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

    [JsonPropertyName("totalInvested")]
    public decimal TotalInvested { get; set; }

    [JsonPropertyName("totalShares")]
    public decimal TotalShares { get; set; }

    [JsonPropertyName("transactions")]
    public List<Guid> Transactions { get; set; } = new();

    /// <summary>
    /// Indicates whether this position has been fully closed (all shares sold).
    /// </summary>
    [JsonPropertyName("isClosed")]
    public bool IsClosed => StockStatus == PortfolioStockStatus.FullySold;

    /// <summary>
    /// Profit/loss from selling this position. Only set for closed positions.
    /// </summary>
    [JsonPropertyName("profitLoss")]
    public virtual decimal? ProfitLoss { get; set; }

    public PortfolioStock ToPortfolioStock()
    {
        return new PortfolioStock
        {
            StockId = StockId,
            ExchangeName = ExchangeName,
            Symbol = Symbol,
            CompanyName = CompanyName ?? string.Empty,
            Created = Created,
            CurrencyCode = CurrencyCode,
            Updated = Updated
        };
    }

    public static PortfolioHolding FromPortfolioStock(PortfolioStock stock)
    {
        return new PortfolioHolding
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
