# QQQ overnight bounce

Both timers run Monday to Friday in UTC. `NasdaqBounceBuy` polls each minute from 16:00 through 20:59 UTC, the year-round window that can contain the close-minus-30-minute entry (including early closes). `NasdaqBounceSell` polls each minute from 13:00 through 20:59 UTC, covering regular NASDAQ sessions in both EST and EDT. The strategy uses New York exchange-local time and Market Data closure records to skip holidays, early-close periods outside the valid window, and times outside its entry, review, and exit rules. No host timezone setting is required. All executions are paper records, including portfolios currently labelled `Live`. There is no broker connection.

## Behaviour

- Strategy ID: `nasdaq-bounce-overnight`, catalogue version `1.0.0`.
- Entry: 30 minutes before regular-session close (normally 15:30 New York; 12:30 for a 13:00 early close). The market-time endpoint must also report an open regular session.
- Decline is `(previous regular-session close - current price) / previous close * 100`. Inclusive thresholds of 1%, 1.5%, and 2% allocate 25%, 40%, and 50% of available cash respectively.
- The first usable entry signal is saved once per trading day, including no-buy decisions. Subsequent minute ticks retry that signal for five minutes; they do not evaluate new signals. One entry is permitted per portfolio/day.
- Quantity is rounded down to six decimal places. Prices are USD; cash movements use verified Market Data FX in portfolio currency. User-currency transaction costs are recorded separately. Fees and slippage are zero.
- The next **trading** day is determined using holidays and weekends. AI reviews start at 10:00 New York and repeat every 30 minutes. Today's 15-minute candles, current price and timestamps are sent to Foundry; no portfolio or user data is sent.
- A validated support-break decision sells the remaining strategy quantity. Invalid, unavailable, or insufficient AI output leaves the position open. Review outcomes and prompt version are persisted and shared across portfolios.
- At 30 minutes before that day's close, the sell Function bypasses AI and exits regardless of P&L. A new qualifying entry must wait until the previous sale finishes, then uses the refreshed cash balance.
- Missing prices, calendar uncertainty, closed markets or missing FX never produce a fabricated execution. Overdue exits remain pending and retry on subsequent ticks while the regular session is open. They continue on the next trading session if necessary.

## Configuration

| Setting | Default |
| --- | --- |
| `NasdaqBounce__EntriesEnabled` | `false` |
| `NasdaqBounce__FoundryEndpoint` | `https://market-data-foundry.cognitiveservices.azure.com/` |
| `NasdaqBounce__FoundryDeployment` | `gpt-6-luna` |
| `NasdaqBounce__MaximumCandleAgeMinutes` | `35` |
| `NasdaqBounce__FoundryTimeoutSeconds` | `45` |

`CandleApi__BaseUrl`, `KEYVAULT_URI`, `AZURE_CLIENT_ID`, the existing database settings and `REPORT_BLOBS_CONNECTIONSTRING` continue to apply. The storage setting accepts either the connection string itself or the name of a `ConnectionStrings` entry. Bicep now supplies the canonical database endpoint/name settings consumed by the service layer.

The existing Key Vault secrets `MARKET-DATA-FUNCTIONS-API-KEY` and `SLACK-SCREENER-WEBHOOKURL` are reused. The Function App's configured user-assigned managed identity needs access to those secrets and the **Cognitive Services OpenAI User** role on `market-data-foundry`. Foundry uses the pinned .NET SDK dependencies in `Services.csproj`; it does not create agents or model deployments.

The latest returned intraday candle close is the paper execution price, including a forming candle if the API supplies one. Its timestamp is retained on the execution intent and transaction. A price must belong to today's regular session and cannot be future-dated or older than the configured maximum candle age. Source bar time is distinct from trade-recording time; these are paper fills, not confirmed market fills.

## Persistence and recovery

The `StrategyExecutions` Cosmos container uses `/PartitionKey`. Position documents are partitioned by portfolio; daily signals and shared AI reviews have separate partitions in the same container. The existing data storage account holds `strategy-locks`, created lazily on first acquisition. Both Functions use the same renewable 60-second portfolio lease, renewed every 20 seconds; loss of the lease cancels protected work.

An immutable intent is persisted before portfolio mutation. Cash, transaction, holding and original-purchase remaining quantity are committed separately, each with a deterministic transaction ID and an idempotency marker in the affected document. ETag concurrency checks prevent overwriting another writer's newer document. A restart replays unfinished steps rather than debiting or crediting cash twice. An entry that never started is cancelled when its five-minute window expires; an entry whose cash movement already committed is always reconciled.

Trade and operational alerts are stored with the position. Trading for all portfolios finishes before Slack delivery is attempted. Delivery bypasses market-hours suppression and retries even after positions close or associations change. Delivery is at least once: if Slack accepts a message and persisting its acknowledgement fails, a retry can repeat it. The execution ID included in each trade message identifies the event.

Sale notifications include the matched entry cost, realised P&L and percentage in portfolio currency, USD P&L, quantity, price, timestamps and exit reason. After a manual partial sale, the automatic-exit notification reports P&L on the remaining shares it sells. Manual holdings and positions owned by other strategies are excluded from automatic liquidation.

## Validation and release

Local checks, with synthetic portfolios and mocked HTTP/AI/Slack boundaries:

```powershell
dotnet test Tests/Strategy.Tests/Strategy.Tests.csproj -m:1
dotnet build Backend/Backend.csproj -m:1
az bicep build --file Infra/Environments/Test/main.bicep --stdout
```

No deployment or activation is performed by these tests. The existing `Services.Tests` suite has Docker/cloud setup and is separate from these isolated checks.

Before an explicitly authorised TEST activation:

1. Apply the infrastructure changes so the execution container and canonical settings exist. Verify identity access to Key Vault, storage, Cosmos, and the existing Foundry deployment.
2. Run the existing CLI catalogue migrations against the intended TEST database: `dotnet run --project CLI -- database migrate`. The Nasdaq bounce migration is idempotent and does not create portfolios or enable entries.
3. Associate synthetic portfolios through the existing portfolio API using strategy ID `nasdaq-bounce-overnight`.
4. Verify Market Data calendar coverage (including early closes), 15-minute candle freshness, FX, and the existing Slack destination. Verify the Foundry deployment supports structured chat outputs in a separately authorised live smoke test.
5. Enable `NasdaqBounce__EntriesEnabled` only for the authorised TEST run. Observe trade IDs, persisted execution/review records and Slack P&L messages.

To stop new entries, set `NasdaqBounce__EntriesEnabled=false`. Keep `NasdaqBounceSell` running: it manages already-persisted positions independently of that switch, strategy status and current portfolio associations. Do not delete execution documents or application markers while reconciliation or notifications are pending.

Monitor Function failures, lease-renewal failures, pending execution errors, failed AI reviews, unsent alerts and positions beyond their stored exit deadline. Recover data or permissions first; the next timer tick retries persisted work. No live Foundry calls, Slack messages, migrations, deployments or strategy activation were performed as part of implementation verification.
