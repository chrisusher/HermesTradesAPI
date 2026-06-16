using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum StrategyStatus
{
    Unknown = 0,

    Active = 1,

    Disabled = 2,

    Pending = 3,
}
