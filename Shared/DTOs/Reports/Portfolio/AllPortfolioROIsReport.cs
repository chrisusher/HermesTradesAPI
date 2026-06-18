namespace Shared.DTOs.Reports.Portfolio;

public class AllPortfolioROIsReport : StrategyReport
{
    [JsonPropertyName("updatedPortfolioCount")]
    public int UpdatedPortfolioCount { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
}
