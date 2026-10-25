namespace Shared.DTOs.Strategies;

public sealed class StrategyTrade
{
    public Guid TransactionId { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid UserId { get; set; }
    public Guid StockId { get; set; }
    public Guid? OriginalTransactionId { get; set; }
    public string StrategyId { get; set; } = "";
    public string StrategyVersion { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Exchange { get; set; } = "";
    public decimal Price { get; set; }

    // Quantity and all amounts are positive for purchases, negative for sales.
    public decimal Quantity { get; set; }
    public decimal PortfolioAmount { get; set; }
    public decimal UserAmount { get; set; }
    public CurrencyCode StockCurrency { get; set; }
    public CurrencyCode PortfolioCurrency { get; set; }
    public DateTimeOffset Time { get; set; }
    public DateTimeOffset SourceCandleTime { get; set; }
}
