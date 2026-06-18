namespace Shared.DTOs.Portfolios;

/// <summary>
/// Summary of holdings for a specific stock in a strategy portfolio.
/// </summary>
public class PortfolioHoldingResponse : PortfolioHolding
{
    private decimal _profitLossPercent = 0;

    /// <summary>
    /// Profit/loss from selling this position. In `PortfolioHoldingResponse` this is required.
    /// This overrides the nullable base property so serializers see a single `profitLoss` member.
    /// </summary>
    public override required decimal? ProfitLoss { get; set; }
    /// <summary>
    /// Realized profit/loss percentage based on original investment. Only set for closed positions.
    /// </summary>
    [JsonPropertyName("profitLossPercent")]
    public decimal ProfitLossPercent
    {
        get
        {
            if (_profitLossPercent != 0)
            {
                return _profitLossPercent;
            }

            var profit = ProfitLoss ?? 0m;
            if (profit != 0 && TotalInvested > 0)
            {
                _profitLossPercent = Math.Round(profit / TotalInvested * 100, 2);
            }
            return _profitLossPercent;
        }
    }
}