using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum OrderType
{
    Unknown = 0,

    LIMIT_BUY = 1,

    MARKET_BUY = 2,

    MARKET_SELL = 3,

    STOP_SELL = 4,

    STOP_LIMIT_BUY = 5,

    INTEREST = 6,

    DIVIDEND = 7,

    LIMIT_SELL = 8,

    STOP_BUY = 9,
}
