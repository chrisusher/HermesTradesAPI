using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TradingTimeType
{
    Unknown = 0,

    OutOfHours = 1,

    Open = 2,

    PreMarket = 3,

    PostMarket = 4,

    Weekend = 5,

    Closed = 6
}
