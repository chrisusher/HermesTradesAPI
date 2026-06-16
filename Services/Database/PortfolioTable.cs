using Shared.Database;
using Shared.DTOs.Portfolios;

namespace Services.Database;

public class PortfolioTable : CosmosTable
{
    public bool AlwaysInvest { get; set; } = false;

    public decimal? FreeCash { get; set; }

    public decimal? StartingBalance { get; set; }

    public Guid? BacktestId { get; set; }

    public Guid UserId { get; set; }

    public Portfolio ToPortfolioDto()
    {
        return new Portfolio
        {
            PortfolioId = Id,
            BacktestId = BacktestId,
            StrategyId = PartitionKey,
            Created = Created,
            FreeCash = FreeCash,
            Status = Status,
            Updated = Updated,
            UserId = UserId,
        };
    }
}
