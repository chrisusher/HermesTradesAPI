using Newtonsoft.Json.Converters;

namespace Shared.Enums;

/// <summary>
/// Represents different candle types based on OHLCV analysis.
/// Classifications range from most bullish to most bearish patterns.
/// </summary>
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum CandleType
{
    /// <summary>
    /// Default value when candle type cannot be determined
    /// </summary>
    Neutral = 0,

    /// <summary>
    /// Large green body with minimal or no wicks - strongest bullish signal
    /// </summary>
    MostBullish = 1,

    /// <summary>
    /// Green body with small upper wick - strong bullish signal
    /// </summary>
    SecondMostBullish = 2,

    /// <summary>
    /// Green body with balanced wicks - moderate bullish signal
    /// </summary>
    NormalBullish = 3,

    /// <summary>
    /// Small green body with longer wicks - weak bullish signal
    /// </summary>
    NeutralBullish = 4,

    /// <summary>
    /// Small green body with very long wicks - minimal bullish signal
    /// </summary>
    LeastBullish = 5,

    /// <summary>
    /// Small red body with very long wicks - minimal bearish signal
    /// </summary>
    LeastBearish = 6,

    /// <summary>
    /// Small red body with longer wicks - weak bearish signal
    /// </summary>
    NeutralBearish = 7,

    /// <summary>
    /// Red body with balanced wicks - moderate bearish signal
    /// </summary>
    NormalBearish = 8,

    /// <summary>
    /// Red body with small upper wick - strong bearish signal
    /// </summary>
    SecondMostBearish = 9,

    /// <summary>
    /// Large red body with minimal or no wicks - strongest bearish signal
    /// </summary>
    MostBearish = 10
}