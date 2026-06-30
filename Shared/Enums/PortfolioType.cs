namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum PortfolioType
{
    Live = 0,

    Paper = 1,
}
