using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Shared;
using Shared.DTOs.Stocks;
using System.Net.Http.Headers;

namespace Services.Clients;

public class StockClient : MarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StockClient> _logger;
    private readonly string _baseUrl;

    public StockClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<StockClient> logger,
        IConfiguration configuration) : base(secretClient, configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = $"{BaseUrl}/v1/stocks";
    }

    public async Task<PortfolioStock?> GetStockAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("A stock symbol is required.", nameof(symbol));
        }

        var endpoint = $"{_baseUrl.TrimEnd('/')}/{symbol.Trim().ToUpperInvariant()}";
        return await GetStockFromEndpointAsync(endpoint, cancellationToken);
    }

    public async Task<PortfolioStock?> GetStockAsync(Guid stockId, CancellationToken cancellationToken = default)
    {
        if (stockId == Guid.Empty)
        {
            throw new ArgumentException("A stock id is required.", nameof(stockId));
        }

        var endpoint = $"{_baseUrl.TrimEnd('/')}/id/{stockId}";
        return await GetStockFromEndpointAsync(endpoint, cancellationToken);
    }

    public async Task<List<PortfolioStock>> GetStocksAsync(IEnumerable<Guid> stockIds, CancellationToken cancellationToken = default)
    {
        if (stockIds is null)
        {
            throw new ArgumentNullException(nameof(stockIds));
        }

        var results = new List<PortfolioStock>();

        foreach (var stockId in stockIds)
        {
            if (stockId == Guid.Empty)
            {
                continue;
            }

            var stock = await GetStockAsync(stockId, cancellationToken);
            
            if (stock is not null)
            {
                results.Add(stock);
            }
        }

        return results;
    }

    private async Task<PortfolioStock?> GetStockFromEndpointAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var activity = StartExternalActivity("market-data.stocks", endpoint);
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
                _logger.LogError("Stock API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PortfolioStock>(content, SharedCommon.JsonOptions);
        }
        catch (Exception ex)
        {
            CompleteExternalActivity(activity, exception: ex);
            throw;
        }
    }
}
