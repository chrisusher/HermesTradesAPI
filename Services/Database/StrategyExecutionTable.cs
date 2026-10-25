using Shared.Database;

namespace Services.Database;

public sealed class StrategyExecutionTable : CosmosTable
{
    public string Kind { get; set; } = "Position";

    public bool Complete { get; set; }

    public bool PendingAlerts { get; set; }

    public string Payload { get; set; } = "";
}
