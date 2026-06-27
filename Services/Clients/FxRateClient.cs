using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;

namespace Services.Clients;

public class FxRateClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FxRateClient> _logger;
    private readonly string _baseUrl;

    public FxRateClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<FxRateClient> logger,
        IConfiguration configuration) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/api/v1/currency";
    }

    public async Task<decimal?> GetConversionRateAsync(CurrencyCode fromCurrency, CurrencyCode toCurrency, CancellationToken cancellationToken = default)
    {
        var endpoint = $"{_baseUrl.TrimEnd('/')}/rate/{fromCurrency}/{toCurrency}";
        using var activity = StartExternalActivity("market-data.fx-rate", endpoint);
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-functions-key", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            CompleteExternalActivity(activity, response);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Currency API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return null;
            }

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
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }

    public async Task<decimal> ConvertAsync(decimal amount, CurrencyCode fromCurrency, CurrencyCode toCurrency, DateTime? forDate = null, CancellationToken cancellationToken = default)
    {
        forDate ??= DateTime.UtcNow.Date;

        var endpoint = $"{_baseUrl.TrimEnd('/')}/{fromCurrency}/{toCurrency}?amount={amount}&forDate={forDate:yyyy-MM-dd}";
        using var activity = StartExternalActivity("market-data.fx-rate.convert", endpoint);
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-functions-key", apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            CompleteExternalActivity(activity, response);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Currency conversion request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                return amount;
            }

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
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
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
