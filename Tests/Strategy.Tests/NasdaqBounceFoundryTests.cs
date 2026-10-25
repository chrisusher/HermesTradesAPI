using NUnit.Framework;
using Services.Strategies.NasdaqBounce;
using Shared.DTOs.Yahoo;

namespace Strategy.Tests;

public class NasdaqBounceFoundryTests
{
    private static NasdaqBounceMarketSnapshot Snapshot(decimal price) => new(
        Guid.NewGuid(),
        "NASDAQ",
        100,
        price,
        DateTimeOffset.Parse("2026-10-23T14:00:00Z"),
        [
            new PeriodCandle
            {
                Date = DateTime.Parse("2026-10-23T13:30:00Z").ToUniversalTime(),
                Low = 99
            },
            new PeriodCandle
            {
                Date = DateTime.Parse("2026-10-23T13:45:00Z").ToUniversalTime(),
                Low = 99
            }
        ]);

    private const string Sell = """
        {
          "action": "Sell",
          "explanation": "Two support touches at 99; current price is below support.",
          "evidence": [
            {
              "time": "2026-10-23T13:30:00Z",
              "price": 99
            },
            {
              "time": "2026-10-23T13:45:00Z",
              "price": 99
            }
          ]
        }
        """;

    private static NasdaqBounceSession Session() => new(
        new DateOnly(2026, 10, 23),
        DateTimeOffset.Parse("2026-10-23T13:30:00Z"),
        DateTimeOffset.Parse("2026-10-23T20:00:00Z"));

    [Test]
    public void SupportedBreakIsAccepted()
    {
        var session = Session();

        var decision = FoundryNasdaqBounceSupportAnalyser.ParseAndValidate(
            Sell,
            Snapshot(98),
            session,
            DateTimeOffset.Parse("2026-10-23T14:00:00Z"));

        Assert.That(decision.Action, Is.EqualTo(SupportAction.Sell));
    }

    [TestCase("Unknown")]
    [TestCase("sell")]
    [TestCase("9")]
    [TestCase("0")]
    public void InvalidActionIsRejected(string action)
    {
        var session = Session();
        var response = Sell.Replace("\"Sell\"", $"\"{action}\"");

        Assert.Throws<InvalidOperationException>(() => FoundryNasdaqBounceSupportAnalyser.ParseAndValidate(
            response,
            Snapshot(98),
            session,
            DateTimeOffset.Parse("2026-10-23T14:00:00Z")));
    }

    [Test]
    public void FabricatedEvidenceOrPriceAboveSupportIsRejected()
    {
        var session = Session();
        var fabricatedEvidence = Sell.Replace("\"price\": 99", "\"price\": 101");

        Assert.Throws<InvalidOperationException>(() => FoundryNasdaqBounceSupportAnalyser.ParseAndValidate(
            fabricatedEvidence,
            Snapshot(98),
            session,
            DateTimeOffset.Parse("2026-10-23T14:00:00Z")));
        Assert.Throws<InvalidOperationException>(() => FoundryNasdaqBounceSupportAnalyser.ParseAndValidate(
            Sell,
            Snapshot(100),
            session,
            DateTimeOffset.Parse("2026-10-23T14:00:00Z")));
    }
}
