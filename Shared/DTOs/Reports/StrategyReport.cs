namespace Shared.DTOs.Reports;

public class StrategyReport
{
    [JsonPropertyName("reportName")]
    public string ReportName { get; set; } = string.Empty!;

    [JsonPropertyName("reportDate")]
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
}
