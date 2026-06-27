namespace Shared.DTOs.Portfolios;

public class Portfolio : CreatePortfolioRequest
{
    [JsonPropertyName("portfolioId")]
    public Guid PortfolioId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("stocks")]
    public List<PortfolioHolding> Stocks { get; set; } = [];

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
}
