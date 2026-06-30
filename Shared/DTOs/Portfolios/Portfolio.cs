namespace Shared.DTOs.Portfolios;

public class Portfolio : PortfolioSummary
{
    [JsonPropertyName("stocks")]
    public new List<PortfolioHolding> Stocks { get; set; } = [];

    public static Portfolio FromPortfolioSummaryDto(PortfolioSummary summary)
    {
        return new Portfolio
        {
            PortfolioId = summary.PortfolioId,
            UserId = summary.UserId,
            Name = summary.Name,
            AlwaysInvest = summary.AlwaysInvest,
            Currency = summary.Currency,
            Description = summary.Description,
            Created = summary.Created,
            FreeCash = summary.FreeCash,
            PortfolioType = summary.PortfolioType,
            StartingBalance = summary.StartingBalance,
            Status = summary.Status,
            StrategyId = summary.StrategyId,
            Updated = summary.Updated,
        };
    }
}
