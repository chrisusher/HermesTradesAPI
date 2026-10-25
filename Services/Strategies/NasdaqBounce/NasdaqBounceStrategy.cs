using System.Diagnostics;
using Shared.Config;
using Shared.Interfaces;

namespace Services.Strategies.NasdaqBounce;

public sealed class NasdaqBounceStrategy(
    INasdaqBounceStore store,
    INasdaqBounceMarketData market,
    NasdaqBounceCalendar calendar,
    INasdaqBounceSupportAnalyser analyser,
    IStrategyLock locks,
    INotificationClient notifications,
    NasdaqBounceOptions options,
    TimeProvider clock,
    ILogger<NasdaqBounceStrategy> logger)
{
    // One market snapshot per invocation/session; all portfolios use the same as-of data.
    private readonly Dictionary<DateOnly, NasdaqBounceMarketSnapshot> _snapshots = [];

    public Task BuyAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("Buy", BuyCoreAsync, cancellationToken);

    public Task SellAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("Sell", SellCoreAsync, cancellationToken);

    private async Task ExecuteAsync(string operation, Func<CancellationToken, Task> execute, CancellationToken cancellationToken)
    {
        var invocationId = Guid.NewGuid();
        using var scope = logger.BeginScope(new Dictionary<string, object> {
            ["StrategyId"] = NasdaqBounceRules.StrategyId,
            ["StrategyVersion"] = NasdaqBounceRules.Version,
            ["StrategyInvocationId"] = invocationId,
            ["StrategyOperation"] = operation
        });
        var started = Stopwatch.GetTimestamp();
        logger.LogInformation(
            "Strategy tick {StrategyInvocationId} started for {StrategyOperation} at {ExecutionTime}; entries enabled: {EntriesEnabled}.",
            invocationId,
            operation,
            clock.GetUtcNow(),
            options.EntriesEnabled);

        try
        {
            await execute(cancellationToken);
            logger.LogInformation(
                "Strategy tick {StrategyInvocationId} finished after {ElapsedMilliseconds} ms.",
                invocationId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Strategy tick {StrategyInvocationId} cancelled after {ElapsedMilliseconds} ms; invocation cancellation requested: {CancellationRequested}.",
                invocationId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                cancellationToken.IsCancellationRequested);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Strategy tick {StrategyInvocationId} failed after {ElapsedMilliseconds} ms.",
                invocationId,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    private IDisposable? BeginRunScope(NasdaqBounceRun run) =>
        logger.BeginScope(new Dictionary<string, object> {
            ["PortfolioId"] = run.Portfolio.Id,
            ["RunId"] = run.Id
        });

    private async Task BuyCoreAsync(CancellationToken cancellationToken)
    {
        // Entry signals are evaluated once per session, then reused across all eligible portfolios.
        if (!options.EntriesEnabled)
        {
            logger.LogInformation("Entry skipped: {Reason}.", "EntriesDisabled");

            return;
        }

        var now = clock.GetUtcNow();
        var date = NasdaqBounceRules.LocalDate(now);
        var session = await calendar.GetSessionAsync(date, cancellationToken);

        if (session is null || now < session.Deadline || now >= session.Deadline.AddMinutes(5))
        {
            logger.LogInformation(
                "Entry skipped: {Reason}; session {SessionDate}, now {ExecutionTime}, deadline {EntryDeadline}, window ends {EntryWindowEnds}.",
                session is null ? "NoTradingSession" : "OutsideEntryWindow",
                date,
                now,
                session?.Deadline,
                session?.Deadline.AddMinutes(5));

            return;
        }

        if (!await market.IsOpenAsync(cancellationToken))
        {
            logger.LogInformation("Entry skipped: {Reason}; session {SessionDate}.", "MarketClosed", date);

            return;
        }

        logger.LogInformation(
            "Entry window active for {SessionDate}; session open {SessionOpen}, close {SessionClose}, deadline {EntryDeadline}.",
            date,
            session.Open,
            session.Close,
            session.Deadline);
        var previous = await calendar.AdjacentAsync(date, -1, cancellationToken);
        var next = await calendar.AdjacentAsync(date, 1, cancellationToken);
        var signal = await GetEntrySignalAsync(session, previous.Date, now, cancellationToken);

        if (signal is null || signal.Allocation == 0)
        {
            logger.LogInformation(
                "Entry skipped: {Reason}; session {SessionDate}, decline {DeclinePercent}, allocation {Allocation}.",
                signal is null ? "EntrySignalUnavailable" : "NoEntryAllocation",
                date,
                signal?.Decline,
                signal?.Allocation);

            return;
        }

        var (decline, allocation, snapshot) = (signal.Decline, signal.Allocation, signal.Snapshot);
        var portfolios = await store.GetEntryPortfoliosAsync(cancellationToken);
        logger.LogInformation(
            "Entry signal accepted for {SessionDate}; decline {DeclinePercent}, allocation {Allocation}, eligible portfolios {PortfolioCount}, next " +
            "review {ReviewDate}, exit deadline {ExitDeadline}.",
            date,
            decline,
            allocation,
            portfolios.Count,
            next.Date,
            next.Deadline);

        foreach (var portfolio in portfolios)
        {
            using var portfolioScope = logger.BeginScope(new Dictionary<string, object> {
                ["PortfolioId"] = portfolio.Id
            });
            cancellationToken.ThrowIfCancellationRequested();
            await using var lease = await locks.TryAcquireAsync($"portfolio:{portfolio.Id}", cancellationToken);

            if (lease is null)
            {
                logger.LogInformation("Entry skipped: {Reason}. Portfolio {PortfolioId}.", "PortfolioLeaseUnavailable", portfolio.Id);
                continue;
            }

            try
            {
                var token = lease.Token;

                if (clock.GetUtcNow() >= session.Deadline.AddMinutes(5))
                {
                    logger.LogInformation(
                        "Remaining entries skipped: {Reason}; window ends {EntryWindowEnds}. Portfolio {PortfolioId}.",
                        "EntryWindowExpired",
                        session.Deadline.AddMinutes(5),
                        portfolio.Id);
                    break;
                }

                var runs = await store.GetPortfolioRunsAsync(portfolio.Id, token);
                // The sell Function closes the old run first. A subsequent minute tick may then enter.

                if (runs.Any(run => !run.Closed || run.EntryDate == date))
                {
                    logger.LogInformation(
                        "Entry skipped: {Reason}; open runs {OpenRunCount}, existing entries for {SessionDate}: {ExistingEntryCount}. Portfolio {PortfolioId}.",
                        "ExistingPositionOrSessionEntry",
                        runs.Count(run => !run.Closed),
                        date,
                        runs.Count(run => run.EntryDate == date),
                        portfolio.Id);
                    continue;
                }

                var currentPortfolio = await store.GetPortfolioAsync(portfolio.UserId, portfolio.Id, token);
                var rate = await market.GetUsdRateAsync(currentPortfolio.Currency, token);
                var quantity = NasdaqBounceRules.Quantity(currentPortfolio.Cash, allocation, snapshot.Price, rate);
                logger.LogInformation(
                    "Entry sized using cash {AvailableCash} {PortfolioCurrency}, allocation {Allocation}, price {Price}, USD conversion rate {PortfolioRate}; " +
                    "quantity {Quantity}. Portfolio {PortfolioId}.",
                    currentPortfolio.Cash,
                    currentPortfolio.Currency,
                    allocation,
                    snapshot.Price,
                    rate,
                    quantity,
                    portfolio.Id);

                if (quantity <= 0)
                {
                    logger.LogInformation("Entry skipped: {Reason}. Portfolio {PortfolioId}.", "NonPositiveQuantity", portfolio.Id);
                    continue;
                }

                var run = new NasdaqBounceRun {
                    Id = NasdaqBounceRules.Id($"{currentPortfolio.Id}:{date:yyyy-MM-dd}"),
                    Portfolio = currentPortfolio,
                    EntryDate = date,
                    ReviewDate = next.Date,
                    ExitDeadline = next.Deadline,
                    EntryWindowEnds = session.Deadline.AddMinutes(5),
                    Allocation = allocation,
                    Decline = decline
                };
                using var runScope = BeginRunScope(run);
                run.Entry = await CreateTradeAsync(run, snapshot, quantity, now, "buy", token, rate);
                // Persist the immutable intent before any portfolio or transaction mutation.
                await store.SaveRunAsync(run, token);
                logger.LogInformation(
                    "Entry intent persisted for transaction {TransactionId}; entry window ends {EntryWindowEnds}. Portfolio {PortfolioId}.",
                    run.Entry.TransactionId,
                    run.EntryWindowEnds,
                    portfolio.Id);
                await CompleteEntryAsync(run, token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lease.Token.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Entry interrupted; invocation cancelled: {CancellationRequested}, lease cancelled: {LeaseCancellationRequested}. Portfolio {PortfolioId}.",
                    cancellationToken.IsCancellationRequested,
                    lease.Token.IsCancellationRequested,
                    portfolio.Id);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "QQQ entry failed for portfolio {PortfolioId}; persisted intents will resume.", portfolio.Id);
            }
        }

        await DrainNotificationsAsync(cancellationToken);
    }

    private async Task<NasdaqBounceEntrySignal?> GetEntrySignalAsync(
        NasdaqBounceSession session,
        DateOnly previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var saved = await store.GetEntrySignalAsync(session.Date, cancellationToken);

        if (saved is not null)
        {
            logger.LogInformation(
                "Reusing entry signal for {SessionDate}; decline {DeclinePercent}, allocation {Allocation}, price {Price}, candle {SourceCandleTime}.",
                session.Date,
                saved.Decline,
                saved.Allocation,
                saved.Snapshot.Price,
                saved.Snapshot.PriceTime);

            return saved;
        }

        await using var lease = await locks.TryAcquireAsync($"entry-signal:{session.Date:yyyy-MM-dd}", cancellationToken);

        if (lease is null)
        {
            logger.LogInformation("Entry signal deferred: {Reason}; session {SessionDate}.", "EntrySignalLeaseUnavailable", session.Date);

            return null;
        }

        saved = await store.GetEntrySignalAsync(session.Date, lease.Token);

        if (saved is not null)
        {
            logger.LogInformation(
                "Reusing entry signal persisted while acquiring lease for {SessionDate}; decline {DeclinePercent}, allocation {Allocation}.",
                session.Date,
                saved.Decline,
                saved.Allocation);

            return saved;
        }

        logger.LogInformation("Evaluating entry signal for {SessionDate} against previous session {PreviousSessionDate}.", session.Date, previous);
        var snapshot = await market.GetSnapshotAsync(session, previous, now, lease.Token);
        var decline = (snapshot.PreviousClose - snapshot.Price) / snapshot.PreviousClose * 100m;

        var signal = new NasdaqBounceEntrySignal(
            session.Date,
            decline,
            NasdaqBounceRules.Allocation(decline),
            snapshot with {
                Candles = []
            });

        // Persist no-buy decisions too: the retry window must not become five minutes of new signals.
        await store.SaveEntrySignalAsync(signal, lease.Token);
        logger.LogInformation(
            "Entry signal persisted for {SessionDate}; previous close {PreviousClose}, price {Price}, candle {SourceCandleTime}, decline " +
            "{DeclinePercent}, allocation {Allocation}.",
            session.Date,
            snapshot.PreviousClose,
            snapshot.Price,
            snapshot.PriceTime,
            signal.Decline,
            signal.Allocation);

        return signal;
    }

    private async Task SellCoreAsync(CancellationToken cancellationToken)
    {
        // A single sell tick first resumes pending trades, then reviews open positions.
        _snapshots.Clear();
        var candidates = await store.GetPendingRunsAsync(cancellationToken);
        logger.LogInformation("Loaded {PendingRunCount} runs with pending positions or notifications.", candidates.Count);
        // Load persisted positions, independently of entry enablement and current associations.

        foreach (var candidate in candidates)
        {
            using var runScope = BeginRunScope(candidate);
            cancellationToken.ThrowIfCancellationRequested();
            await using var lease = await locks.TryAcquireAsync($"portfolio:{candidate.Portfolio.Id}", cancellationToken);

            if (lease is null)
            {
                logger.LogInformation(
                    "Position deferred: {Reason}. Portfolio {PortfolioId}. Run {RunId}.",
                    "PortfolioLeaseUnavailable",
                    candidate.Portfolio.Id,
                    candidate.Id);
                continue;
            }

            var run = (await store.GetPortfolioRunsAsync(candidate.Portfolio.Id, lease.Token)).Single(position => position.Id == candidate.Id);
            logger.LogInformation(
                "Position loaded; entry applied {EntryApplied}, exit intent present {HasExitIntent}, closed {Closed}, review date {ReviewDate}, exit deadline " +
                "{ExitDeadline}, pending alerts {PendingAlertCount}. Portfolio {PortfolioId}. Run {RunId}.",
                run.EntryApplied,
                run.Exit is not null,
                run.Closed,
                run.ReviewDate,
                run.ExitDeadline,
                run.Alerts.Count(alert => !alert.Sent),
                candidate.Portfolio.Id,
                candidate.Id);

            try
            {
                var token = lease.Token;

                if (!run.EntryApplied && !run.Closed)
                {
                    if (clock.GetUtcNow() >= run.EntryWindowEnds && !await store.HasTradeStartedAsync(run.Entry, token))
                    {
                        logger.LogWarning(
                            "Entry intent cancelled: {Reason}; transaction {TransactionId}, entry window ended {EntryWindowEnds}; no cash movement was started. Portfolio " +
                            "{PortfolioId}. Run {RunId}.",
                            "UnstartedEntryExpired",
                            run.Entry.TransactionId,
                            run.EntryWindowEnds,
                            candidate.Portfolio.Id,
                            candidate.Id);
                        run.Closed = true;

                        var message = string.Concat(
                            $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); ",
                            "entry window expired before any cash movement. ",
                            $"Position intent {run.Id} cancelled.");

                        AddAlert(run, "entry-expired", "QQQ entry skipped", message);

                        await store.SaveRunAsync(run, token);
                        logger.LogInformation(
                            "Expired entry cancellation persisted for transaction {TransactionId}. Portfolio {PortfolioId}. Run {RunId}.",
                            run.Entry.TransactionId,
                            candidate.Portfolio.Id,
                            candidate.Id);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Resuming entry intent for transaction {TransactionId}; entry window ends {EntryWindowEnds}. Portfolio {PortfolioId}. Run {RunId}.",
                            run.Entry.TransactionId,
                            run.EntryWindowEnds,
                            candidate.Portfolio.Id,
                            candidate.Id);
                        await CompleteEntryAsync(run, token);
                    }
                }

                if (run.Exit is not null && !run.Closed)
                {
                    logger.LogInformation(
                        "Resuming exit intent for transaction {TransactionId}; reason {ExitReason}. Portfolio {PortfolioId}. Run {RunId}.",
                        run.Exit.TransactionId,
                        run.ExitReason,
                        candidate.Portfolio.Id,
                        candidate.Id);
                    await CompleteExitAsync(run, token);
                }

                if (!run.Closed)
                {
                    await ReviewPositionAsync(run, token);
                }
                else
                {
                    logger.LogInformation(
                        "Position requires no further trading: {Reason}. Portfolio {PortfolioId}. Run {RunId}.",
                        "PositionClosed",
                        candidate.Portfolio.Id,
                        candidate.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lease.Token.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Position processing interrupted; invocation cancelled: {CancellationRequested}, lease cancelled: {LeaseCancellationRequested}. Portfolio " +
                    "{PortfolioId}. Run {RunId}.",
                    cancellationToken.IsCancellationRequested,
                    lease.Token.IsCancellationRequested,
                    candidate.Portfolio.Id,
                    candidate.Id);
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "QQQ position {RunId} remains pending. Portfolio {PortfolioId}.", run.Id, candidate.Portfolio.Id);

                var message = string.Concat(
                    $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); ",
                    $"position {run.Id} remains pending. Failure: {exception.GetType().Name}. ",
                    $"Exit deadline: {run.ExitDeadline:O}.");

                AddAlert(
                    run,
                    $"error:{NasdaqBounceRules.LocalDate(clock.GetUtcNow())}",
                    "QQQ strategy requires attention",
                    message);

                await store.SaveRunAsync(run, lease.Token);
                logger.LogInformation(
                    "Pending position and recovery alert persisted; exit deadline {ExitDeadline}. Portfolio {PortfolioId}. Run {RunId}.",
                    run.ExitDeadline,
                    candidate.Portfolio.Id,
                    candidate.Id);
            }
        }

        await DrainNotificationsAsync(cancellationToken);
    }

    private async Task DrainNotificationsAsync(CancellationToken cancellationToken)
    {
        // Finish trading for every portfolio before attempting slow or unavailable Slack deliveries.
        var candidates = (await store.GetPendingRunsAsync(cancellationToken))
            .Where(run => run.Alerts.Any(alert => !alert.Sent))
            .ToList();

        logger.LogInformation("Draining pending notifications for {NotificationRunCount} runs.", candidates.Count);

        foreach (var candidate in candidates)
        {
            using var runScope = BeginRunScope(candidate);
            await using var lease = await locks.TryAcquireAsync($"portfolio:{candidate.Portfolio.Id}", cancellationToken);

            if (lease is null)
            {
                logger.LogInformation(
                    "Notification delivery deferred: {Reason}. Portfolio {PortfolioId}. Run {RunId}.",
                    "PortfolioLeaseUnavailable",
                    candidate.Portfolio.Id,
                    candidate.Id);
                continue;
            }

            try
            {
                var run = (await store.GetPortfolioRunsAsync(candidate.Portfolio.Id, lease.Token)).Single(position => position.Id == candidate.Id);
                await FlushAsync(run, lease.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || lease.Token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "QQQ notification delivery remains pending for {RunId}. Portfolio {PortfolioId}.",
                    candidate.Id,
                    candidate.Portfolio.Id);
            }
        }
    }

    private async Task ReviewPositionAsync(NasdaqBounceRun run, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var date = NasdaqBounceRules.LocalDate(now);

        if (date < run.ReviewDate)
        {
            logger.LogInformation(
                "Position review skipped: {Reason}; session {SessionDate}, first review date {ReviewDate}. Portfolio {PortfolioId}. Run {RunId}.",
                "BeforeReviewDate",
                date,
                run.ReviewDate,
                run.Portfolio.Id,
                run.Id);

            return;
        }

        var session = await calendar.GetSessionAsync(date, cancellationToken);

        if (session is null || now < session.Open || now >= session.Close || !await market.IsOpenAsync(cancellationToken))
        {
            logger.LogInformation(
                "Position review skipped: {Reason}; session {SessionDate}, now {ExecutionTime}, open {SessionOpen}, close {SessionClose}. Portfolio " +
                "{PortfolioId}. Run {RunId}.",
                session is null ? "NoTradingSession" : now < session.Open || now >= session.Close ? "OutsideTradingHours" : "MarketClosed",
                date,
                now,
                session?.Open,
                session?.Close,
                run.Portfolio.Id,
                run.Id);

            return;
        }

        var forced = now >= run.ExitDeadline;
        var slot = forced ? null : NasdaqBounceRules.ReviewSlot(session, now);

        if (!forced && (slot is null || run.Reviews.Any(review => review.Slot == slot)))
        {
            logger.LogInformation(
                "Position review skipped: {Reason}; review slot {ReviewSlot}, exit deadline {ExitDeadline}. Portfolio {PortfolioId}. Run {RunId}.",
                slot is null ? "OutsideReviewSlot" : "ReviewAlreadyApplied",
                slot,
                run.ExitDeadline,
                run.Portfolio.Id,
                run.Id);

            return;
        }

        logger.LogInformation(
            "Position evaluation started; forced exit {ForcedExit}, review slot {ReviewSlot}, exit deadline {ExitDeadline}. Portfolio {PortfolioId}. Run " +
            "{RunId}.",
            forced,
            slot,
            run.ExitDeadline,
            run.Portfolio.Id,
            run.Id);

        var previous = await calendar.AdjacentAsync(date, -1, cancellationToken);
        var snapshot = await GetSnapshotAsync(session, previous.Date, now, cancellationToken);

        if (forced)
        {
            logger.LogInformation(
                "Exit selected: {Reason}; now {ExecutionTime}, exit deadline {ExitDeadline}. Portfolio {PortfolioId}. Run {RunId}.",
                "ScheduledExitDeadlineReached",
                now,
                run.ExitDeadline,
                run.Portfolio.Id,
                run.Id);

            await ExitAsync(run, snapshot, now, "Scheduled next-session exit", cancellationToken);

            return;
        }

        var review = await GetReviewAsync(slot!.Value, snapshot, session, now, cancellationToken);
        // Another host may currently own this review. Retry on the next minute tick.

        if (review is null)
        {
            logger.LogInformation(
                "Position review deferred: {Reason}; review slot {ReviewSlot}. Portfolio {PortfolioId}. Run {RunId}.",
                "SharedReviewUnavailable",
                slot,
                run.Portfolio.Id,
                run.Id);

            return;
        }

        run.Reviews.Add(review);

        if (review.Failure is not null)
        {
            logger.LogWarning(
                "Holding position because review {ReviewSlot} failed with {ReviewFailure}; forced exit remains {ExitDeadline}. Portfolio {PortfolioId}. Run " +
                "{RunId}.",
                slot,
                review.Failure,
                run.ExitDeadline,
                run.Portfolio.Id,
                run.Id);

            var message = string.Concat(
                $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); review {slot:O}. ",
                "Holding until the next review; forced exit remains ",
                $"{run.ExitDeadline:O}. Failure: {review.Failure}.");

            AddAlert(run, $"review:{slot:O}", "QQQ AI review unavailable", message);
        }

        if (review.Decision?.Action == SupportAction.Sell)
        {
            logger.LogInformation(
                "Exit selected: {Reason}; review slot {ReviewSlot}, explanation {DecisionExplanation}. Portfolio {PortfolioId}. Run {RunId}.",
                "BrokenIntradaySupport",
                slot,
                review.Decision.Explanation,
                run.Portfolio.Id,
                run.Id);

            await ExitAsync(run, snapshot, now, $"Broken intraday support: {review.Decision.Explanation}", cancellationToken);
        }
        else
        {
            await store.SaveRunAsync(run, cancellationToken);
            logger.LogInformation(
                "Hold decision persisted for review {ReviewSlot}; action {SupportAction}, review failure {ReviewFailure}, explanation {DecisionExplanation}, " +
                "exit deadline {ExitDeadline}. Portfolio {PortfolioId}. Run {RunId}.",
                slot,
                review.Decision?.Action,
                review.Failure,
                review.Decision?.Explanation,
                run.ExitDeadline,
                run.Portfolio.Id,
                run.Id);
        }
    }

    private async Task<NasdaqBounceMarketSnapshot> GetSnapshotAsync(
        NasdaqBounceSession session,
        DateOnly previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_snapshots.TryGetValue(session.Date, out var snapshot))
        {
            logger.LogInformation(
                "Loading review market snapshot for {SessionDate}; previous session {PreviousSessionDate}, as of {ExecutionTime}.",
                session.Date,
                previous,
                now);

            snapshot = await market.GetSnapshotAsync(session, previous, now, cancellationToken);

            _snapshots.Add(session.Date, snapshot);
            logger.LogInformation(
                "Review market snapshot loaded; session {SessionDate}, price {Price}, previous close {PreviousClose}, candle {SourceCandleTime}, candle count " +
                "{CandleCount}.",
                session.Date,
                snapshot.Price,
                snapshot.PreviousClose,
                snapshot.PriceTime,
                snapshot.Candles.Count);
        }
        else
        {
            logger.LogDebug(
                "Reusing invocation market snapshot for {SessionDate}; price {Price}, candle {SourceCandleTime}.",
                session.Date,
                snapshot.Price,
                snapshot.PriceTime);
        }

        return snapshot;
    }

    private async Task<NasdaqBounceReview?> GetReviewAsync(
        DateTimeOffset slot,
        NasdaqBounceMarketSnapshot snapshot,
        NasdaqBounceSession session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var reviewScope = logger.BeginScope(new Dictionary<string, object> {
            ["ReviewSlot"] = slot
        });
        var saved = await store.GetReviewAsync(slot, cancellationToken);

        if (saved is not null)
        {
            logger.LogInformation(
                "Reusing shared review {ReviewSlot}; action {SupportAction}, failure {ReviewFailure}, prompt version {PromptVersion}.",
                slot,
                saved.Decision?.Action,
                saved.Failure,
                saved.PromptVersion);

            return saved;
        }

        await using var lease = await locks.TryAcquireAsync($"review:{slot:yyyyMMddHHmm}", cancellationToken);

        if (lease is null)
        {
            logger.LogInformation("Shared review {ReviewSlot} deferred: {Reason}.", slot, "ReviewLeaseUnavailable");

            return null;
        }

        saved = await store.GetReviewAsync(slot, lease.Token);

        if (saved is not null)
        {
            logger.LogInformation(
                "Reusing shared review {ReviewSlot} persisted while acquiring lease; action {SupportAction}, failure {ReviewFailure}.",
                slot,
                saved.Decision?.Action,
                saved.Failure);

            return saved;
        }

        var review = new NasdaqBounceReview {
            Slot = slot
        };

        try
        {
            logger.LogInformation(
                "Support analysis started for {ReviewSlot}; prompt version {PromptVersion}, price {Price}, candle {SourceCandleTime}, candle count {CandleCount}.",
                slot,
                review.PromptVersion,
                snapshot.Price,
                snapshot.PriceTime,
                snapshot.Candles.Count);

            review.Decision = await analyser.AnalyseAsync(snapshot, session, now, lease.Token);

            logger.LogInformation(
                "Support analysis returned {SupportAction} for {ReviewSlot}; explanation {DecisionExplanation}, evidence points {EvidenceCount}.",
                review.Decision.Action,
                slot,
                review.Decision.Explanation,
                review.Decision.Evidence.Count);
        }
        catch (OperationCanceledException) when (lease.Token.IsCancellationRequested)
        {
            logger.LogWarning("Support analysis cancelled for {ReviewSlot}; review lease cancellation requested.", slot);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "QQQ support analysis failed at review {Slot}.", slot);
            review.Failure = exception.GetType().Name;
        }

        await store.SaveReviewAsync(review, lease.Token);
        logger.LogInformation(
            "Shared review {ReviewSlot} persisted; action {SupportAction}, failure {ReviewFailure}, prompt version {PromptVersion}.",
            slot,
            review.Decision?.Action,
            review.Failure,
            review.PromptVersion);

        return review;
    }

    private async Task<StrategyTrade> CreateTradeAsync(
        NasdaqBounceRun run,
        NasdaqBounceMarketSnapshot snapshot,
        decimal quantity,
        DateTimeOffset now,
        string side,
        CancellationToken cancellationToken,
        decimal? fixedPortfolioRate = null)
    {
        var rate = fixedPortfolioRate ?? await market.GetUsdRateAsync(run.Portfolio.Currency, cancellationToken);

        var userRate = run.Portfolio.UserCurrency == run.Portfolio.Currency
            ? rate
            : await market.GetUsdRateAsync(run.Portfolio.UserCurrency, cancellationToken);

        var trade = new StrategyTrade {
            TransactionId = NasdaqBounceRules.Id($"{run.Id}:{side}"),
            PortfolioId = run.Portfolio.Id,
            UserId = run.Portfolio.UserId,
            StockId = snapshot.StockId,
            StrategyId = NasdaqBounceRules.StrategyId,
            StrategyVersion = NasdaqBounceRules.Version,
            Symbol = "QQQ",
            Exchange = snapshot.Exchange,
            Price = snapshot.Price,
            Quantity = quantity,
            PortfolioAmount = snapshot.Price * quantity * rate,
            UserAmount = snapshot.Price * quantity * userRate,
            StockCurrency = CurrencyCode.USD,
            PortfolioCurrency = run.Portfolio.Currency,
            Time = now,
            SourceCandleTime = snapshot.PriceTime,
            OriginalTransactionId = side == "sell" ? run.Entry.TransactionId : null
        };
        logger.LogInformation(
            "Trade intent prepared; side {TradeSide}, transaction {TransactionId}, original purchase {OriginalTransactionId}, quantity {Quantity}, price " +
            "{Price}, portfolio amount {PortfolioAmount} {PortfolioCurrency}, user amount {UserAmount} {UserCurrency}, portfolio rate {PortfolioRate}, " +
            "user rate {UserRate}, candle {SourceCandleTime}, trade time {TradeTime}. Portfolio {PortfolioId}. Run {RunId}.",
            side,
            trade.TransactionId,
            trade.OriginalTransactionId,
            quantity,
            trade.Price,
            trade.PortfolioAmount,
            trade.PortfolioCurrency,
            trade.UserAmount,
            run.Portfolio.UserCurrency,
            rate,
            userRate,
            trade.SourceCandleTime,
            trade.Time,
            run.Portfolio.Id,
            run.Id);

        return trade;
    }

    private async Task CompleteEntryAsync(NasdaqBounceRun run, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Applying entry transaction {TransactionId}; quantity {Quantity}, price {Price}, portfolio amount {PortfolioAmount} {PortfolioCurrency}. " +
            "Portfolio {PortfolioId}. Run {RunId}.",
            run.Entry.TransactionId,
            run.Entry.Quantity,
            run.Entry.Price,
            run.Entry.PortfolioAmount,
            run.Entry.PortfolioCurrency,
            run.Portfolio.Id,
            run.Id);

        await store.ApplyTradeAsync(run.Entry, cancellationToken);
        logger.LogInformation(
            "Entry transaction {TransactionId} applied; persisting position acknowledgement. Portfolio {PortfolioId}. Run {RunId}.",
            run.Entry.TransactionId,
            run.Portfolio.Id,
            run.Id);

        run.EntryApplied = true;

        var message = string.Concat(
            FormattableString.Invariant(
                $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); decline {run.Decline:F3}%; "),
            FormattableString.Invariant(
                $"allocation {run.Allocation:P0}; {run.Entry.Quantity:F6} shares at ${run.Entry.Price:F4}. "),
            FormattableString.Invariant(
                $"Cash used: {run.Entry.PortfolioAmount:F2} {run.Portfolio.Currency}. "),
            FormattableString.Invariant(
                $"Candle: {run.Entry.SourceCandleTime:O}; execution: {run.Entry.TransactionId}."));

        AddAlert(run, "buy", "PAPER BUY QQQ", message);

        await store.SaveRunAsync(run, cancellationToken);
        logger.LogInformation(
            "Entry completed and position persisted for transaction {TransactionId}; review date {ReviewDate}, exit deadline {ExitDeadline}. Portfolio " +
            "{PortfolioId}. Run {RunId}.",
            run.Entry.TransactionId,
            run.ReviewDate,
            run.ExitDeadline,
            run.Portfolio.Id,
            run.Id);
    }

    private async Task ExitAsync(
        NasdaqBounceRun run,
        NasdaqBounceMarketSnapshot snapshot,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var remaining = await store.GetRemainingQuantityAsync(run, cancellationToken);

        logger.LogInformation(
            "Exit sizing evaluated; remaining quantity {RemainingQuantity}, original entry quantity {EntryQuantity}, reason {ExitReason}, price {Price}, " +
            "candle {SourceCandleTime}. Portfolio {PortfolioId}. Run {RunId}.",
            remaining,
            run.Entry.Quantity,
            reason,
            snapshot.Price,
            snapshot.PriceTime,
            run.Portfolio.Id,
            run.Id);

        if (remaining == 0)
        {
            logger.LogInformation(
                "Position reconciliation selected: {Reason}. Portfolio {PortfolioId}. Run {RunId}.",
                "NoRemainingStrategyShares",
                run.Portfolio.Id,
                run.Id);

            run.Closed = true;
            var message = string.Concat(
                $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); ",
                "no remaining strategy shares to sell. ",
                $"Position {run.Id} reconciled.");

            AddAlert(run, "externally-closed", "QQQ position already closed", message);

            await store.SaveRunAsync(run, cancellationToken);
            logger.LogInformation(
                "Position reconciled and closure persisted without a sale. Portfolio {PortfolioId}. Run {RunId}.",
                run.Portfolio.Id,
                run.Id);

            return;
        }

        run.Exit = await CreateTradeAsync(run, snapshot, -remaining, now, "sell", cancellationToken);
        run.ExitReason = reason;

        await store.SaveRunAsync(run, cancellationToken);
        logger.LogInformation(
            "Exit intent persisted for transaction {TransactionId}; reason {ExitReason}. Portfolio {PortfolioId}. Run {RunId}.",
            run.Exit.TransactionId,
            reason,
            run.Portfolio.Id,
            run.Id);

        await CompleteExitAsync(run, cancellationToken);
    }

    private async Task CompleteExitAsync(NasdaqBounceRun run, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Applying exit transaction {TransactionId}; quantity {Quantity}, price {Price}, portfolio amount {PortfolioAmount} {PortfolioCurrency}, " +
            "reason {ExitReason}. Portfolio {PortfolioId}. Run {RunId}.",
            run.Exit!.TransactionId,
            run.Exit.Quantity,
            run.Exit.Price,
            run.Exit.PortfolioAmount,
            run.Exit.PortfolioCurrency,
            run.ExitReason,
            run.Portfolio.Id,
            run.Id);

        await store.ApplyTradeAsync(run.Exit!, cancellationToken);
        logger.LogInformation(
            "Exit transaction {TransactionId} applied; persisting position closure. Portfolio {PortfolioId}. Run {RunId}.",
            run.Exit.TransactionId,
            run.Portfolio.Id,
            run.Id);

        run.Closed = true;

        // Sales store negative amounts, so subtracting the sale and entry costs gives realised P&L.
        var quantity = -run.Exit!.Quantity;
        var entryCost = run.Entry.PortfolioAmount * quantity / run.Entry.Quantity;
        var usdPnl = quantity * (run.Exit.Price - run.Entry.Price);
        var pnl = -run.Exit.PortfolioAmount - entryCost;
        var percent = pnl / entryCost * 100m;

        var message = string.Concat(
            FormattableString.Invariant(
                $"Portfolio {run.Portfolio.Name} ({run.Portfolio.Id}); {-run.Exit.Quantity:F6} shares at ${run.Exit.Price:F4}. "),
            FormattableString.Invariant(
                $"Realised P&L: {pnl:+0.00;-0.00;0.00} {run.Portfolio.Currency} ({percent:+0.00;-0.00;0.00}%); "),
            FormattableString.Invariant(
                $"USD P&L: {usdPnl:+0.00;-0.00;0.00}. Reason: {run.ExitReason}. "),
            FormattableString.Invariant(
                $"Candle: {run.Exit.SourceCandleTime:O}; execution: {run.Exit.TransactionId}."));

        AddAlert(run, "sell", "PAPER SELL QQQ", message);

        await store.SaveRunAsync(run, cancellationToken);
        logger.LogInformation(
            "Exit completed and closure persisted for transaction {TransactionId}; realised P&L {RealisedProfitLoss} {PortfolioCurrency}, return " +
            "{ReturnPercent}, USD P&L {UsdProfitLoss}, reason {ExitReason}. Portfolio {PortfolioId}. Run {RunId}.",
            run.Exit.TransactionId,
            pnl,
            run.Portfolio.Currency,
            percent,
            usdPnl,
            run.ExitReason,
            run.Portfolio.Id,
            run.Id);
    }

    private void AddAlert(NasdaqBounceRun run, string key, string subject, string message)
    {
        if (!run.Alerts.Any(alert => alert.Key == key))
        {
            run.Alerts.Add(new NasdaqBounceAlert {
                Key = key,
                Subject = subject,
                Message = message
            });
            logger.LogInformation(
                "Notification queued with alert key {AlertKey}; pending alerts {PendingAlertCount}. Portfolio {PortfolioId}. Run {RunId}.",
                key,
                run.Alerts.Count(alert => !alert.Sent),
                run.Portfolio.Id,
                run.Id);
        }
        else
        {
            logger.LogDebug(
                "Duplicate notification suppressed for alert key {AlertKey}. Portfolio {PortfolioId}. Run {RunId}.",
                key,
                run.Portfolio.Id,
                run.Id);
        }
    }

    private async Task FlushAsync(NasdaqBounceRun run, CancellationToken cancellationToken)
    {
        foreach (var alert in run.Alerts.Where(notification => !notification.Sent))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                logger.LogInformation(
                    "Attempting notification delivery for alert key {AlertKey}. Portfolio {PortfolioId}. Run {RunId}.",
                    alert.Key,
                    run.Portfolio.Id,
                    run.Id);

                if (!await notifications.SendTradeNotificationAsync(alert.Subject, alert.Message, cancellationToken))
                {
                    logger.LogWarning(
                        "Notification delivery deferred: {Reason}; alert key {AlertKey}. Remaining alerts will retry on a later tick. Portfolio {PortfolioId}. Run " +
                        "{RunId}.",
                        "NotificationNotAccepted",
                        alert.Key,
                        run.Portfolio.Id,
                        run.Id);
                    break;
                }

                alert.Sent = true;

                await store.SaveRunAsync(run, cancellationToken);
                logger.LogInformation(
                    "Notification delivered and acknowledgement persisted for alert key {AlertKey}. Portfolio {PortfolioId}. Run {RunId}.",
                    alert.Key,
                    run.Portfolio.Id,
                    run.Id);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "QQQ notification {AlertKey} remains pending for {RunId}. Portfolio {PortfolioId}.",
                    alert.Key,
                    run.Id,
                    run.Portfolio.Id);
                break;
            }
        }
    }
}
