using Shared.Database;

namespace Services.Database;

public class PortfolioHistoryTable : CosmosTable
{
    public Guid PortfolioHistoryId { get; set; }

    public DateTime CurrentDate { get; set; } = DateTime.UtcNow;

    public decimal CurrentValue { get; set; }
}
