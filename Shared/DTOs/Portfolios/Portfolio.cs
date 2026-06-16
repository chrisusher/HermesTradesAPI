namespace Shared.DTOs.Portfolios;

public class Portfolio : Entity
{
    [JsonPropertyName("backtestId")]
    public Guid? BacktestId { get; set; }

    [JsonPropertyName("portfolioId")]
    public Guid PortfolioId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("strategyId")]
    public string StrategyId { get; set; } = string.Empty;

    [JsonPropertyName("stocks")]
    public List<PortfolioHolding> Stocks { get; set; } = [];

    [JsonPropertyName("freeCash")]
    public decimal? FreeCash { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }
}
