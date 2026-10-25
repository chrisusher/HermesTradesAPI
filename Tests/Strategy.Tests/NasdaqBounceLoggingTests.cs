using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Services.Strategies.NasdaqBounce;

namespace Strategy.Tests;

public class NasdaqBounceLoggingTests
{
    [Test]
    public async Task DisabledEntryExplainsSkipAndCorrelatesInvocation()
    {
        var fixture = new Fixture();
        var logger = new RecordingLogger();
        fixture.Options.EntriesEnabled = false;

        await CreateStrategy(fixture, logger).BuyAsync();

        var skipped = logger.Entries.Single(entry => Equals(entry.Properties.GetValueOrDefault("Reason"), "EntriesDisabled"));
        var invocationId = skipped.Properties["StrategyInvocationId"];
        Assert.That(logger.Entries.All(entry => Equals(entry.Properties["StrategyInvocationId"], invocationId)), Is.True);
        Assert.That(skipped.Properties["StrategyId"], Is.EqualTo(NasdaqBounceRules.StrategyId));
        Assert.That(skipped.Properties["StrategyOperation"], Is.EqualTo("Buy"));
        Assert.That(logger.Entries.First().Message, Does.Contain(invocationId!.ToString()));
        Assert.That(logger.Entries.Last().Message, Does.Contain(invocationId.ToString()));
        Assert.That(logger.Entries.Last().Properties, Contains.Key("ElapsedMilliseconds"));
        Assert.That(fixture.Store.Trades, Is.Empty);
    }

    [Test]
    public async Task EntryRecoveryLogsCommittedFailureAndSuccessfulAcknowledgement()
    {
        var fixture = new Fixture();
        var logger = new RecordingLogger();
        var strategy = CreateStrategy(fixture, logger);
        fixture.Store.FailAfterApply = true;

        await strategy.BuyAsync();

        var run = fixture.Store.AllRuns.Single();
        var failure = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        Assert.That(failure.Exception, Is.TypeOf<IOException>());
        Assert.That(failure.Message, Does.Contain(run.Portfolio.Id.ToString()));
        Assert.That(run.EntryApplied, Is.False);

        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        await strategy.SellAsync();

        var recovery = logger.Entries.Single(entry => entry.Message.StartsWith("Resuming entry intent"));
        var completed = logger.Entries.Single(entry => entry.Message.StartsWith("Entry completed"));
        Assert.That(recovery.Message, Does.Contain(run.Id.ToString()));
        Assert.That(completed.Message, Does.Contain(run.Portfolio.Id.ToString()));
        Assert.That(completed.Properties["TransactionId"], Is.EqualTo(run.Entry.TransactionId));
        Assert.That(completed.Properties["RunId"], Is.EqualTo(run.Id));
        Assert.That(fixture.Store.AllRuns.Single().EntryApplied, Is.True);
        Assert.That(fixture.Store.Trades, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReviewFailureAndForcedExitExplainDecisionAndRealisedResult()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        var logger = new RecordingLogger();
        var strategy = CreateStrategy(fixture, logger);
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T14:00:00Z");
        fixture.Analyser.Fail = true;

        await strategy.SellAsync();

        var hold = logger.Entries.Single(entry => entry.Message.StartsWith("Hold decision persisted"));
        Assert.That(hold.Properties["ReviewFailure"], Is.EqualTo(nameof(HttpRequestException)));
        Assert.That(hold.Properties, Contains.Key("ReviewSlot"));
        Assert.That(hold.Properties, Contains.Key("ExitDeadline"));
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.False);

        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        fixture.Market.Price = 90;
        await strategy.SellAsync();

        Assert.That(logger.Entries.Any(entry => Equals(entry.Properties.GetValueOrDefault("Reason"), "ScheduledExitDeadlineReached")), Is.True);
        var completed = logger.Entries.Single(entry => entry.Message.StartsWith("Exit completed"));
        Assert.That(completed.Properties["RealisedProfitLoss"], Is.LessThan(0m));
        Assert.That(completed.Properties["PortfolioCurrency"], Is.EqualTo(CurrencyCode.USD));
        Assert.That(completed.Message, Does.Contain(fixture.Store.AllRuns.Single().Id.ToString()));
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
    }

    [Test]
    public async Task UnhandledFailureIsLoggedWithExceptionAndRethrown()
    {
        var fixture = new Fixture();
        var logger = new RecordingLogger();
        fixture.Market.FailCalendar = true;

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => CreateStrategy(fixture, logger).BuyAsync());

        var failure = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        Assert.That(failure.Exception, Is.SameAs(exception));
        Assert.That(failure.Properties, Contains.Key("StrategyInvocationId"));
        Assert.That(logger.Entries.Any(entry => entry.Message.Contains("finished after")), Is.False);
    }

    [Test]
    public async Task CancellationIsLoggedAndRethrownWithoutApplyingTrade()
    {
        var fixture = new Fixture();
        var logger = new RecordingLogger();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateStrategy(fixture, logger).BuyAsync(cancellation.Token));

        var cancelled = logger.Entries.Single(entry => entry.Message.Contains("tick") && entry.Level == LogLevel.Warning);
        Assert.That(cancelled.Properties["CancellationRequested"], Is.True);
        Assert.That(logger.Entries.Any(entry => entry.Level == LogLevel.Error), Is.False);
        Assert.That(fixture.Store.Trades, Is.Empty);
    }

    private static NasdaqBounceStrategy CreateStrategy(Fixture fixture, ILogger<NasdaqBounceStrategy> logger) =>
        new(
            fixture.Store,
            fixture.Market,
            new NasdaqBounceCalendar(fixture.Market),
            fixture.Analyser,
            fixture.Locks,
            fixture.Slack,
            fixture.Options,
            fixture.Clock,
            logger);

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> Properties);

    private sealed class RecordingLogger : ILogger<NasdaqBounceStrategy>
    {
        private readonly LoggerExternalScopeProvider _scopes = new();
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>();
            _scopes.ForEachScope((scope, collected) => AddProperties(scope, collected), properties);
            AddProperties(state, properties);
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }

        private static void AddProperties(object? state, Dictionary<string, object?> properties)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var property in values)
                {
                    properties[property.Key] = property.Value;
                }
            }
        }
    }
}
