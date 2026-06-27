namespace Services.Tests;

[TestFixture]
public class OrderTypeExtensionsTests
{
    [Test]
    public void GetSupportedOrderTypes_ExcludesUnknownAndReturnsExpectedValues()
    {
        var orderTypes = OrderTypeExtensions.GetSupportedOrderTypes();

        Assert.That(orderTypes, Does.Contain("LIMIT_BUY"));
        Assert.That(orderTypes, Does.Contain("MARKET_SELL"));
        Assert.That(orderTypes, Does.Not.Contain("Unknown"));
        Assert.That(orderTypes, Has.Count.EqualTo(Enum.GetValues<OrderType>().Length - 1));
    }
}
