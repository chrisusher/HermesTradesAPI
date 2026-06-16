using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum StockType
{
    Unknown = 0,

    Equity = 1,
    
    ETF = 2
}
