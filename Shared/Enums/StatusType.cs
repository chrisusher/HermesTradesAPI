using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum StatusType
{
    Active = 0,

    Inactive = 1,

    Deleted = 2
}
