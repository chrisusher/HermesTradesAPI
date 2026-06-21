using Azure.Security.KeyVault.Secrets;
using Shared.Interfaces;

namespace Services;

public sealed class ApiKeyValidationService : IApiKeyValidationService
{
    private readonly SecretClient _secretClient;
    private const string SecretName = "API-KEY";

    public ApiKeyValidationService(SecretClient secretClient)
    {
        _secretClient = secretClient;
    }

    public async Task<bool> IsValidApiKeyAsync(string? providedApiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return false;
        }

        var secret = await _secretClient.GetSecretAsync(SecretName, cancellationToken: cancellationToken);
        var expectedApiKey = secret.Value.Value;

        return string.Equals(providedApiKey, expectedApiKey, StringComparison.Ordinal);
    }
}
