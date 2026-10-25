using NUnit.Framework;
using Services.Strategies.NasdaqBounce;
using Shared.Config;

namespace Strategy.Tests;

public class NasdaqBounceWorkflowTests
{
    [Test]
    public async Task ExpiredUnstartedIntentIsCancelledInsteadOfBuyingLater()
    {
        var fixture = new Fixture();
        fixture.Store.BeforeApply = _ => throw new IOException("Failed before any mutation");
        await fixture.Strategy().BuyAsync();
        fixture.Store.BeforeApply = null;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(5);
        await fixture.Strategy().SellAsync();
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
        Assert.That(fixture.Store.AllRuns.Single().EntryApplied, Is.False);
        Assert.That(fixture.Store.Trades, Is.Empty);
        Assert.That(fixture.Store.Portfolios[fixture.Portfolio.Id].Cash, Is.EqualTo(1000));
    }

    [Test]
    public async Task NoBuySignalIsFrozenForTheEntryWindow()
    {
        var fixture = new Fixture();
        fixture.Market.Price = 100;
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        fixture.Market.Price = 98;
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades, Is.Empty);
        Assert.That(fixture.Market.Snapshots, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentBuyCannotPassAnUnfinishedSale()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Store.BeforeApply = async trade =>
        {

            if (trade.Quantity < 0)
            {
                entered.SetResult();
                await release.Task;
            }
        };
        var sale = fixture.Strategy().SellAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(1));
        release.SetResult();
        await sale;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(3));
    }

    [TestCase(99.01, 0)]
    [TestCase(99, .25)]
    [TestCase(98.51, .25)]
    [TestCase(98.5, .4)]
    [TestCase(98.01, .4)]
    [TestCase(98, .5)]
    [TestCase(95, .5)]
    public async Task InclusiveTiersUseAvailableCash(double price, double allocation)
    {
        var fixture = new Fixture();
        fixture.Market.Price = (decimal)price;
        await fixture.Strategy().BuyAsync();

        if (allocation == 0)
        {
            Assert.That(fixture.Store.Trades, Is.Empty);

            return;
        }
        var run = fixture.Store.AllRuns.Single();
        Assert.Multiple(() =>
        {
            Assert.That(run.Allocation, Is.EqualTo((decimal)allocation));
            Assert.That(run.Entry.PortfolioAmount, Is.LessThanOrEqualTo(1000m * (decimal)allocation));
            Assert.That(run.Entry.Quantity * 1_000_000m, Is.EqualTo(decimal.Truncate(run.Entry.Quantity * 1_000_000m)));
            Assert.That(fixture.Slack.Messages.Single().Subject, Does.Contain("BUY"));
        });
    }

    [Test]
    public async Task DisabledOrClosedMarketDoesNotBuy()
    {
        var fixture = new Fixture();
        Assert.That(new NasdaqBounceOptions().EntriesEnabled, Is.False);
        fixture.Options.EntriesEnabled = false;
        await fixture.Strategy().BuyAsync();
        fixture.Options.EntriesEnabled = true;
        fixture.Market.IsOpen = false;
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades, Is.Empty);
    }

    [Test]
    public async Task IdenticalPortfoliosGetSeparateTransactionsAndReviewIsShared()
    {
        var fixture = new Fixture();
        var other = fixture.Portfolio with
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
        fixture.Store.Portfolios[other.Id] = other;
        await fixture.Strategy().BuyAsync();
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(2));
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T14:00:00Z");
        await fixture.Strategy().SellAsync();
        await fixture.Strategy().SellAsync();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Analyser.Calls, Is.EqualTo(1));
            Assert.That(fixture.Store.AllRuns.All(x => x.Reviews.Count == 1), Is.True);
            Assert.That(fixture.Store.Portfolios.Values.All(x => x.Cash >= 500), Is.True);
        });
    }

    [Test]
    public async Task ForcedLossExitIgnoresAiDisablementAndChangedAssociation()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        fixture.Market.Price = 90;
        fixture.Analyser.Fail = true;
        fixture.Options.EntriesEnabled = false;
        fixture.Store.Associated = false;
        await fixture.Strategy().SellAsync();
        Assert.Multiple(() =>
        {
            Assert.That(fixture.Analyser.Calls, Is.Zero);
            Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
            Assert.That(fixture.Store.Trades.Count, Is.EqualTo(2));
            Assert.That(fixture.Slack.Messages.Last().Message, Does.Contain("Realised P&L: -"));
            Assert.That(fixture.Store.Portfolios[fixture.Portfolio.Id].Cash, Is.LessThan(1000));
        });
    }

    [Test]
    public async Task AiFailureIsSavedAndNextSlotRetries()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T14:00:00Z");
        fixture.Analyser.Fail = true;
        await fixture.Strategy().SellAsync();
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Analyser.Calls, Is.EqualTo(1));
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.False);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(30);
        fixture.Analyser.Fail = false;
        fixture.Analyser.Action = SupportAction.Sell;
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Analyser.Calls, Is.EqualTo(2));
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
    }

    [Test]
    public async Task SlackFailureDoesNotBlockExitAndRetriesAfterHours()
    {
        var fixture = new Fixture();
        fixture.Slack.Fail = true;
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
        Assert.That(fixture.Slack.Messages, Is.Empty);
        fixture.Clock.Now = fixture.Clock.Now.AddHours(5);
        fixture.Market.IsOpen = false;
        fixture.Slack.Fail = false;
        await fixture.Strategy().SellAsync();
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Slack.Messages.Count, Is.EqualTo(2));
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task RestartAfterEntryCommitReplaysWithoutAnotherDebit()
    {
        var fixture = new Fixture();
        fixture.Store.FailAfterApply = true;
        await fixture.Strategy().BuyAsync();
        var cash = fixture.Store.Portfolios[fixture.Portfolio.Id].Cash;
        fixture.Options.EntriesEnabled = false;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(10);
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().EntryApplied, Is.True);
        Assert.That(fixture.Store.Portfolios[fixture.Portfolio.Id].Cash, Is.EqualTo(cash));
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task SaleMustFinishBeforeNewEntryUsesUpdatedCash()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(1));
        await fixture.Strategy().SellAsync();
        var available = fixture.Store.Portfolios[fixture.Portfolio.Id].Cash;
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        await fixture.Strategy().BuyAsync();
        var newRun = fixture.Store.AllRuns.Single(x => !x.Closed);
        Assert.That(newRun.Entry.PortfolioAmount, Is.LessThanOrEqualTo(available * .5m));
        Assert.That(fixture.Store.Trades.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task EntryWindowIsFiveMinutesAndOwnedLockPreventsEntry()
    {
        var fixture = new Fixture();
        await using (var lease = await fixture.Locks.TryAcquireAsync($"portfolio:{fixture.Portfolio.Id}", default))
        {
            await fixture.Strategy().BuyAsync();
        }

        Assert.That(fixture.Store.Trades, Is.Empty);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(5);
        await fixture.Strategy().BuyAsync();
        Assert.That(fixture.Store.Trades, Is.Empty);
    }

    [Test]
    public async Task MissingPricesKeepForcedExitPendingUntilRecovery()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        fixture.Market.FailSnapshot = true;
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.False);
        fixture.Clock.Now = fixture.Clock.Now.AddMinutes(1);
        fixture.Market.FailSnapshot = false;
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().Closed, Is.True);
    }

    [Test]
    public async Task FxSizingAndSaleUsePortfolioCurrency()
    {
        var fixture = new Fixture();
        fixture.Store.Portfolios[fixture.Portfolio.Id] = fixture.Portfolio with
        {
            Currency = CurrencyCode.GBP,
            UserCurrency = CurrencyCode.USD
        };
        await fixture.Strategy().BuyAsync();
        var run = fixture.Store.AllRuns.Single();
        Assert.That(run.Entry.PortfolioAmount, Is.LessThanOrEqualTo(500));
        Assert.That(run.Entry.UserAmount, Is.EqualTo(run.Entry.Price * run.Entry.Quantity));
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.Portfolios[fixture.Portfolio.Id].Cash, Is.EqualTo(1000));
    }

    [Test]
    public async Task SaleUsesOnlyRemainingStrategyQuantity()
    {
        var fixture = new Fixture();
        await fixture.Strategy().BuyAsync();
        fixture.Store.Remaining = 1;
        fixture.Clock.Now = DateTimeOffset.Parse("2026-10-23T19:30:00Z");
        await fixture.Strategy().SellAsync();
        Assert.That(fixture.Store.AllRuns.Single().Exit!.Quantity, Is.EqualTo(-1));
    }
}
