using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Strategies.NasdaqBounce;
using Shared.Calendar;
using Shared.Config;
using Shared.DTOs.Yahoo;
using Shared.Interfaces;

namespace Strategy.Tests;

internal sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-22T19:30:00Z");

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeMarket : INasdaqBounceMarketData
{
    public Guid StockId { get; } = Guid.NewGuid();
    public decimal Price { get; set; } = 98;
    public decimal PriorClose { get; set; } = 100;
    public bool IsOpen { get; set; } = true;
    public bool FailSnapshot { get; set; }
    public bool FailCalendar { get; set; }
    public List<CloseDate> Closures { get; } = [];
    public Dictionary<CurrencyCode, decimal> Rates { get; } = new()
    {
        [CurrencyCode.USD] = 1,
        [CurrencyCode.GBP] = .8m
    };
    public int Snapshots { get; private set; }

    public Task<List<CloseDate>> GetClosuresAsync(int year, CancellationToken ct) =>
        FailCalendar
            ? throw new HttpRequestException("Calendar unavailable")
            : Task.FromResult(Closures.Where(x => x.Date.Year == year).ToList());

    public Task<bool> IsOpenAsync(CancellationToken ct) => Task.FromResult(IsOpen);

    public Task<decimal> GetUsdRateAsync(CurrencyCode currency, CancellationToken ct) => Task.FromResult(Rates[currency]);

    public Task<NasdaqBounceMarketSnapshot> GetSnapshotAsync(
        NasdaqBounceSession session,
        DateOnly previous,
        DateTimeOffset now,
        CancellationToken ct)
    {
        Snapshots++;

        if (FailSnapshot)
        {
            throw new HttpRequestException("No candles");
        }
        var candles = new List<PeriodCandle>
        {
            new()
            {
                Date = session.Open.UtcDateTime,
                Open = 100,
                High = 100,
                Low = 99,
                Close = 100
            },
            new()
            {
                Date = session.Open.AddMinutes(15).UtcDateTime,
                Open = 100,
                High = 100,
                Low = 99,
                Close = 99
            }
        };

        return Task.FromResult(new NasdaqBounceMarketSnapshot(
            StockId,
            "NASDAQ",
            PriorClose,
            Price,
            now.AddMinutes(-1),
            candles));
    }
}

internal sealed class FakeAnalyser : INasdaqBounceSupportAnalyser
{
    public SupportAction Action { get; set; } = SupportAction.Hold;
    public bool Fail { get; set; }
    public int Calls { get; private set; }

    public Task<SupportDecision> AnalyseAsync(
        NasdaqBounceMarketSnapshot snapshot,
        NasdaqBounceSession session,
        DateTimeOffset now,
        CancellationToken ct)
    {
        Calls++;

        if (Fail)
        {
            throw new HttpRequestException("AI unavailable");
        }

        return Task.FromResult(new SupportDecision(Action, "Test support decision", []));
    }
}

internal sealed class FakeLocks : IStrategyLock
{
    private readonly ConcurrentDictionary<string, byte> _held = new();

    public Task<IStrategyLease?> TryAcquireAsync(string key, CancellationToken ct)
    {
        IStrategyLease? lease = _held.TryAdd(key, 0)
            ? new Lease(_held, key, ct)
            : null;

        return Task.FromResult(lease);
    }

    private sealed class Lease(ConcurrentDictionary<string, byte> held, string key, CancellationToken ct) : IStrategyLease
    {
        public CancellationToken Token => ct;

        public ValueTask DisposeAsync()
        {
            held.TryRemove(key, out _);

            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class FakeNotifications : INotificationClient
{
    public bool Fail { get; set; }
    public List<(string Subject, string Message)> Messages { get; } = [];

    public Task<bool> SendTradeNotificationAsync(string subject, string message, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new HttpRequestException("Slack unavailable");
        }
        Messages.Add((subject, message));

        return Task.FromResult(true);
    }

    public Task<bool> SendScreenerNotificationAsync(string symbol, string subject, string message)
    {
        throw new NotSupportedException();
    }

    public Task<bool> SendSummaryNotificationAsync(string name, string subject, string message)
    {
        throw new NotSupportedException();
    }
}

internal sealed class FakeStore : INasdaqBounceStore
{
    public Dictionary<Guid, NasdaqBouncePortfolio> Portfolios { get; } = [];
    public Dictionary<Guid, string> Runs { get; } = [];
    public Dictionary<DateTimeOffset, NasdaqBounceReview> Reviews { get; } = [];
    public Dictionary<Guid, StrategyTrade> Trades { get; } = [];
    public Dictionary<DateOnly, NasdaqBounceEntrySignal> EntrySignals { get; } = [];
    public Func<StrategyTrade, Task>? BeforeApply { get; set; }
    public bool Associated { get; set; } = true;
    public bool FailAfterApply { get; set; }
    public decimal? Remaining { get; set; }

    private static NasdaqBounceRun Read(string json) => JsonSerializer.Deserialize<NasdaqBounceRun>(json)!;
    public List<NasdaqBounceRun> AllRuns => Runs.Values.Select(Read).ToList();

    public Task<List<NasdaqBouncePortfolio>> GetEntryPortfoliosAsync(CancellationToken ct) =>
        Task.FromResult(Associated ? Portfolios.Values.ToList() : []);

    public Task<List<NasdaqBounceRun>> GetPendingRunsAsync(CancellationToken ct) =>
        Task.FromResult(AllRuns.Where(x => !x.Closed || x.Alerts.Any(a => !a.Sent)).ToList());

    public Task<List<NasdaqBounceRun>> GetPortfolioRunsAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(AllRuns.Where(x => x.Portfolio.Id == id).ToList());

    public Task<NasdaqBouncePortfolio> GetPortfolioAsync(Guid user, Guid id, CancellationToken ct) =>
        Task.FromResult(Portfolios[id]);

    public Task SaveRunAsync(NasdaqBounceRun run, CancellationToken ct)
    {
        Runs[run.Id] = JsonSerializer.Serialize(run);

        return Task.CompletedTask;
    }

    public Task<NasdaqBounceReview?> GetReviewAsync(DateTimeOffset slot, CancellationToken ct) =>
        Task.FromResult(Reviews.GetValueOrDefault(slot));

    public Task SaveReviewAsync(NasdaqBounceReview review, CancellationToken ct)
    {
        Reviews[review.Slot] = review;

        return Task.CompletedTask;
    }

    public Task<NasdaqBounceEntrySignal?> GetEntrySignalAsync(DateOnly date, CancellationToken ct) =>
        Task.FromResult(EntrySignals.GetValueOrDefault(date));

    public Task SaveEntrySignalAsync(NasdaqBounceEntrySignal signal, CancellationToken ct)
    {
        EntrySignals[signal.Date] = signal;

        return Task.CompletedTask;
    }

    public Task<decimal> GetRemainingQuantityAsync(NasdaqBounceRun run, CancellationToken ct) =>
        Task.FromResult(Remaining ?? run.Entry.Quantity);

    public Task<bool> HasTradeStartedAsync(StrategyTrade trade, CancellationToken ct) =>
        Task.FromResult(Trades.ContainsKey(trade.TransactionId));

    public async Task ApplyTradeAsync(StrategyTrade trade, CancellationToken ct)
    {
        if (BeforeApply is not null)
        {
            await BeforeApply(trade);
        }

        if (Trades.TryAdd(trade.TransactionId, trade))
        {
            var portfolio = Portfolios[trade.PortfolioId];
            Portfolios[portfolio.Id] = portfolio with
            {
                Cash = portfolio.Cash - trade.PortfolioAmount
            };
        }

        if (FailAfterApply)
        {
            FailAfterApply = false;
            // Simulate a worker failure after the transaction has been applied.
            throw new IOException("Simulated crash after commit");
        }
    }
}

internal sealed class Fixture
{
    public FakeClock Clock { get; } = new();
    public FakeStore Store { get; } = new();
    public FakeMarket Market { get; } = new();
    public FakeAnalyser Analyser { get; } = new();
    public FakeLocks Locks { get; } = new();
    public FakeNotifications Slack { get; } = new();
    public NasdaqBounceOptions Options { get; } = new()
    {
        EntriesEnabled = true
    };
    public NasdaqBouncePortfolio Portfolio { get; }

    public Fixture()
    {
        Portfolio = new NasdaqBouncePortfolio(Guid.NewGuid(), Guid.NewGuid(), "Synthetic", 1000, CurrencyCode.USD, CurrencyCode.USD);
        Store.Portfolios[Portfolio.Id] = Portfolio;
    }

    public NasdaqBounceStrategy Strategy() => new(
        Store,
        Market,
        new NasdaqBounceCalendar(Market),
        Analyser,
        Locks,
        Slack,
        Options,
        Clock,
        NullLogger<NasdaqBounceStrategy>.Instance);
}
