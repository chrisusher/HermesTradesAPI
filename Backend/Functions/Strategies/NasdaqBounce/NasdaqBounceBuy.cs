using Microsoft.Azure.Functions.Worker;
using Services.Strategies.NasdaqBounce;

namespace Backend.Functions.Strategies.NasdaqBounce;

public sealed class NasdaqBounceBuy(NasdaqBounceStrategy strategy)
{
    [Function("NasdaqBounceBuy")]
    // NASDAQ close-minus-30-minute entry windows fall between 16:00 and 20:59 UTC
    // throughout the year, including early closes and both US daylight-saving offsets.
    public Task RunAsync([TimerTrigger("0 * 16-20 * * 1-5", UseMonitor = true)] TimerInfo timer, CancellationToken cancellationToken) =>
        strategy.BuyAsync(cancellationToken);
}
