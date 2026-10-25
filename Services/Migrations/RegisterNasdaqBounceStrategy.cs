using Microsoft.EntityFrameworkCore;
using Services.Database;
using Services.Strategies.NasdaqBounce;
using Shared.Interfaces;

namespace Services.Migrations;

public sealed class RegisterNasdaqBounceStrategy(DatabaseContext db) : IMigration
{
    public string MigrationName => "2026-10-24-register-nasdaq-bounce-overnight";
    public DateTime CreatedOn => new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

    public async Task ApplyAsync()
    {
        if (!await db.Strategies.AnyAsync(x => x.PartitionKey == NasdaqBounceRules.StrategyId))
        {
            db.Strategies.Add(new StrategyTable
            {
                Id = NasdaqBounceRules.Id(NasdaqBounceRules.StrategyId),
                PartitionKey = NasdaqBounceRules.StrategyId,
                Name = "QQQ overnight bounce",
                MaxPositionPercentage = 50m,
                Description = "Buy QQQ after a sharp decline before close; exit on intraday support break or next-session deadline."
            });
        }

        if (!await db.StrategyVersions.AnyAsync(
            x => x.StrategyId == NasdaqBounceRules.StrategyId && x.Version == NasdaqBounceRules.Version))
        {
            db.StrategyVersions.Add(new StrategyVersionTable
            {
                Id = NasdaqBounceRules.Id($"{NasdaqBounceRules.StrategyId}:{NasdaqBounceRules.Version}"),
                PartitionKey = NasdaqBounceRules.StrategyId,
                StrategyId = NasdaqBounceRules.StrategyId,
                Version = NasdaqBounceRules.Version,
                Description = "Initial paper execution strategy. New entries are disabled by default."
            });
        }

        await db.SaveChangesAsync();
    }
}
