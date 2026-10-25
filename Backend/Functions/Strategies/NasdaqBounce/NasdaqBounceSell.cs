using Microsoft.Azure.Functions.Worker;
using Services.Strategies.NasdaqBounce;

namespace Backend.Functions.Strategies.NasdaqBounce;

public sealed class NasdaqBounceSell(NasdaqBounceStrategy strategy)
{
    [Function("NasdaqBounceSell")]
    // This UTC window covers every regular NASDAQ session under EST and EDT.
    // The strategy checks the live calendar and market state before reviewing or exiting.
    public Task RunAsync([TimerTrigger("0 * 13-20 * * 1-5", UseMonitor = true)] TimerInfo timer, CancellationToken cancellationToken) =>
        strategy.SellAsync(cancellationToken);
}
