using Shared.Database;
using Shared.DTOs.Strategies;

namespace Services.Database;

public class StrategyTable : CosmosTable
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool AlwaysInvest { get; set; } = false;

    public decimal? StartingBalance { get; set; }

    public decimal? MaxPositionPercentage { get; set; }

    public StrategyConfig Config { get; set; } = new StrategyConfig();

    public static StrategyTable FromStrategy(Strategy strategy)
    {
        return new StrategyTable
        {
            Id = Guid.NewGuid(),
            PartitionKey = strategy.StrategyId,
            Name = strategy.Name,
            AlwaysInvest = strategy.AlwaysInvest,
            Config = strategy.Config,
            Created = strategy.Created,
            Description = strategy.Description,
            MaxPositionPercentage = strategy.MaxPositionPercentage,
            StartingBalance = strategy.StartingBalance,
            Status = strategy.Status,
            Updated = DateTime.UtcNow,
        };
    }

    public Strategy ToStrategy()
    {
        return new Strategy
        {
            StrategyId = PartitionKey,
            Name = Name,
            AlwaysInvest = AlwaysInvest,
            Config = Config,
            Created = Created,
            Description = Description,
            MaxPositionPercentage = MaxPositionPercentage,
            StartingBalance = StartingBalance,
            Status = Status,
            Updated = Updated,
        };
    }

    public StrategySummary ToSummary()
    {
        return new StrategySummary
        {
            StrategyId = PartitionKey,
            Name = Name,
            Description = Description,
        };
    }
}