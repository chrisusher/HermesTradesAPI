using Shared.Database;
using Shared.DTOs.Portfolios;

namespace Services.Database;

/// <summary>
/// Represents a single stock holding within a portfolio. This breaks the large embedded
/// Stocks collection previously stored on <see cref="PortfolioTable"/> into individually
/// addressable documents to avoid the Cosmos DB 2MB item size limit and reduce RU charges
/// for partial updates.
/// </summary>
public class PortfolioHoldingTable : CosmosTable
{
    public required Guid PortfolioId { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public string ExchangeName { get; set; } = string.Empty;

    public CurrencyCode CurrencyCode { get; set; } = CurrencyCode.Unknown;

    public Guid StockId { get; set; } = Guid.Empty;

    public string? StrategyId { get; set; }

    public DateTime FirstPurchaseDate { get; set; }

    public decimal? ProfitLoss { get; set; }

    public decimal Quantity { get; set; }

    public HoldingSource Source { get; set; } = HoldingSource.Strategy;

    public decimal? SaleAmount { get; set; }

    public PortfolioStockStatus StockStatus { get; set; } = PortfolioStockStatus.Active;

    /// <summary>
    /// List of transaction identifiers associated with this holding. Consider capping / moving
    /// to a separate container if growth becomes unbounded.
    /// </summary>
    public List<string> Transactions { get; set; } = [];

    public PortfolioHolding ToPortfolioHolding()
    {
        return new PortfolioHolding
        {
            PortfolioId = PortfolioId,
            Symbol = Symbol,
            ExchangeName = ExchangeName,
            StockId = StockId,
            CurrencyCode = CurrencyCode,
            FirstPurchaseDate = FirstPurchaseDate,
            ProfitLoss = ProfitLoss,
            SaleAmount = SaleAmount,
            Source = Source,
            StockStatus = StockStatus,
            StrategyId = StrategyId,
            TotalShares = Quantity,
            Transactions = Transactions
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => Guid.TryParse(t, out var transactionId) ? transactionId : Guid.Empty)
                .Where(transactionId => transactionId != Guid.Empty)
                .ToList()
        };
    }
}
