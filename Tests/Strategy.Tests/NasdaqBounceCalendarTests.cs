using NUnit.Framework;
using Services.Strategies.NasdaqBounce;
using Shared.Calendar;

namespace Strategy.Tests;

public class NasdaqBounceCalendarTests
{
    [TestCase("2026-03-06", "2026-03-06T20:30:00Z")]
    [TestCase("2026-03-09", "2026-03-09T19:30:00Z")]
    [TestCase("2026-10-30", "2026-10-30T19:30:00Z")]
    [TestCase("2026-11-02", "2026-11-02T20:30:00Z")]
    public async Task DeadlineTracksNewYorkDaylightSaving(string date, string expected)
    {
        var session = await new NasdaqBounceCalendar(new FakeMarket()).GetSessionAsync(DateOnly.Parse(date), default);
        Assert.That(session!.Deadline, Is.EqualTo(DateTimeOffset.Parse(expected)));
    }

    [Test]
    public async Task EarlyCloseIsLocalWallTimeAndHolidayIsExcluded()
    {
        var market = new FakeMarket();
        market.Closures.Add(new CloseDate {
            Date = new DateTime(2026, 11, 27, 13, 0, 0)
        });
        market.Closures.Add(new CloseDate {
            Date = new DateTime(2026, 11, 26)
        });

        var calendar = new NasdaqBounceCalendar(market);

        var early = await calendar.GetSessionAsync(new DateOnly(2026, 11, 27), default);
        Assert.That(early!.Deadline, Is.EqualTo(DateTimeOffset.Parse("2026-11-27T17:30:00Z")));
        Assert.That(await calendar.GetSessionAsync(new DateOnly(2026, 11, 26), default), Is.Null);
        Assert.That(await calendar.GetSessionAsync(new DateOnly(2026, 11, 28), default), Is.Null);
    }

    [Test]
    public async Task NextSessionCrossesWeekendHolidayAndYearBoundary()
    {
        var market = new FakeMarket();
        market.Closures.Add(new CloseDate {
            Date = new DateTime(2027, 1, 1)
        });
        var calendar = new NasdaqBounceCalendar(market);
        Assert.That((await calendar.AdjacentAsync(new DateOnly(2026, 12, 31), 1, default)).Date, Is.EqualTo(new DateOnly(2027, 1, 4)));
        Assert.That((await calendar.AdjacentAsync(new DateOnly(2026, 10, 23), 1, default)).Date, Is.EqualTo(new DateOnly(2026, 10, 26)));
        Assert.That((await calendar.AdjacentAsync(new DateOnly(2027, 1, 4), -1, default)).Date, Is.EqualTo(new DateOnly(2026, 12, 31)));
    }

    [Test]
    public async Task FailedCalendarDoesNotBecomeAnOpenDay()
    {
        var market = new FakeMarket {
            FailCalendar = true
        };
        var calendar = new NasdaqBounceCalendar(market);
        await Assert.ThrowsAsync<HttpRequestException>(() => calendar.GetSessionAsync(new DateOnly(2026, 10, 22), default));
        market.FailCalendar = false;
        Assert.That(await calendar.GetSessionAsync(new DateOnly(2026, 10, 22), default), Is.Not.Null);
    }

    [Test]
    public async Task ReviewsStartAtTenAndStopAtDeadline()
    {
        var session = (await new NasdaqBounceCalendar(new FakeMarket()).GetSessionAsync(new DateOnly(2026, 10, 23), default))!;

        Assert.That(NasdaqBounceRules.ReviewSlot(session, session.Open.AddMinutes(29)), Is.Null);
        Assert.That(NasdaqBounceRules.ReviewSlot(session, session.Open.AddMinutes(30)), Is.EqualTo(session.Open.AddMinutes(30)));
        Assert.That(NasdaqBounceRules.ReviewSlot(session, session.Open.AddMinutes(59)), Is.EqualTo(session.Open.AddMinutes(30)));
        Assert.That(NasdaqBounceRules.ReviewSlot(session, session.Open.AddMinutes(60)), Is.EqualTo(session.Open.AddMinutes(60)));
        Assert.That(NasdaqBounceRules.ReviewSlot(session, session.Deadline), Is.Null);
    }

    [Test]
    public void SizingRejectsMissingCashPriceAndFx()
    {
        Assert.Multiple(() => {
            Assert.That(NasdaqBounceRules.Quantity(0, .5m, 100, 1), Is.Zero);
            Assert.That(NasdaqBounceRules.Quantity(1000, .5m, 0, 1), Is.Zero);
            Assert.That(NasdaqBounceRules.Quantity(1000, .5m, 100, 0), Is.Zero);
            Assert.That(NasdaqBounceRules.Quantity(-1, .5m, 100, 1), Is.Zero);
        });
    }
}
