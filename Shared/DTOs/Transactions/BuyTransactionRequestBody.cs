namespace Shared.DTOs.Transactions;

public class BuyTransactionRequestBody : TransactionRequestBody
{
    [JsonPropertyName("type")]
    public OrderType Type { get; set; } = OrderType.MARKET_BUY;
}
