using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum HoldingSource
{
    Strategy = 0,

    Manual = 1
}
