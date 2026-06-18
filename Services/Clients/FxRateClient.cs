using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace Services.Clients;

public class FxRateClient
{
    private readonly HttpClient _httpClient;
    private readonly SecretClient _secretClient;
    private readonly ILogger<FxRateClient> _logger;
    private readonly string _baseUrl;
    private const string SECRET_NAME = "MARKET-DATA-FUNCTIONS-API-KEY";

    public FxRateClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<FxRateClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _secretClient = secretClient;
        _logger = logger;
        _baseUrl = configuration["CurrencyApi:BaseUrl"] ?? "https://market-data-functions.azurewebsites.net/api/v1/currency";
    }

    public async Task<decimal?> GetConversionRateAsync(CurrencyCode fromCurrency, CurrencyCode toCurrency, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{_baseUrl.TrimEnd('/')}/rate/{fromCurrency}/{toCurrency}";
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-functions-key", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Currency API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return ParseRate(document.RootElement);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unable to parse currency rate payload from {Endpoint}", endpoint);
            return null;
        }
    }

    public async Task<decimal> ConvertAsync(decimal amount, CurrencyCode fromCurrency, CurrencyCode toCurrency, DateTime? forDate = null, CancellationToken cancellationToken = default)
    {
        forDate ??= DateTime.UtcNow.Date;

        var endpoint = $"{_baseUrl.TrimEnd('/')}/{fromCurrency}/{toCurrency}?amount={amount}&forDate={forDate:yyyy-MM-dd}";
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-functions-key", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Currency conversion request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            return amount;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return amount;
        }

        try
        {
            return JsonSerializer.Deserialize<decimal>(content);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unable to parse currency conversion payload from {Endpoint}", endpoint);
            return amount;
        }
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var secret = await _secretClient.GetSecretAsync(SECRET_NAME, cancellationToken: cancellationToken);
        return secret.Value.Value ?? throw new InvalidOperationException($"The secret '{SECRET_NAME}' did not contain a value.");
    }

    private static decimal? ParseRate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "rate", "value", "conversionRate", "result" })
            {
                if (element.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.Number &&
                    property.TryGetDecimal(out var parsedValue))
                {
                    return parsedValue;
                }
            }
        }

        return null;
    }
}
