namespace Shared.Interfaces;

public interface IStrategyLease : IAsyncDisposable
{
    CancellationToken Token { get; }
}
