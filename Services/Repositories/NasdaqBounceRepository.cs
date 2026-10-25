using Microsoft.EntityFrameworkCore;
using Services.Database;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Services.Repositories;

public sealed class NasdaqBounceRepository(IDbContextFactory<DatabaseContext> factory) : INasdaqBounceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions() => new(SharedCommon.JsonOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<List<NasdaqBounceRun>> GetPendingRunsAsync(CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var executions = await database.StrategyExecutions
            .AsNoTracking()
            .Where(execution => execution.Kind == "Position" && (!execution.Complete || execution.PendingAlerts))
            .ToListAsync(cancellationToken);

        return executions.Select(execution => JsonSerializer.Deserialize<NasdaqBounceRun>(execution.Payload, JsonOptions)!).ToList();
    }

    public async Task<List<NasdaqBounceRun>> GetPortfolioRunsAsync(Guid portfolioId, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var executions = await database.StrategyExecutions
            .AsNoTracking()
            .Where(execution => execution.PartitionKey == portfolioId.ToString() && execution.Kind == "Position")
            .ToListAsync(cancellationToken);

        return executions.Select(execution => JsonSerializer.Deserialize<NasdaqBounceRun>(execution.Payload, JsonOptions)!).ToList();
    }

    public Task SaveRunAsync(NasdaqBounceRun run, CancellationToken cancellationToken) =>
        SaveAsync(
            run.Id,
            run.Portfolio.Id.ToString(),
            "Position",
            run.Closed,
            run.Alerts.Any(alert => !alert.Sent),
            JsonSerializer.Serialize(run, JsonOptions),
            cancellationToken);

    public async Task<NasdaqBounceReview?> GetReviewAsync(DateTimeOffset slot, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var id = CreateId($"review:{slot:O}");
        var execution = await database.StrategyExecutions
            .AsNoTracking()
            .SingleOrDefaultAsync(execution => execution.Id == id && execution.PartitionKey == "nasdaq-bounce-reviews", cancellationToken);

        return execution is null ? null : JsonSerializer.Deserialize<NasdaqBounceReview>(execution.Payload, JsonOptions);
    }

    public Task SaveReviewAsync(NasdaqBounceReview review, CancellationToken cancellationToken) =>
        SaveAsync(
            CreateId($"review:{review.Slot:O}"),
            "nasdaq-bounce-reviews",
            "Review",
            true,
            false,
            JsonSerializer.Serialize(review, JsonOptions),
            cancellationToken);

    public async Task<NasdaqBounceEntrySignal?> GetEntrySignalAsync(DateOnly date, CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var id = CreateId($"signal:{date:yyyy-MM-dd}");
        var execution = await database.StrategyExecutions
            .AsNoTracking()
            .SingleOrDefaultAsync(execution => execution.Id == id && execution.PartitionKey == "nasdaq-bounce-entry-signals", cancellationToken);

        return execution is null ? null : JsonSerializer.Deserialize<NasdaqBounceEntrySignal>(execution.Payload, JsonOptions);
    }

    public Task SaveEntrySignalAsync(NasdaqBounceEntrySignal signal, CancellationToken cancellationToken) => SaveAsync(
        CreateId($"signal:{signal.Date:yyyy-MM-dd}"),
        "nasdaq-bounce-entry-signals",
        "EntrySignal",
        true,
        false,
        JsonSerializer.Serialize(signal, JsonOptions),
        cancellationToken);

    private async Task SaveAsync(
        Guid id,
        string partitionKey,
        string kind,
        bool complete,
        bool pendingAlerts,
        string payload,
        CancellationToken cancellationToken)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var execution = await database.StrategyExecutions.SingleOrDefaultAsync(
            execution => execution.Id == id && execution.PartitionKey == partitionKey,
            cancellationToken);

        if (execution is null)
        {
            execution = new StrategyExecutionTable
            {
                Id = id,
                PartitionKey = partitionKey,
                Kind = kind
            };
            database.StrategyExecutions.Add(execution);
        }

        execution.Payload = payload;
        execution.Complete = complete;
        execution.PendingAlerts = pendingAlerts;
        execution.Updated = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    private static Guid CreateId(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
}
