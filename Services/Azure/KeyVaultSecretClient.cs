using Azure.Security.KeyVault.Secrets;
using Shared.Interfaces;

namespace Services.Azure;

public sealed class KeyVaultSecretClient(SecretClient client) : ISecretClient
{
    public async Task<string> GetSecretAsync(string secretName) => (await client.GetSecretAsync(secretName)).Value.Value;

    public async Task<string?> SetSecretAsync(string secretName, string secretValue) =>
        (await client.SetSecretAsync(secretName, secretValue)).Value.Value;
}
