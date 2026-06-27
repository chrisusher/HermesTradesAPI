using System.ComponentModel.DataAnnotations;
using Shared.Database;
using Shared.DTOs.Transactions;

namespace Services.Database;

public class TransactionsTable : CosmosTable
{
    [Required]
    public Guid StockId { get; set; }

    public Guid? OriginalTransactionId { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.Unknown;

    public Guid PortfolioId { get; set; }

    public decimal Price { get; set; }

    public decimal Quantity { get; set; }

    public decimal? QuantityRemaining { get; set; }

    public string? StrategyId { get; set; }

    /// <summary>
    /// Optional strategy version identifier if this transaction was created by a specific strategy version.
    /// </summary>
    public Guid? StrategyVersionId { get; set; }

    public OrderType Type { get; set; }

    public decimal TotalCost { get; set; }

    public decimal TotalCostToUser { get; set; }

    public DateTime? TransactionDate { get; set; }

    public static TransactionsTable FromTransaction(TransactionObject transaction, Guid portfolioId,string? strategyId, Guid? strategyVersionId)
    {
        return new TransactionsTable
        {
            Id = Guid.NewGuid(),
            Created = transaction.Created,
            Currency = transaction.Currency,
            PartitionKey = transaction.StockId.ToString(),
            OriginalTransactionId = transaction.OriginalTransactionId,
            PortfolioId = portfolioId,
            Price = transaction.Price,
            Quantity = transaction.Quantity,
            QuantityRemaining = transaction.Quantity,
            StockId = transaction.StockId,
            StrategyId = strategyId,
            StrategyVersionId = strategyVersionId,
            Type = transaction.Type,
            TotalCost = transaction.TotalCost,
            TotalCostToUser = transaction.TotalCostToUser,
            TransactionDate = transaction.TransactionDate ?? transaction.Created,
        };
    }

    /// <summary>
    /// Converts to TransactionResponse DTO, but SYMBOL IS NOT POPULATED
    /// </summary>
    /// <returns></returns>
    public TransactionResponse ToTransactionResponse()
    {
        return new TransactionResponse
        {
            Created = Created,
            Currency = Currency,
            TransactionId = Id,
            StockId = StockId,
            OriginalTransactionId = OriginalTransactionId,
            PortfolioId = PortfolioId,
            Price = Price,
            Quantity = Quantity,
            QuantityRemaining = QuantityRemaining ?? Quantity,
            StrategyId = StrategyId,
            TotalCost = TotalCost,
            TotalCostToUser = TotalCostToUser,
            TransactionDate = TransactionDate ?? Created,
            Type = Type,
        };
    }

    public TransactionResponse ToTransactionResponse(TransactionObject transaction)
    {
        var response = ToTransactionResponse();
        response.Symbol = transaction.Symbol;
        response.PortfolioId = PortfolioId;
        return response;
    }

    public TransactionResponse ToTransactionResponse(UpdateTransactionRequestBody updateRequest)
    {
        var response = ToTransactionResponse();
        response.Symbol = updateRequest.Symbol;
        response.PortfolioId = PortfolioId;
        return response;
    }
}
