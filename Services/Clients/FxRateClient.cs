using Azure.Security.KeyVault.Secrets;
using ChrisUsher.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Shared.DTOs.Currency;
using System.Net.Http.Headers;

namespace Services.Clients;

public class FxRateClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FxRateClient> _logger;
    private readonly string _baseUrl;
    private readonly ICacheService _cache;
    private readonly TimeProvider _timeProvider;

    public FxRateClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<FxRateClient> logger,
        IConfiguration configuration,
        ICacheService cache,
        TimeProvider timeProvider) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/v1/currency";
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public Task<decimal?> GetConversionRateAsync(
        CurrencyCode fromCurrency,
        CurrencyCode toCurrency,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var endpoint = $"{_baseUrl.TrimEnd('/')}/rate/{fromCurrency}/{toCurrency}";

        return GetRateAsync(fromCurrency, toCurrency, now.Date, endpoint, now, cancellationToken);
    }

    public async Task<decimal> ConvertAsync(
        decimal amount,
        CurrencyCode fromCurrency,
        CurrencyCode toCurrency,
        DateTime? forDate = null,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var rateDate = forDate?.Date ?? now.Date;
        // Fetch a unit conversion so every amount can reuse the same dated rate.
        var endpoint = rateDate == now.Date
            ? $"{_baseUrl.TrimEnd('/')}/rate/{fromCurrency}/{toCurrency}"
            : $"{_baseUrl.TrimEnd('/')}/{fromCurrency}/{toCurrency}?amount=1&forDate={rateDate:yyyy-MM-dd}";

        var rate = await GetRateAsync(fromCurrency, toCurrency, rateDate, endpoint, now, cancellationToken);

        return rate.HasValue ? amount * rate.Value : amount;
    }

    private async Task<decimal?> GetRateAsync(
        CurrencyCode fromCurrency,
        CurrencyCode toCurrency,
        DateTime rateDate,
        string endpoint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (fromCurrency == toCurrency)
        {
            return 1m;
        }

        var cacheKey = GetDailyCacheKey($"{_baseUrl}/{fromCurrency}/{toCurrency}/{rateDate:yyyy-MM-dd}", now);
        var cachedRate = await _cache.GetAsync<FxRate>(cacheKey);

        if (cachedRate is { Value: > 0m })
        {
            return cachedRate.Value;
        }

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
                _logger.LogWarning("Currency API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, content);

                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(content);
                var rate = ParseRate(document.RootElement);

                if (rate is > 0m)
                {
                    await _cache.SetAsync(cacheKey, new FxRate(rate.Value), GetDailyCacheTtl(now));

                    return rate;
                }

                return null;
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

    private static decimal? ParseRate(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[]
            {
                "rate",
                "value",
                "conversionRate",
                "result"
            })
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
