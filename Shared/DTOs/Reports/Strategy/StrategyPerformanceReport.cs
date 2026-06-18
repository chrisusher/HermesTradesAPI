using Shared.DTOs.Reports.Performance;

namespace Shared.DTOs.Reports.Strategy;

public class StrategyPerformanceReport : StrategyReport
{   
    [JsonPropertyName("strategyName")]
    public string StrategyName { get; set; } = string.Empty!;

    [JsonPropertyName("lastWeek")]
    public PeriodPerformance LastWeekPerformance { get; set; } = new();

    [JsonPropertyName("lastMonth")]
    public PeriodPerformance LastMonthPerformance { get; set; } = new();

    [JsonPropertyName("last3Months")]
    public PeriodPerformance Last3MonthsPerformance { get; set; } = new();

    [JsonPropertyName("last6Months")]
    public PeriodPerformance Last6MonthsPerformance { get; set; } = new();

    [JsonPropertyName("lastYear")]
    public PeriodPerformance LastYearPerformance { get; set; } = new();

    [JsonPropertyName("totalProfitLoss")]
    public decimal TotalProfitLoss { get; set; }
}
