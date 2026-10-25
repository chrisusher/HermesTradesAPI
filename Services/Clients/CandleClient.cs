using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Shared.DTOs.Yahoo;
using System.Net.Http.Headers;
using ChrisUsher.Core.Services.Interfaces;

namespace Services.Clients;

public class CandleClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CandleClient> _logger;
    private readonly string _baseUrl;
    private readonly ICacheService _cache;
    private readonly TimeProvider _timeProvider;

    public CandleClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<CandleClient> logger,
        IConfiguration configuration,
        ICacheService cache,
        TimeProvider timeProvider) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/v1/candles";
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public Task<List<PeriodCandle>> GetCandlesAsync(Guid stockId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => GetCandlesAsync(stockId, from, to, "daily", cancellationToken);

    public async Task<List<PeriodCandle>> GetCandlesAsync(
        Guid stockId,
        DateTime from,
        DateTime to,
        string interval,
        CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty)
        {
            throw new ArgumentException("A stock id is required.", nameof(stockId));
        }

        if (from > to)
        {
            throw new ArgumentException("The from date must be earlier than or equal to the to date.", nameof(from));
        }

        if (interval is not ("daily" or "weekly" or "monthly" or "5m" or "15m" or "30m" or "1h"))
        {
            throw new ArgumentException("Unsupported candle interval.", nameof(interval));
        }

        var endpoint = BuildEndpoint(stockId, from, to) + $"&interval={Uri.EscapeDataString(interval)}";
        cancellationToken.ThrowIfCancellationRequested();
        var now = _timeProvider.GetUtcNow();
        // Current and intraday candles can still change; cache only completed daily ranges.
        var cacheKey = interval == "daily" && to.ToUniversalTime().Date < now.UtcDateTime.Date
            ? GetDailyCacheKey(endpoint, now)
            : null;

        if (cacheKey is not null)
        {
            var cachedCandles = await _cache.GetAsync<List<PeriodCandle>>(cacheKey);

            if (cachedCandles is not null)
            {
                return cachedCandles;
            }
        }

        using var activity = StartExternalActivity("market-data.candles", endpoint);
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
                _logger.LogWarning("Candle API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);

                return new List<PeriodCandle>();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<PeriodCandle>();
            }

            try
            {
                var candlesResponse = JsonSerializer.Deserialize<GetCandlesResponse>(content, SharedCommon.JsonOptions);

                if (cacheKey is not null && candlesResponse?.Candles is { Count: > 0 } candles)
                {
                    await _cache.SetAsync(cacheKey, candles, GetDailyCacheTtl(now));
                }

                return candlesResponse?.Candles ?? new List<PeriodCandle>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to parse candle payload from {Endpoint}", endpoint);

                return new List<PeriodCandle>();
            }
        }
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }

    private string BuildEndpoint(Guid stockId, DateTime from, DateTime to)
    {
        var fromValue = Uri.EscapeDataString(from.ToUniversalTime().ToString("o"));
        var toValue = Uri.EscapeDataString(to.ToUniversalTime().ToString("o"));

        return $"{_baseUrl.TrimEnd('/')}/stocks/{stockId}?from={fromValue}&to={toValue}";
    }
}
