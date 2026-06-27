using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Services.Clients;

namespace Services.Tests;

[TestFixture]
public class MarketDataClientTracingTests
{
    [Test]
    public void StartExternalActivity_CreatesChildSpanUnderCurrentActivity()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Services.Clients.MarketDataClient",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        var configuration = new ConfigurationBuilder().Build();
        var client = new TestMarketDataClient(configuration);

        using var parentActivity = new Activity("parent-operation").Start();
        using var activity = client.StartTestActivity("https://example.test/api/data");

        Assert.That(activity, Is.Not.Null, "Expected activity to be created.");
        Assert.That(activity!.ParentId, Is.EqualTo(parentActivity.Id), "Expected activity to have the correct parent ID.");
        Assert.That(activity.ParentSpanId, Is.EqualTo(parentActivity.SpanId), "Expected activity to have the correct parent span ID.");
        Assert.That(activity.Kind, Is.EqualTo(ActivityKind.Client), "Expected activity to have the correct kind.");
    }

    private sealed class TestMarketDataClient : MarketDataClient
    {
        public TestMarketDataClient(IConfiguration configuration)
            : base(null!, configuration)
        {
        }

        public Activity? StartTestActivity(string endpoint)
            => StartExternalActivity("market-data-test", endpoint);
    }
}
