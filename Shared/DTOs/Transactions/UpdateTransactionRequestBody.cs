namespace Shared.DTOs.Transactions;

public class UpdateTransactionRequestBody : SellTransactionRequestBody
{
    [JsonPropertyName("screenerId")]
    public string? ScreenerId { get; set; }

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }
}
