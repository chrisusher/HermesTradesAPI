using System.Text.Json;
using Shared.Converters;

namespace Shared;

public class SharedCommon
{
    private static JsonSerializerOptions? _jsonOptions;

    public static JsonSerializerOptions JsonOptions
    {
        get
        {
            if (_jsonOptions is null)
            {
                _jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                };

                _jsonOptions.Converters.Add(new JsonStringEnumConverter());
                
                // Add custom DateTime converter to handle timezone issues
                _jsonOptions.Converters.Add(new UtcDateTimeConverter());
            }

            return _jsonOptions;
        }
    }
}
