using Services.Clients;
using ChrisUsher.Core.Shared.Converters;
using Shared.Calendar;
using Shared.Config;

namespace Services.Strategies.NasdaqBounce;

public sealed class NasdaqBounceMarketData(StockClient stocks, CandleClient candles, CalendarClient calendar,
    FxRateClient fx, NasdaqBounceOptions options) : INasdaqBounceMarketData
{
    public Task<List<CloseDate>> GetClosuresAsync(int year, CancellationToken cancellationToken) =>
        calendar.GetCloseDatesAsync("US", year, cancellationToken);

    public async Task<bool> IsOpenAsync(CancellationToken cancellationToken)
    {
        var result = await calendar.GetMarketTimeAsync("US", cancellationToken);

        return !result.IsClosed && result.TradingTime == TradingTimeType.Open;
    }

    public async Task<NasdaqBounceMarketSnapshot> GetSnapshotAsync(NasdaqBounceSession session, DateOnly previousSession,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stock = await stocks.GetStockAsync("QQQ", cancellationToken)
            ?? throw new InvalidOperationException("QQQ metadata is unavailable.");

        if (stock.CurrencyCode != CurrencyCode.USD || stock.StockId == Guid.Empty)
        {
            throw new InvalidOperationException("QQQ must have a stock ID and USD prices.");
        }

        var intraday = await candles.GetCandlesAsync(stock.StockId, session.Open.UtcDateTime,
            now.UtcDateTime, "15m", cancellationToken);

        foreach (var candle in intraday)
        {
            candle.Date = DateTimeHandler.ConvertToUtc(candle.Date);
        }

        var valid = intraday
            .Where(x => new DateTimeOffset(x.Date) >= session.Open && new DateTimeOffset(x.Date) <= now)
            .OrderBy(x => x.Date).ToList();

        var latest = valid.LastOrDefault() ?? throw new InvalidOperationException("No current-session QQQ candles.");
        var priceTime = new DateTimeOffset(latest.Date);

        if (latest.Close <= 0 || now - priceTime > TimeSpan.FromMinutes(options.MaximumCandleAgeMinutes) ||
            valid.Any(x => x.Open <= 0 || x.Low <= 0 || x.High < x.Low || x.Close < x.Low || x.Close > x.High))
        {
            throw new InvalidOperationException("Invalid or stale QQQ candles.");
        }

        var from = DateTimeHandler.ConvertToUtc(previousSession.ToDateTime(TimeOnly.MinValue));
        var daily = await candles.GetCandlesAsync(stock.StockId, from, from.AddDays(1).AddTicks(-1), "daily", cancellationToken);
        
        var prior = daily.LastOrDefault(x => DateOnly.FromDateTime(x.Date) == previousSession);

        if (prior is null || prior.Close <= 0)
        {
            throw new InvalidOperationException("Previous session close is unavailable.");
        }

        return new NasdaqBounceMarketSnapshot(stock.StockId, stock.ExchangeName, prior.Close, latest.Close, priceTime, valid);
    }

    public async Task<decimal> GetUsdRateAsync(CurrencyCode currency, CancellationToken cancellationToken)
    {
        if (currency == CurrencyCode.USD)
        {
            return 1m;
        }

        if (currency == CurrencyCode.Unknown)
        {
            throw new InvalidOperationException("Portfolio/user currency is unknown.");
        }
        var rate = await fx.GetConversionRateAsync(CurrencyCode.USD, currency, cancellationToken);

        return rate is > 0 ? rate.Value : throw new InvalidOperationException("Market Data FX rate is unavailable.");
    }
}
