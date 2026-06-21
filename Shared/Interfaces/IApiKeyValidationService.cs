namespace Shared.Interfaces;

public interface IApiKeyValidationService
{
    Task<bool> IsValidApiKeyAsync(string? providedApiKey, CancellationToken cancellationToken = default);
}
