namespace Shared.Interfaces;

public interface IStrategyLock
{
    Task<IStrategyLease?> TryAcquireAsync(string key, CancellationToken cancellationToken);
}
