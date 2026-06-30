using Shared.Database;
using Shared.DTOs.Portfolios;

namespace Services.Database;

public class PortfolioTable : CosmosTable
{
    public string Name { get; set; } = string.Empty;

    public bool AlwaysInvest { get; set; } = false;

    public CurrencyCode Currency { get; set; }

    public PortfolioType PortfolioType { get; set; } = PortfolioType.Live;

    public string? Description { get; set; }

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
            Name = Name,
            AlwaysInvest = AlwaysInvest,
            Currency = Currency,
            Description = Description,
            Created = Created,
            FreeCash = FreeCash,
            PortfolioType = PortfolioType,
            StartingBalance = StartingBalance,
            Status = Status,
            StrategyId = StrategyId,
            Updated = Updated,
        };
    }

    public PortfolioSummary ToPortfolioSummaryDto()
    {
        return new PortfolioSummary
        {
            PortfolioId = Id,
            UserId = Guid.Parse(PartitionKey),
            Name = Name,
            AlwaysInvest = AlwaysInvest,
            Currency = Currency,
            Description = Description,
            Created = Created,
            FreeCash = FreeCash,
            PortfolioType = PortfolioType,
            StartingBalance = StartingBalance,
            Status = Status,
            StrategyId = StrategyId,
            Updated = Updated,
        };
    }
}
