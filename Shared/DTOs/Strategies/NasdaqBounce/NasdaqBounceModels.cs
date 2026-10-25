using Shared.DTOs.Yahoo;
using Shared.DTOs.Strategies;

namespace Shared.DTOs.Strategies.NasdaqBounce;

public sealed record NasdaqBouncePortfolio(Guid Id, Guid UserId, string Name, decimal Cash,
    CurrencyCode Currency, CurrencyCode UserCurrency);

public sealed record NasdaqBounceSession(DateOnly Date, DateTimeOffset Open, DateTimeOffset Close)
{
    public DateTimeOffset Deadline => Close.AddMinutes(-30);
}

public sealed record NasdaqBounceMarketSnapshot(Guid StockId, string Exchange, decimal PreviousClose,
    decimal Price, DateTimeOffset PriceTime, IReadOnlyList<PeriodCandle> Candles);

public sealed record NasdaqBounceEntrySignal(DateOnly Date, decimal Decline, decimal Allocation, NasdaqBounceMarketSnapshot Snapshot);

public sealed record SupportPoint(DateTimeOffset Time, decimal Price);

public sealed record SupportDecision(SupportAction Action, string Explanation, List<SupportPoint> Evidence)
{
    public const string PromptVersion = "nasdaq-bounce-support-v1";
}

public sealed class NasdaqBounceReview
{
    public DateTimeOffset Slot { get; set; }
    public SupportDecision? Decision { get; set; }
    public string? Failure { get; set; }
    public string PromptVersion { get; set; } = SupportDecision.PromptVersion;
}

public sealed class NasdaqBounceAlert
{
    public string Key { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Sent { get; set; }
}

public sealed class NasdaqBounceRun
{
    public Guid Id { get; set; }
    public NasdaqBouncePortfolio Portfolio { get; set; } = null!;
    public DateOnly EntryDate { get; set; }
    public DateOnly ReviewDate { get; set; }
    public DateTimeOffset ExitDeadline { get; set; }
    public DateTimeOffset EntryWindowEnds { get; set; }
    public decimal Allocation { get; set; }
    public decimal Decline { get; set; }
    public StrategyTrade Entry { get; set; } = null!;
    public bool EntryApplied { get; set; }
    public StrategyTrade? Exit { get; set; }
    public string? ExitReason { get; set; }
    public bool Closed { get; set; }
    public List<NasdaqBounceReview> Reviews { get; set; } = [];
    public List<NasdaqBounceAlert> Alerts { get; set; } = [];
}
