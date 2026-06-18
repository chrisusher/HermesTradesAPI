using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Shared;
using Shared.DTOs.Stocks;
using System.Net.Http.Headers;

namespace Services.Clients;

public class StockClient
{
    private readonly HttpClient _httpClient;
    private readonly SecretClient _secretClient;
    private readonly ILogger<StockClient> _logger;
    private readonly string _baseUrl;
    private const string SECRET_NAME = "MARKET-DATA-FUNCTIONS-API-KEY";

    public StockClient(
        HttpClient httpClient,
        SecretClient secretClient,
        ILogger<StockClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _secretClient = secretClient;
        _logger = logger;
        _baseUrl = configuration["StockApi:BaseUrl"] ?? "https://market-data-functions.azurewebsites.net/api/v1/stocks";
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
        var apiKey = await GetApiKeyAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-functions-key", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Stock API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PortfolioStock>(content, SharedCommon.JsonOptions);
    }

    private async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var secret = await _secretClient.GetSecretAsync(SECRET_NAME, cancellationToken: cancellationToken);

        return secret.Value.Value ?? throw new InvalidOperationException($"The secret '{SECRET_NAME}' did not contain a value.");
    }
}
