using Shared.DTOs.Stocks;

namespace Shared.DTOs.Portfolios;

/// <summary>
/// Summary of holdings for a specific stock in a strategy portfolio.
/// </summary>
public class PortfolioHolding : PortfolioHoldingSummary
{
    [JsonPropertyName("averagePurchasePrice")]
    public decimal AveragePurchasePrice { get; set; }

    [JsonPropertyName("averageSalePrice")]
    public decimal AverageSalePrice { get; set; }

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("firstPurchaseDate")]
    public DateTime FirstPurchaseDate { get; set; }

    [JsonPropertyName("previousClosePrice")]
    public decimal? PreviousClosePrice { get; set; }

    [JsonPropertyName("saleAmount")]
    public decimal? SaleAmount { get; set; }

    [JsonPropertyName("stopLoss")]
    public decimal? StopLoss { get; set; }

    [JsonPropertyName("totalInvested")]
    public decimal TotalInvested { get; set; }

    [JsonPropertyName("totalShares")]
    public decimal TotalShares { get; set; }

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

    public new static PortfolioHolding FromPortfolioStock(PortfolioStock stock)
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

    public static PortfolioHolding FromPortfolioHoldingSummary(PortfolioHoldingSummary summary)
    {
        return new PortfolioHolding
        {
            PortfolioId = summary.PortfolioId,
            StockId = summary.StockId,
            ExchangeName = summary.ExchangeName,
            Symbol = summary.Symbol,
            CompanyName = summary.CompanyName,
            Created = summary.Created,
            CurrencyCode = summary.CurrencyCode,
            Updated = summary.Updated
        };
    }
}
