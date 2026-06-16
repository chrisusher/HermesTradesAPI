using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum CurrencyCode
{
    Unknown = 0,

    USD = 1,

    EUR = 2,

    GBP = 3,

    GBX = 4,

    CAD = 5,

    CHF = 6,
}
