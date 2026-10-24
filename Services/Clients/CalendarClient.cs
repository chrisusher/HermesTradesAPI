using System.Net.Http.Headers;
using Azure.Security.KeyVault.Secrets;
using ChrisUsher.Core.Shared;
using Microsoft.Extensions.Configuration;
using Shared.Calendar;

namespace Services.Clients;

public class CalendarClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CalendarClient> _logger;
    private readonly string _baseUrl;

    public CalendarClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<CalendarClient> logger,
        IConfiguration configuration) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/v1/calendar";
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
        var endpoint = $"{_baseUrl.TrimEnd('/')}/{countryCode}/{year}";

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
                return new List<CloseDate>();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<CloseDate>();
            }

            try
            {
                var closeDates = JsonSerializer.Deserialize<List<CloseDate>>(content, SharedCommon.JsonOptions);

                return closeDates ?? new List<CloseDate>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Unable to parse Close Dates from {Endpoint}", endpoint);
                return new List<CloseDate>();
            }
        }
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }
}
