namespace Shared.Converters;

public class DateTimeHandler
{
    public static DateTime ConvertToUtc(DateTime date)
    {
        return date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
    }

    public static string GetQuarterDateString(DateTime date)
    {
        var quarter = (date.Month - 1) / 3 + 1;
        return $"{date.Year}-Q{quarter}";
    }

    public static string GetPreviousQuarterDateString(DateTime date)
    {
        var previousQuarterDate = date.AddMonths(-3);
        var quarter = (previousQuarterDate.Month - 1) / 3 + 1;
        return $"{previousQuarterDate.Year}-Q{quarter}";
    }
}
