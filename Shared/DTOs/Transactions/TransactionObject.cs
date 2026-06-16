namespace Shared.DTOs.Transactions;

public class TransactionObject
{
    [JsonPropertyName("created")]
    public DateTime Created { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("currency")]
    public CurrencyCode Currency { get; set; } = CurrencyCode.Unknown;

    [JsonPropertyName("stockId")]
    public Guid StockId { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public OrderType Type { get; set; }

    [JsonPropertyName("originalTransactionId")]
    public Guid? OriginalTransactionId { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("quantityRemaining")]
    public decimal QuantityRemaining { get; set; }

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("totalCostToUser")]
    public decimal TotalCostToUser { get; set; }

    [JsonPropertyName("transactionDate")]
    public DateTime? TransactionDate { get; set; }
}
