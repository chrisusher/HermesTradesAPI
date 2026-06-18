using Newtonsoft.Json.Converters;

namespace Shared.Enums;

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ReportType
{
    Unknown = 0,

    StrategyPerformance = 1,

    SinglePortfolioROI = 2,

    BacktestROI = 3,

    AllPortfolioROIs = 4,
}
