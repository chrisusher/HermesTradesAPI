using Shared.DTOs.Portfolios;

namespace Shared.DTOs.Reports.Strategy;

/// <summary>
/// ROI performance report for a strategy showing return on investment metrics.
/// </summary>
public class StrategyROIReport : StrategyReport
{
    [JsonPropertyName("strategyName")]
    public string StrategyName { get; set; } = string.Empty!;

    /// <summary>
    /// Annualised return percentage (CAGR) based on the period duration.
    /// </summary>
    [JsonPropertyName("annualisedReturnPercent")]
    public decimal? AnnualisedReturnPercent { get; set; }

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue => Math.Round(Holdings.Sum(h => h.CurrentValue), 2);

    [JsonPropertyName("freeCash")]
    public decimal FreeCash { get; set; }

    [JsonPropertyName("holdings")]
    public List<PortfolioHoldingResponse> Holdings { get; set; } = new();

    [JsonPropertyName("periodEnd")]
    public DateTime? PeriodEnd { get; set; }

    [JsonPropertyName("periodStart")]
    public DateTime? PeriodStart { get; set; }

    [JsonPropertyName("startingBalance")]
    public decimal StartingBalance { get; set; }

    [JsonPropertyName("status")]
    public StrategyReportStatus Status { get; set; } = new();

    [JsonPropertyName("totalInvested")]
    public decimal TotalInvested => Math.Round(Holdings.Sum(h => h.TotalInvested), 2);

    [JsonPropertyName("totalSales")]
    public decimal TotalSales => Math.Round(Holdings.Sum(h => h.SaleAmount ?? 0), 2);

    [JsonPropertyName("totalPortfolioValue")]
    public decimal TotalPortfolioValue => CurrentValue + FreeCash;

    [JsonPropertyName("totalReturn")]
    public decimal TotalReturn => Math.Round(Holdings.Sum(h => h.ProfitLoss ?? 0), 2);

    /// <summary>
    /// Total return as a percentage of the starting balance.
    /// Uses starting balance as the denominator so recycled capital does not inflate the figure.
    /// </summary>
    [JsonPropertyName("totalReturnPercent")]
    public decimal TotalReturnPercent
    {
        get
        {
            if (StartingBalance == 0)
            {
                if (TotalInvested == 0)
                {
                    return 0;
                }

                return Math.Round(TotalReturn / TotalInvested * 100, 2);
            }
            return Math.Round(TotalReturn / StartingBalance * 100, 2);
        }
    }

    /// <summary>
    /// Profit or loss on positions that have been fully closed (realised).
    /// </summary>
    [JsonPropertyName("realisedPnL")]
    public decimal RealisedPnL => Math.Round(Holdings.Where(h => h.IsClosed).Sum(h => h.ProfitLoss ?? 0), 2);

    /// <summary>
    /// Realised P&amp;L as a percentage of the starting balance.
    /// </summary>
    [JsonPropertyName("realisedPnLPercent")]
    public decimal RealisedPnLPercent
    {
        get
        {
            if (StartingBalance == 0)
            {
                return 0;
            }
            return Math.Round(RealisedPnL / StartingBalance * 100, 2);
        }
    }

    /// <summary>
    /// Profit or loss on positions that are still open (unrealised).
    /// </summary>
    [JsonPropertyName("unrealisedPnL")]
    public decimal UnrealisedPnL => Math.Round(Holdings.Where(h => !h.IsClosed).Sum(h => h.ProfitLoss ?? 0), 2);

    /// <summary>
    /// Unrealised P&amp;L as a percentage of the starting balance.
    /// </summary>
    [JsonPropertyName("unrealisedPnLPercent")]
    public decimal UnrealisedPnLPercent
    {
        get
        {
            if (StartingBalance == 0)
            {
                return 0;
            }
            return Math.Round(UnrealisedPnL / StartingBalance * 100, 2);
        }
    }

    [JsonPropertyName("totalTransactions")]
    public int TotalTransactions { get; set; }
}