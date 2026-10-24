namespace Shared.Interfaces;

public interface ISecretClient
{
    Task<string> GetSecretAsync(string secretName);

    Task<string?> SetSecretAsync(string secretName, string secretValue);
}
