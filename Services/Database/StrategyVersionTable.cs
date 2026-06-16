using Shared.Database;
using Shared.DTOs.Strategies;

namespace Services.Database;

/// <summary>
/// Entity for storing strategy versions in Cosmos DB.
/// </summary>
public class StrategyVersionTable : CosmosTable
{
    /// <summary>
    /// The strategy this version belongs to.
    /// </summary>
    public required string StrategyId { get; set; }

    /// <summary>
    /// Version string (e.g., "1.0.0", "v2", etc.).
    /// </summary>
    public required string Version { get; set; }

    /// <summary>
    /// Optional description of what changed in this version.
    /// </summary>
    public string? Description { get; set; }

    public static StrategyVersionTable FromStrategyVersion(StrategyVersion strategyVersion)
    {
        return new StrategyVersionTable
        {
            Id = strategyVersion.StrategyVersionId,
            PartitionKey = strategyVersion.StrategyId,
            StrategyId = strategyVersion.StrategyId,
            Version = strategyVersion.Version,
            Description = strategyVersion.Description,
            Created = strategyVersion.Created,
            Status = strategyVersion.Status,
            Updated = DateTime.UtcNow,
        };
    }

    public StrategyVersion ToStrategyVersion()
    {
        return new StrategyVersion
        {
            StrategyVersionId = Id,
            StrategyId = StrategyId,
            Version = Version,
            Description = Description,
            Created = Created,
            Status = Status,
            Updated = Updated,
        };
    }
}
