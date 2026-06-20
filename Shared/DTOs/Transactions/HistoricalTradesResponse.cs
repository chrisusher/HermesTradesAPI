namespace Shared.DTOs.Transactions;

/// <summary>
/// Response containing historical confirmed trades for a strategy
/// </summary>
public class HistoricalTradesResponse
{
    /// <summary>
    /// List of confirmed transactions
    /// </summary>
    [JsonPropertyName("confirmedTrades")]
    public required List<TransactionResponse> ConfirmedTrades { get; set; } = new();

    /// <summary>
    /// The total realised profit/loss for the strategy across the confirmed trades.
    /// </summary>
    [JsonPropertyName("realisedProfitLoss")]
    public decimal RealisedProfitLoss { get; set; }
}