namespace Shared.Enums;

public static class OrderTypeExtensions
{
    public static IReadOnlyList<string> GetSupportedOrderTypes()
    {
        return Enum.GetValues<OrderType>()
            .Where(orderType => orderType != OrderType.Unknown)
            .Select(orderType => orderType.ToString())
            .ToArray();
    }
}
