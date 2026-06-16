namespace Shared.Database;

public class CosmosTable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string PartitionKey { get; set; } = string.Empty;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime? Updated { get; set; }

    public StatusType Status { get; set; } = StatusType.Active;
}
