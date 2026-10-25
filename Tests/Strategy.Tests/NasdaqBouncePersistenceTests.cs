using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Services.Database;
using Services.Migrations;
using Services.Repositories;
using Services.Strategies.NasdaqBounce;
using Shared.Enums;

namespace Strategy.Tests;

public class NasdaqBouncePersistenceTests
{
    private readonly List<DatabaseContext> _serviceContexts = [];

    [TearDown]
    public void DisposeServiceContexts()
    {
        foreach (var database in _serviceContexts)
        {
            database.Dispose();
        }

        _serviceContexts.Clear();
    }

    private sealed class Factory : IDbContextFactory<DatabaseContext>
    {
        private readonly DbContextOptions<DatabaseContext> _options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public int Saves { get; set; }
        public int FailAfterSave { get; set; } = -1;

        public DatabaseContext CreateDbContext() => new Context(_options, this);

        private sealed class Context(DbContextOptions<DatabaseContext> options, Factory owner) : DatabaseContext(options)
        {
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                // Cosmos treats nested configuration as owned by convention; InMemory needs it explicitly.
                modelBuilder.Entity<StrategyTable>().OwnsOne(x => x.Config);
            }

            public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
            {
                var result = await base.SaveChangesAsync(ct);

                if (++owner.Saves == owner.FailAfterSave)
                {
                    throw new IOException("Simulated process loss after committed document");
                }

                return result;
            }
        }
    }

    private Services.TransactionService CreateTransactionService(Factory factory)
    {
        var database = factory.CreateDbContext();
        _serviceContexts.Add(database);
        var portfolios = new PortfolioRepository(database, new StrategyRepository(database), factory);
        var transactions = new TransactionRepository(database, portfolios, factory);

        return new Services.TransactionService(
            portfolios,
            null!,
            transactions,
            null!,
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
    }

    private NasdaqBounceService CreateNasdaqBounceService(Factory factory)
    {
        var database = factory.CreateDbContext();
        _serviceContexts.Add(database);
        var strategies = new StrategyRepository(database);
        var portfolioRepository = new PortfolioRepository(database, strategies, factory);
        var transactions = CreateTransactionService(factory);
        var portfolios = new Services.PortfolioService(
            portfolioRepository,
            transactions,
            null!,
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        return new NasdaqBounceService(
            new NasdaqBounceRepository(factory),
            portfolios,
            strategies,
            new Services.UserService(new UserRepository(database)),
            transactions);
    }

    private static async Task<(Factory Factory, StrategyTrade Buy)> SetupAsync()
    {
        var factory = new Factory();
        var user = Guid.NewGuid();
        var portfolio = Guid.NewGuid();
        await using var db = factory.CreateDbContext();
        db.Portfolio.Add(new PortfolioTable
        {
            Id = portfolio,
            PartitionKey = user.ToString(),
            UserId = user,
            Currency = CurrencyCode.GBP,
            FreeCash = 1000
        });
        await db.SaveChangesAsync();
        factory.Saves = 0;

        return (factory, new StrategyTrade
        {
            TransactionId = Guid.NewGuid(),
            PortfolioId = portfolio,
            UserId = user,
            StockId = Guid.NewGuid(),
            StrategyId = "nasdaq-bounce-overnight",
            StrategyVersion = "1.0.0",
            Symbol = "QQQ",
            Exchange = "NASDAQ",
            Price = 100,
            Quantity = 5,
            PortfolioAmount = 400,
            UserAmount = 500,
            StockCurrency = CurrencyCode.USD,
            PortfolioCurrency = CurrencyCode.GBP,
            Time = DateTimeOffset.UtcNow,
            SourceCandleTime = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task EntryResumesAfterEachCommittedDocument(int failAt)
    {
        var (factory, buy) = await SetupAsync();
        factory.FailAfterSave = failAt;
        var transactionService = CreateTransactionService(factory);
        await Assert.ThrowsAsync<IOException>(() => transactionService.ApplyStrategyTradeAsync(buy, default));
        factory.FailAfterSave = -1;
        await transactionService.ApplyStrategyTradeAsync(buy, default);
        await transactionService.ApplyStrategyTradeAsync(buy, default);
        await using var db = factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.Portfolio.Single().FreeCash, Is.EqualTo(600));
            Assert.That(db.Transactions.Count(), Is.EqualTo(1));
            Assert.That(db.PortfolioHoldings.Single().Quantity, Is.EqualTo(5));
            Assert.That(db.Portfolio.Single().AppliedStrategyTransactions.Count, Is.EqualTo(1));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task SaleResumesAfterEachCommittedDocument(int failAt)
    {
        var (factory, buy) = await SetupAsync();
        var transactionService = CreateTransactionService(factory);
        await transactionService.ApplyStrategyTradeAsync(buy, default);
        var sell = new StrategyTrade
        {
            TransactionId = Guid.NewGuid(),
            PortfolioId = buy.PortfolioId,
            UserId = buy.UserId,
            StockId = buy.StockId,
            OriginalTransactionId = buy.TransactionId,
            StrategyId = buy.StrategyId,
            StrategyVersion = buy.StrategyVersion,
            Symbol = buy.Symbol,
            Exchange = buy.Exchange,
            Price = 90,
            Quantity = -5,
            PortfolioAmount = -360,
            UserAmount = -450,
            PortfolioCurrency = CurrencyCode.GBP,
            Time = buy.Time.AddDays(1),
            SourceCandleTime = buy.Time.AddDays(1).AddMinutes(-1)
        };
        factory.Saves = 0;
        factory.FailAfterSave = failAt;
        await Assert.ThrowsAsync<IOException>(() => transactionService.ApplyStrategyTradeAsync(sell, default));
        factory.FailAfterSave = -1;
        await transactionService.ApplyStrategyTradeAsync(sell, default);
        await transactionService.ApplyStrategyTradeAsync(sell, default);
        await using var db = factory.CreateDbContext();
        Assert.Multiple(() =>
        {
            Assert.That(db.Portfolio.Single().FreeCash, Is.EqualTo(960));
            Assert.That(db.Transactions.Count(), Is.EqualTo(2));
            Assert.That(db.PortfolioHoldings.Single().Quantity, Is.Zero);
            Assert.That(db.Transactions.Single(x => x.Id == buy.TransactionId).QuantityRemaining, Is.Zero);
            Assert.That(db.Transactions.Single(x => x.Id == sell.TransactionId).OriginalTransactionId, Is.EqualTo(buy.TransactionId));
        });
    }

    [Test]
    public async Task InsufficientCashDoesNotCreateAnyTradeOrHolding()
    {
        var (factory, buy) = await SetupAsync();
        buy.PortfolioAmount = 1001;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTransactionService(factory).ApplyStrategyTradeAsync(buy, default));
        await using var db = factory.CreateDbContext();
        Assert.That(db.Transactions.Count(), Is.Zero);
        Assert.That(db.PortfolioHoldings.Count(), Is.Zero);
        Assert.That(db.Portfolio.Single().FreeCash, Is.EqualTo(1000));
    }

    [Test]
    public async Task SaleCannotCreditCashWithoutItsMatchingPurchase()
    {
        var (factory, buy) = await SetupAsync();
        await CreateTransactionService(factory).ApplyStrategyTradeAsync(buy, default);
        var sell = new StrategyTrade
        {
            TransactionId = Guid.NewGuid(),
            PortfolioId = buy.PortfolioId,
            UserId = buy.UserId,
            StockId = buy.StockId,
            StrategyId = buy.StrategyId,
            StrategyVersion = buy.StrategyVersion,
            Symbol = buy.Symbol,
            Exchange = buy.Exchange,
            Price = 100,
            Quantity = -5,
            PortfolioAmount = -400,
            UserAmount = -500,
            PortfolioCurrency = CurrencyCode.GBP,
            Time = buy.Time.AddDays(1),
            SourceCandleTime = buy.SourceCandleTime.AddDays(1)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateTransactionService(factory).ApplyStrategyTradeAsync(sell, default));
        await using var db = factory.CreateDbContext();
        Assert.That(db.Portfolio.Single().FreeCash, Is.EqualTo(600));
        Assert.That(db.Transactions.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ExecutionRepositoryPreservesPayloadsAndPendingAlertQueries()
    {
        var (factory, buy) = await SetupAsync();
        var repository = new NasdaqBounceRepository(factory);
        var run = new NasdaqBounceRun
        {
            Id = Guid.NewGuid(),
            Portfolio = new NasdaqBouncePortfolio(buy.PortfolioId, buy.UserId, "Strategy", 1000, CurrencyCode.GBP, CurrencyCode.USD),
            Entry = buy
        };
        var slot = new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.Zero);
        var review = new NasdaqBounceReview
        {
            Slot = slot,
            Failure = "No support decision"
        };
        var signal = new NasdaqBounceEntrySignal(
            new DateOnly(2026, 10, 23),
            1.5m,
            0.4m,
            new NasdaqBounceMarketSnapshot(buy.StockId, buy.Exchange, 100, 98.5m, slot, []));

        await repository.SaveRunAsync(run, default);
        await repository.SaveReviewAsync(review, default);
        await repository.SaveEntrySignalAsync(signal, default);
        Assert.That((await repository.GetPendingRunsAsync(default)).Single().Entry.TransactionId, Is.EqualTo(buy.TransactionId));
        Assert.That((await repository.GetPortfolioRunsAsync(Guid.NewGuid(), default)), Is.Empty);
        Assert.That(await repository.GetReviewAsync(slot.AddMinutes(15), default), Is.Null);
        Assert.That((await repository.GetReviewAsync(slot, default))!.Failure, Is.EqualTo(review.Failure));
        Assert.That((await repository.GetEntrySignalAsync(signal.Date, default))!.Snapshot.StockId, Is.EqualTo(buy.StockId));

        run.Closed = true;
        run.Alerts.Add(new NasdaqBounceAlert
        {
            Key = "exit",
            Sent = false
        });
        await repository.SaveRunAsync(run, default);
        Assert.That(await repository.GetPendingRunsAsync(default), Has.Count.EqualTo(1));

        run.Alerts[0].Sent = true;
        await repository.SaveRunAsync(run, default);
        Assert.That(await repository.GetPendingRunsAsync(default), Is.Empty);
        Assert.That((await repository.GetPortfolioRunsAsync(buy.PortfolioId, default)).Single().Closed, Is.True);

        await using var database = factory.CreateDbContext();
        Assert.That(await database.StrategyExecutions.CountAsync(), Is.EqualTo(3));
        Assert.That(await database.StrategyExecutions.AnyAsync(execution => execution.Id == NasdaqBounceRules.Id($"review:{slot:O}")), Is.True);
    }

    [Test]
    public async Task NasdaqServiceComposesEligiblePortfoliosThroughExistingServices()
    {
        var (factory, buy) = await SetupAsync();
        var service = CreateNasdaqBounceService(factory);
        Assert.That(await service.GetEntryPortfoliosAsync(default), Is.Empty);

        await using var database = factory.CreateDbContext();
        await new RegisterNasdaqBounceStrategy(database).ApplyAsync();
        var portfolio = await database.Portfolio.SingleAsync();
        portfolio.StrategyId = NasdaqBounceRules.StrategyId;
        portfolio.Status = StatusType.Active;
        portfolio.Name = "Overnight";
        database.Users.Add(new UserTable
        {
            PartitionKey = buy.UserId.ToString(),
            CurrencyCode = CurrencyCode.USD
        });
        await database.SaveChangesAsync();

        var composed = (await service.GetEntryPortfoliosAsync(default)).Single();
        Assert.That(composed.Id, Is.EqualTo(buy.PortfolioId));
        Assert.That(composed.Cash, Is.EqualTo(1000));
        Assert.That(composed.Currency, Is.EqualTo(CurrencyCode.GBP));
        Assert.That(composed.UserCurrency, Is.EqualTo(CurrencyCode.USD));
        Assert.That(await service.GetPortfolioAsync(buy.UserId, buy.PortfolioId, default), Is.EqualTo(composed));
        Assert.That(await service.HasTradeStartedAsync(buy, default), Is.False);

        await service.ApplyTradeAsync(buy, default);
        Assert.That(await service.HasTradeStartedAsync(buy, default), Is.True);
        Assert.That((await service.GetPortfolioAsync(buy.UserId, buy.PortfolioId, default)).Cash, Is.EqualTo(600));

        var strategy = await database.Strategies.SingleAsync();
        strategy.Status = StatusType.Inactive;
        await database.SaveChangesAsync();
        Assert.That(await service.GetEntryPortfoliosAsync(default), Is.Empty);

        strategy.Status = StatusType.Active;
        portfolio.Status = StatusType.Inactive;
        await database.SaveChangesAsync();
        Assert.That(await service.GetEntryPortfoliosAsync(default), Is.Empty);
    }

    [TestCase(2, 2)]
    [TestCase(10, 5)]
    [TestCase(-1, 0)]
    public async Task RemainingQuantityIsClampedToPurchaseAndStrategyHolding(int holdingQuantity, int expectedQuantity)
    {
        var (factory, buy) = await SetupAsync();
        var service = CreateNasdaqBounceService(factory);
        await service.ApplyTradeAsync(buy, default);
        await using var database = factory.CreateDbContext();
        var holding = await database.PortfolioHoldings.SingleAsync();
        holding.Quantity = holdingQuantity;
        await database.SaveChangesAsync();
        var run = new NasdaqBounceRun
        {
            Portfolio = new NasdaqBouncePortfolio(buy.PortfolioId, buy.UserId, "Strategy", 1000, CurrencyCode.GBP, CurrencyCode.USD),
            Entry = buy
        };

        Assert.That(await service.GetRemainingQuantityAsync(run, default), Is.EqualTo(expectedQuantity));
    }

    [Test]
    public async Task CatalogueMigrationIsIdempotent()
    {
        var (factory, _) = await SetupAsync();
        await using var db = factory.CreateDbContext();
        await new RegisterNasdaqBounceStrategy(db).ApplyAsync();
        await new RegisterNasdaqBounceStrategy(db).ApplyAsync();
        Assert.That(db.Strategies.Count(), Is.EqualTo(1));
        Assert.That(db.StrategyVersions.Single().Version, Is.EqualTo("1.0.0"));
    }

    [Test]
    public async Task LegacyDuplicateLookupAndOriginalPurchaseArePortfolioScoped()
    {
        var (factory, buy) = await SetupAsync();
        await CreateTransactionService(factory).ApplyStrategyTradeAsync(buy, default);
        await using var db = factory.CreateDbContext();
        var portfolios = new PortfolioRepository(db, new StrategyRepository(db), factory);
        var transactions = new TransactionRepository(db, portfolios, factory);
        Assert.That(
            await transactions.TransactionExistsAsync(
                buy.StockId,
                100,
                5,
                500,
                OrderType.MARKET_BUY,
                Guid.NewGuid()),
            Is.Null);
        Assert.That(
            await transactions.FindOriginalTransactionAsync(buy.StockId, 5, Guid.NewGuid()),
            Is.Null);
        Assert.That(
            await transactions.TransactionExistsAsync(
                buy.StockId,
                100,
                5,
                500,
                OrderType.MARKET_BUY,
                buy.PortfolioId),
            Is.Not.Null);
    }

    [Test]
    public void CosmosModelMapsExecutionContainerAndEtags()
    {
        using var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseCosmos("https://localhost:8081", Convert.ToBase64String(new byte[64]), "not-used").Options);
        Assert.That(db.Model.FindEntityType(typeof(StrategyExecutionTable))!.GetContainer(), Is.EqualTo("StrategyExecutions"));
        Assert.That(db.Model.FindEntityType(typeof(PortfolioTable))!.GetETagProperty(), Is.Not.Null);
    }
}
