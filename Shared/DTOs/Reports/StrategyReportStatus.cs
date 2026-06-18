namespace Shared.DTOs.Reports;

/// <summary>
/// Lightweight status metadata embedded in report JSON to avoid a separate status blob.
/// </summary>
public class StrategyReportStatus
{
    [JsonPropertyName("status")]
    public ProgressType State { get; set; } = ProgressType.Completed; // e.g., InProgress, Completed

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }

    [JsonPropertyName("backtestId")]
    public Guid? BacktestId { get; set; }

    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; set; } = true;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}
