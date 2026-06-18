namespace Shared.Data;

public static class StaticData
{
    public static bool IsBuyOrder(OrderType orderType)
    {
        switch (orderType)
        {
            case OrderType.LIMIT_BUY:
            case OrderType.MARKET_BUY:
            case OrderType.STOP_LIMIT_BUY:
            case OrderType.STOP_BUY:
                return true;
            default:
                return false;
        }
    }

    public static (string exchangeName, string symbol) ParseCombinedSymbol(string combinedSymbol)
    {
        var parts = combinedSymbol.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (string.Empty, combinedSymbol);
    }

    /// <summary>
    /// Ensures a DateTime has Kind=Utc, which is required by PostgreSQL with timestamp with time zone.
    /// If the DateTime has Kind=Unspecified, it treats it as UTC.
    /// </summary>
    public static DateTime SpecifyUtcKind(DateTime dateTime)
    {
        return dateTime.Kind is DateTimeKind.Utc
            ? dateTime
            : new DateTime(dateTime.Ticks, DateTimeKind.Utc);
    }
}
