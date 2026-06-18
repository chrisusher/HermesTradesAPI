using Shared.Database;
using Shared.DTOs.Portfolios;

namespace Services.Database;

public class PortfolioTable : CosmosTable
{
    public bool AlwaysInvest { get; set; } = false;

    public decimal? FreeCash { get; set; }

    public decimal? StartingBalance { get; set; }

    public string StrategyId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public Portfolio ToPortfolioDto()
    {
        return new Portfolio
        {
            PortfolioId = Id,
            UserId = Guid.Parse(PartitionKey),
            Created = Created,
            FreeCash = FreeCash,
            Status = Status,
            StrategyId = StrategyId,
            Updated = Updated,
        };
    }
}
