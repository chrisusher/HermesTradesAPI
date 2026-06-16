namespace Shared.DTOs.Transactions;

public class TransactionResponse : TransactionObject
{
    [JsonPropertyName("transactionId")]
    public Guid TransactionId { get; set; }

    [JsonPropertyName("profitLoss")]
    public decimal? ProfitLoss { get; set; }

    [JsonPropertyName("screenerId")]
    public string? ScreenerId { get; set; }

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }
}
