using System.Globalization;
using System.Text.Json;

namespace Shared.Converters;

/// <summary>
/// JSON converter that ensures all DateTime values are treated as UTC
/// to avoid timezone conversion errors.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateTimeString = reader.GetString();

        if (string.IsNullOrEmpty(dateTimeString))
        {
            return default;
        }

        if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime))
        {
            // If the parsed DateTime has no timezone info, treat it as UTC
            return dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
        }

        throw new JsonException($"Cannot parse DateTime: {dateTimeString}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Always write DateTime as UTC with ISO format
        var utcValue = DateTimeHandler.ConvertToUtc(value);
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}
