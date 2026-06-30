namespace Shared.DTOs.Portfolios;

public class PortfolioSummary : CreatePortfolioRequest
{
    [JsonPropertyName("portfolioId")]
    public Guid PortfolioId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("startingBalance")]
    public decimal? StartingBalance { get; set; }

    [JsonPropertyName("stocks")]
    public List<PortfolioHoldingSummary> Stocks { get; set; } = [];

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
}
