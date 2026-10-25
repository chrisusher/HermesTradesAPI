using System.Net.Http.Headers;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Shared.Calendar;
using ChrisUsher.Core.Services.Interfaces;

namespace Services.Clients;

public class CalendarClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CalendarClient> _logger;
    private readonly string _baseUrl;
    private readonly ICacheService _cache;
    private readonly TimeProvider _timeProvider;

    public CalendarClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<CalendarClient> logger,
        IConfiguration configuration,
        ICacheService cache,
        TimeProvider timeProvider) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/v1/calendar";
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<bool> ShouldSendNotificationsAsync(string countryCode = "US", DateTime? now = null)
    {
        if (now is null)
        {
            now = DateTime.UtcNow;
        }

        var marketTime = await GetMarketTimeAsync(countryCode);

        if (marketTime.IsClosed)
        {
            return false;
        }

        return true;
    }

    public async Task<GetMarketTimeResponse> GetMarketTimeAsync(string countryCode = "US", CancellationToken cancellationToken = default)
    {
        // TradingTime changes during the session, so this endpoint must stay live.
        var endpoint = $"{_baseUrl.TrimEnd('/')}/{countryCode}/market-time";

        using var activity = StartExternalActivity("market-data.calendar", endpoint);
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
                _logger.LogWarning("Calendar API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);

                return new GetMarketTimeResponse();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new GetMarketTimeResponse();
            }

            try
            {
                var closeDates = JsonSerializer.Deserialize<GetMarketTimeResponse>(content, SharedCommon.JsonOptions);

                return closeDates ?? new GetMarketTimeResponse();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to parse Close Dates from {Endpoint}", endpoint);

                return new GetMarketTimeResponse();
            }
        }
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }

    public async Task<List<CloseDate>> GetCloseDatesAsync(string countryCode, int year, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var endpoint = $"{_baseUrl.TrimEnd('/')}/{countryCode.ToUpperInvariant()}/{year}";
        var now = _timeProvider.GetUtcNow();
        var cacheKey = GetDailyCacheKey(endpoint, now);
        var cachedDates = await _cache.GetAsync<List<CloseDate>>(cacheKey);

        if (cachedDates is not null)
        {
            return cachedDates;
        }

        using var activity = StartExternalActivity("market-data.calendar", endpoint);
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
                _logger.LogWarning("Calendar API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Calendar API returned an empty response.");
            }

            try
            {
                var closeDates = JsonSerializer.Deserialize<List<CloseDate>>(content, SharedCommon.JsonOptions);

                if (closeDates is null)
                {
                    throw new InvalidOperationException("Calendar API returned null closures.");
                }

                await _cache.SetAsync(cacheKey, closeDates, GetDailyCacheTtl(now));

                return closeDates;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to parse Close Dates from {Endpoint}", endpoint);
                throw;
            }
        }
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }
}
