using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum OrderGroupType
{
    All = 0,

    Buys = 1,

    Sales = 2,
}
