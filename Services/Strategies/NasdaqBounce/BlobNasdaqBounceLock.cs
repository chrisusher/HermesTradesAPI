using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Shared.Interfaces;

namespace Services.Strategies.NasdaqBounce;

public sealed class BlobNasdaqBounceLock(BlobServiceClient storage, ILogger<BlobNasdaqBounceLock> logger) : IStrategyLock
{
    public async Task<IStrategyLease?> TryAcquireAsync(string key, CancellationToken cancellationToken)
    {
        var container = storage.GetBlobContainerClient("strategy-locks");
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(Uri.EscapeDataString(key));

        try
        {
            await blob.UploadAsync(BinaryData.FromString(""), cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Another invocation already created the lock blob.
        }
        var lease = blob.GetBlobLeaseClient();

        try
        {
            await lease.AcquireAsync(
                TimeSpan.FromSeconds(60),
                cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {

            return null;
        }

        return new RenewableLease(lease, logger, cancellationToken);
    }

    private sealed class RenewableLease : IStrategyLease
    {
        private readonly BlobLeaseClient _lease;
        private readonly CancellationTokenSource _lifetime;
        private readonly Task _renewal;
        private readonly ILogger _logger;
        public CancellationToken Token => _lifetime.Token;

        public RenewableLease(BlobLeaseClient lease, ILogger logger, CancellationToken ct)
        {
            _lease = lease;
            _logger = logger;
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _renewal = RenewAsync();
        }

        private async Task RenewAsync()
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));

                while (await timer.WaitForNextTickAsync(_lifetime.Token))
                {
                    await _lease.RenewAsync(cancellationToken: _lifetime.Token);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                // Normal shutdown cancels the renewal loop.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Strategy lease renewal failed; cancelling protected work.");
                await _lifetime.CancelAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _lifetime.CancelAsync();
            await _renewal;

            try
            {
                await _lease.ReleaseAsync(cancellationToken: CancellationToken.None);
            }
            catch (RequestFailedException ex) when (ex.Status is 409 or 412)
            {
                // The lease expired or was released before this cleanup ran.
            }
            finally
            {
                _lifetime.Dispose();
            }
        }
    }
}
