using System.Net;
using System.Text;
using Azure;
using Azure.Security.KeyVault.Secrets;
using ChrisUsher.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Services.Clients;
using Services.Clients.Notifications;
using Services.Strategies.NasdaqBounce;
using Shared.Config;
using Shared.Interfaces;

namespace Strategy.Tests;

public class NasdaqBounceClientTests
{
    private static readonly Guid StockId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);

            return Task.FromResult(response(request));
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static SecretClient Secrets()
    {
        var mock = new Mock<SecretClient>();
        mock.Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SecretContentType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new KeyVaultSecret("synthetic", "synthetic-key"), Mock.Of<Response>()));

        return mock.Object;
    }

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CandleApi:BaseUrl"] = "https://market-data.invalid/api"
        })
        .Build();

    [TestCase("2026-10-23T13:45:00Z", "USD", true)]
    [TestCase("2026-10-23T13:45:00", "USD", true)]
    [TestCase("2026-10-23T13:00:00Z", "USD", false)]
    [TestCase("2026-10-23T13:45:00Z", "GBP", false)]
    public async Task AdapterUsesLiveFifteenMinuteCloseAndRejectsInvalidData(string candleTime, string currency, bool succeeds)
    {
        using var handler = new Handler(request => request.RequestUri!.AbsolutePath.Contains("/stocks/QQQ")
            ? Json($$"""{"stockId":"{{StockId}}","symbol":"QQQ","exchangeName":"NASDAQ","currencyCode":"{{currency}}"}""")
            : request.RequestUri!.Query.Contains("interval=daily")
                ? Json("""{"candles":[{"DateTime":"2026-10-22T00:00:00Z","Open":100,"High":101,"Low":99,"Close":100}]}""")
                : Json($$"""
                    {
                      "candles": [
                        {
                          "DateTime": "{{candleTime}}",
                          "Open": 100,
                          "High": 100,
                          "Low": 97,
                          "Close": 98
                        },
                        {
                          "DateTime": "2026-10-23T14:15:00Z",
                          "Open": 70,
                          "High": 70,
                          "Low": 70,
                          "Close": 70
                        }
                      ]
                    }
                    """));
        using var http = new HttpClient(handler);
        var secrets = Secrets();
        var config = Config();
        var cache = Mock.Of<ICacheService>();
        var adapter = new NasdaqBounceMarketData(
            new StockClient(http, secrets, NullLogger<StockClient>.Instance, config),
            new CandleClient(http, secrets, NullLogger<CandleClient>.Instance, config, cache, TimeProvider.System),
            new CalendarClient(http, secrets, NullLogger<CalendarClient>.Instance, config, cache, TimeProvider.System),
            new FxRateClient(http, secrets, NullLogger<FxRateClient>.Instance, config, cache, TimeProvider.System),
            new NasdaqBounceOptions());
        var session = new NasdaqBounceSession(
            new DateOnly(2026, 10, 23),
            DateTimeOffset.Parse("2026-10-23T13:30:00Z"),
            DateTimeOffset.Parse("2026-10-23T20:00:00Z"));

        if (!succeeds)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.GetSnapshotAsync(
                session,
                new DateOnly(2026, 10, 22),
                DateTimeOffset.Parse("2026-10-23T14:00:00Z"),
                default));

            return;
        }
        var snapshot = await adapter.GetSnapshotAsync(
            session,
            new DateOnly(2026, 10, 22),
            DateTimeOffset.Parse("2026-10-23T14:00:00Z"),
            default);
        Assert.That(snapshot.Price, Is.EqualTo(98));
        Assert.That(snapshot.PreviousClose, Is.EqualTo(100));
        Assert.That(snapshot.PriceTime, Is.EqualTo(DateTimeOffset.Parse("2026-10-23T13:45:00Z")));
        Assert.That(snapshot.Candles.Count, Is.EqualTo(1));
        Assert.That(handler.Requests.Any(x => x.Query.Contains("interval=15m")), Is.True);
    }

    [Test]
    public async Task CalendarErrorsFailClosedAndClosuresDoNotBecomeEmptySuccess()
    {
        using var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("")
        });
        using var http = new HttpClient(handler);
        var client = new CalendarClient(
            http,
            Secrets(),
            NullLogger<CalendarClient>.Instance,
            Config(),
            Mock.Of<ICacheService>(),
            TimeProvider.System);
        Assert.That((await client.GetMarketTimeAsync()).IsClosed, Is.True);
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetCloseDatesAsync("US", 2026));
    }

    [Test]
    public async Task TradeSlackBypassesCalendarAndUsesExistingSecret()
    {
        using var calendarHandler = new Handler(_ => throw new AssertionException("Trade alert must not query calendar"));
        using var calendarHttp = new HttpClient(calendarHandler);
        var secret = new Mock<ISecretClient>();
        secret.Setup(x => x.GetSecretAsync("SLACK-SCREENER-WEBHOOKURL")).ReturnsAsync("https://slack.invalid/webhook");
        using var webhookHandler = new Handler(_ => Json("ok"));
        using var webhookHttp = new HttpClient(webhookHandler);
        using var client = new SlackNotificationClient(
            secret.Object,
            new CalendarClient(
                calendarHttp,
                Secrets(),
                NullLogger<CalendarClient>.Instance,
                Config(),
                Mock.Of<ICacheService>(),
                TimeProvider.System),
            NullLoggerFactory.Instance,
            webhookHttp);
        Assert.That(await client.SendTradeNotificationAsync("PAPER SELL QQQ", "P&L: -10 USD"), Is.True);
        Assert.That(webhookHandler.Requests.Count, Is.EqualTo(1));
        Assert.That(calendarHandler.Requests, Is.Empty);
        secret.Verify(x => x.GetSecretAsync("SLACK-SCREENER-WEBHOOKURL"), Times.Once);
    }
}
