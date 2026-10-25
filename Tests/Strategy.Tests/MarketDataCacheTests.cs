using System.Net;
using Azure;
using Azure.Security.KeyVault.Secrets;
using ChrisUsher.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Services.Clients;

namespace Strategy.Tests;

public class MarketDataCacheTests
{
    private readonly TestClock _clock = new();
    private MemoryCache _cache = null!;
    private Mock<SecretClient> _secrets = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _clock.Now = new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.Zero);
        _cache = new MemoryCache(_clock);
        _secrets = new Mock<SecretClient>();
        _secrets.Setup(secret => secret.GetSecretAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new KeyVaultSecret("test", "test-key"), Mock.Of<Response>()));
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CandleApi:BaseUrl"] = "https://market-data.invalid/api"
        }).Build();
    }

    [Test]
    public async Task FxRatesAreSharedAcrossClientsAndConversionAmountsUntilMidnight()
    {
        using var handler = new Handler(_ => Json("0.8"));
        using var http = new HttpClient(handler);
        var firstClient = Fx(http);
        Assert.That(await firstClient.GetConversionRateAsync(CurrencyCode.USD, CurrencyCode.GBP), Is.EqualTo(0.8m));
        Assert.That(await Fx(http).ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.GBP), Is.EqualTo(80m));
        Assert.That(await Fx(http).ConvertAsync(200m, CurrencyCode.USD, CurrencyCode.GBP), Is.EqualTo(160m));
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        Assert.That(_cache.LastTtl, Is.EqualTo(TimeSpan.FromHours(10)));
        _secrets.Verify(secret => secret.GetSecretAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()), Times.Once);
        _clock.Now = new DateTimeOffset(_clock.Now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        await firstClient.GetConversionRateAsync(CurrencyCode.USD, CurrencyCode.GBP);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task DatedFxRatesKeepDateAndDirectionSeparateAndUseUnitAmounts()
    {
        using var handler = new Handler(_ => Json("0.8"));
        using var http = new HttpClient(handler);
        var client = Fx(http);
        var date = new DateTime(2026, 10, 20);
        Assert.That(await client.ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.GBP, date), Is.EqualTo(80m));
        Assert.That(await client.ConvertAsync(200m, CurrencyCode.USD, CurrencyCode.GBP, date.AddHours(4)), Is.EqualTo(160m));
        await client.ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.GBP, date.AddDays(1));
        await client.ConvertAsync(100m, CurrencyCode.GBP, CurrencyCode.USD, date);
        Assert.That(handler.Requests, Has.Count.EqualTo(3));
        Assert.That(handler.Requests.All(uri => uri.Query.Contains("amount=1&")), Is.True);
    }

    [TestCase("invalid")]
    [TestCase("")]
    [TestCase("null")]
    [TestCase("0")]
    [TestCase("-1")]
    public async Task InvalidFxRatesAreRetried(string payload)
    {
        var requestCount = 0;
        using var handler = new Handler(_ => Json(++requestCount == 1 ? payload : "0.8"));
        using var http = new HttpClient(handler);
        var client = Fx(http);
        Assert.That(await client.ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.GBP), Is.EqualTo(100m));
        Assert.That(await client.ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.GBP), Is.EqualTo(80m));
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task FailedFxRequestsAreRetriedAndSameCurrencyNeedsNoHttp()
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var http = new HttpClient(handler);
        var client = Fx(http);
        Assert.That(await client.GetConversionRateAsync(CurrencyCode.USD, CurrencyCode.GBP), Is.Null);
        Assert.That(await client.GetConversionRateAsync(CurrencyCode.USD, CurrencyCode.GBP), Is.Null);
        Assert.That(await client.ConvertAsync(100m, CurrencyCode.USD, CurrencyCode.USD), Is.EqualTo(100m));
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ClosuresAreCachedByCountryAndYearAndRefreshNextDay()
    {
        using var handler = new Handler(_ => Json("[]"));
        using var http = new HttpClient(handler);
        await Calendar(http).GetCloseDatesAsync("us", 2026);
        await Calendar(http).GetCloseDatesAsync("US", 2026);
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        await Calendar(http).GetCloseDatesAsync("GB", 2026);
        await Calendar(http).GetCloseDatesAsync("US", 2027);
        Assert.That(handler.Requests, Has.Count.EqualTo(3));
        _clock.Now = _clock.Now.AddDays(1);
        await Calendar(http).GetCloseDatesAsync("US", 2026);
        Assert.That(handler.Requests, Has.Count.EqualTo(4));
    }

    [TestCase("null")]
    [TestCase("")]
    [TestCase("invalid")]
    public async Task InvalidClosuresAreRetried(string payload)
    {
        var requestCount = 0;
        using var handler = new Handler(_ => Json(++requestCount == 1 ? payload : "[]"));
        using var http = new HttpClient(handler);
        var client = Calendar(http);
        await Assert.CatchAsync<Exception>(async () => await client.GetCloseDatesAsync("US", 2026));
        Assert.That(await client.GetCloseDatesAsync("US", 2026), Is.Empty);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task MarketTimeStaysLiveAcrossSessionChanges()
    {
        var requestCount = 0;
        using var handler = new Handler(_ => Json(++requestCount == 1
            ? "{\"isClosed\":true}"
            : "{\"isClosed\":false}"));
        using var http = new HttpClient(handler);
        Assert.That((await Calendar(http).GetMarketTimeAsync()).IsClosed, Is.True);
        Assert.That((await Calendar(http).GetMarketTimeAsync()).IsClosed, Is.False);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    [TestCase("daily", -1, 1)]
    [TestCase("daily", 0, 2)]
    [TestCase("15m", -1, 2)]
    public async Task OnlyCompletedDailyCandleRangesAreCached(string interval, int endDayOffset, int expectedRequests)
    {
        using var handler = new Handler(_ => Json("{\"candles\":[{\"Close\":100}]}"));
        using var http = new HttpClient(handler);
        var stockId = Guid.NewGuid();
        var from = _clock.Now.UtcDateTime.Date.AddDays(-5);
        var to = _clock.Now.UtcDateTime.Date.AddDays(endDayOffset);
        await Candles(http).GetCandlesAsync(stockId, from, to, interval);
        await Candles(http).GetCandlesAsync(stockId, from, to, interval);
        Assert.That(handler.Requests, Has.Count.EqualTo(expectedRequests));
    }

    [Test]
    public async Task EmptyCandleResponsesAreRetried()
    {
        var requestCount = 0;
        using var handler = new Handler(_ => Json(++requestCount == 1
            ? "{\"candles\":[]}"
            : "{\"candles\":[{\"Close\":100}]}"));
        using var http = new HttpClient(handler);
        var client = Candles(http);
        var stockId = Guid.NewGuid();
        var from = _clock.Now.UtcDateTime.Date.AddDays(-5);
        var to = _clock.Now.UtcDateTime.Date.AddDays(-1);
        Assert.That(await client.GetCandlesAsync(stockId, from, to), Is.Empty);
        Assert.That(await client.GetCandlesAsync(stockId, from, to), Has.Count.EqualTo(1));
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
    }

    private FxRateClient Fx(HttpClient http)
    {
        return new FxRateClient(http, _secrets.Object, NullLogger<FxRateClient>.Instance, _configuration, _cache, _clock);
    }

    private CalendarClient Calendar(HttpClient http)
    {
        return new CalendarClient(http, _secrets.Object, NullLogger<CalendarClient>.Instance, _configuration, _cache, _clock);
    }

    private CandleClient Candles(HttpClient http)
    {
        return new CandleClient(http, _secrets.Object, NullLogger<CandleClient>.Instance, _configuration, _cache, _clock);
    }

    private static HttpResponseMessage Json(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            return Now;
        }
    }

    private sealed class MemoryCache(TestClock clock) : ICacheService
    {
        private readonly Dictionary<string, (object Value, DateTimeOffset Expires)> _entries = new();
        public TimeSpan LastTtl { get; private set; }

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            var value = _entries.TryGetValue(key, out var entry) && entry.Expires > clock.Now
                ? (T)entry.Value
                : null;

            return Task.FromResult(value);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
        {
            _entries[key] = (value, clock.Now.Add(ttl));
            LastTtl = ttl;

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _entries.Remove(key);

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(_entries.TryGetValue(key, out var entry) && entry.Expires > clock.Now);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            return Task.FromResult(respond(request));
        }
    }
}
