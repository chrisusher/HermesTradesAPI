namespace Shared.DTOs.Reports.Portfolio;

public class PortfolioROIReport : StrategyReport
{
    [JsonPropertyName("portfolioName")]
    public string PortfolioName { get; set; } = string.Empty!;

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("profitLoss")]
    public decimal ProfitLoss { get; set; }

    [JsonPropertyName("profitLossPercentage")]
    public decimal ProfitLossPercentage { get; set; }

    [JsonPropertyName("totalInvested")]
    public decimal TotalInvested { get; set; }
}
