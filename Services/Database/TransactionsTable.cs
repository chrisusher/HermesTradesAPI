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

    public decimal Price { get; set; }

    public decimal Quantity { get; set; }

    public decimal? QuantityRemaining { get; set; }

    public string? ScreenerId { get; set; }

    public string? StrategyId { get; set; }

    /// <summary>
    /// Optional strategy version identifier if this transaction was created by a specific strategy version.
    /// </summary>
    public Guid? StrategyVersionId { get; set; }

    /// <summary>
    /// Optional backtest identifier if this transaction was created during a backtest run.
    /// </summary>
    public Guid? BacktestId { get; set; }

    public OrderType Type { get; set; }

    public decimal TotalCost { get; set; }

    public DateTime? TransactionDate { get; set; }

    public static TransactionsTable FromTransaction(TransactionObject transaction, string? strategyId, string? screenerId, Guid? strategyVersionId, Guid? backtestId = null)
    {
        return new TransactionsTable
        {
            Id = Guid.NewGuid(),
            Created = transaction.Created,
            Currency = transaction.Currency,
            PartitionKey = transaction.StockId.ToString(),
            OriginalTransactionId = transaction.OriginalTransactionId,
            Price = transaction.Price,
            Quantity = transaction.Quantity,
            QuantityRemaining = transaction.Quantity,
            ScreenerId = screenerId,
            StockId = transaction.StockId,
            StrategyId = strategyId,
            StrategyVersionId = strategyVersionId,
            BacktestId = backtestId,
            Type = transaction.Type,
            TotalCost = transaction.TotalCost,
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
            Price = Price,
            Quantity = Quantity,
            QuantityRemaining = QuantityRemaining ?? Quantity,
            ScreenerId = ScreenerId,
            StrategyId = StrategyId,
            TotalCost = TotalCost,
            TransactionDate = TransactionDate ?? Created,
            Type = Type,
        };
    }

    public TransactionResponse ToTransactionResponse(TransactionObject transaction)
    {
        var response = ToTransactionResponse();
        response.Symbol = transaction.Symbol;
        return response;
    }

    public TransactionResponse ToTransactionResponse(UpdateTransactionRequestBody updateRequest)
    {
        var response = ToTransactionResponse();
        response.Symbol = updateRequest.Symbol;
        return response;
    }
}
