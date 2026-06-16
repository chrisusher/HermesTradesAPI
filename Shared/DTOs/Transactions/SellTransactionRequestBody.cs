namespace Shared.DTOs.Transactions;

public class SellTransactionRequestBody : TransactionRequestBody
{
    [JsonPropertyName("originalTransactionId")]
    public Guid? OriginalTransactionId { get; set; }

    [JsonPropertyName("type")]
    public OrderType Type { get; set; } = OrderType.MARKET_SELL;
}
