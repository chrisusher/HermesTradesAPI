namespace Shared.DTOs.Portfolios;

public class CreatePortfolioRequest : Entity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("alwaysInvest")]
    public bool AlwaysInvest { get; set; }

    [JsonPropertyName("currency")]
    public CurrencyCode? Currency { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("freeCash")]
    public decimal? FreeCash { get; set; }

    [JsonPropertyName("strategyId")]
    public string? StrategyId { get; set; }
}
