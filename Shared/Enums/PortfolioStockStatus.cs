using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PortfolioStockStatus
{
    Active = 0,

    PartiallySold = 1,

    FullySold = 2
}
