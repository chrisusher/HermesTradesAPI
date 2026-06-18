namespace Shared.DTOs.Portfolios;

public class AddAdhocHoldingRequest
{
    [JsonPropertyName("strategyId")]
    public required string StrategyId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public required string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("exchangeName")]
    public string? ExchangeName { get; set; }

    [JsonPropertyName("purchasePrice")]
    public decimal PurchasePrice { get; set; }

    [JsonPropertyName("shares")]
    public decimal Shares { get; set; }

    [JsonPropertyName("datePurchased")]
    public DateTime DatePurchased { get; set; }

    [JsonPropertyName("totalCostUserCurrency")]
    public decimal TotalCostUserCurrency { get; set; }
}
