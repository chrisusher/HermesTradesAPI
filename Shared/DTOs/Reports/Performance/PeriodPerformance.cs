namespace Shared.DTOs.Reports.Performance;

public class PeriodPerformance
{
    [JsonPropertyName("totalTrades")]
    public int TotalTrades { get; set; }

    [JsonPropertyName("winningTrades")]
    public int WinningTrades { get; set; }

    [JsonPropertyName("winningPercent")]
    public double WinningPercent { get; set; }

    [JsonPropertyName("profitLoss")]
    public decimal ProfitLoss { get; set; }
}
