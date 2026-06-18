namespace Shared.Enums;

/// <summary>
/// Types of progress updates during analysis
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum ProgressType
{
    Started = 0,

    PeriodStarted = 1,
    
    PeriodCompleted = 2,

    GeneratingSummary = 3,

    Completed = 4,

    Error = 5,

    Cancelled = 6,

    InProgress = 7,

    Failed = 8
}