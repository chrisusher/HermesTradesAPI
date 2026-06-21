using System.Security.Cryptography;
using System.Text;
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

        try
        {
            var secret = await _secretClient.GetSecretAsync(SecretName, cancellationToken: cancellationToken);
            var expectedApiKey = secret.Value.Value;

            if (string.IsNullOrWhiteSpace(expectedApiKey))
            {
                return false;
            }

            return ConstantTimeEquals(providedApiKey, expectedApiKey);
        }
        catch
        {
            return false;
        }
    }

    private static bool ConstantTimeEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
