using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Services.Clients;

public class MarketDataClient
{
    private static readonly ActivitySource ActivitySource = new("Services.Clients.MarketDataClient");

    private readonly string _baseUrl;
    private readonly SecretClient _secretClient;
    private const string SECRET_NAME = "MARKET-DATA-FUNCTIONS-API-KEY";

    public MarketDataClient(SecretClient secretClient, IConfiguration configuration)
    {
        _baseUrl = configuration["CandleApi:BaseUrl"] ?? "https://market-data-functions.azurewebsites.net/api";
        _secretClient = secretClient;
    }

    protected string BaseUrl => _baseUrl;

    protected async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        var secret = await _secretClient.GetSecretAsync(SECRET_NAME, cancellationToken: cancellationToken);
        
        return secret.Value.Value ?? throw new InvalidOperationException($"The secret '{SECRET_NAME}' did not contain a value.");
    }

    protected Activity? StartExternalActivity(string operationName, string endpoint)
    {
        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Client);

        if (activity is null)
        {
            return null;
        }

        activity.DisplayName = operationName;
        activity.SetTag("http.method", "GET");
        activity.SetTag("url.full", endpoint);
        activity.SetTag("external.service", "market-data");
        activity.SetTag("component", nameof(MarketDataClient));
        activity.AddEvent(new ActivityEvent("market-data.request.sent"));

        return activity;
    }

    protected static void CompleteExternalActivity(Activity? activity, HttpResponseMessage? response = null, Exception? exception = null)
    {
        if (activity is null)
        {
            return;
        }

        if (exception is not null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.StackTrace ?? string.Empty }
            }));
        }
        else if (response is not null)
        {
            activity.SetTag("http.status_code", (int)response.StatusCode);
            activity.SetStatus(response.IsSuccessStatusCode ? ActivityStatusCode.Ok : ActivityStatusCode.Error, response.ReasonPhrase);
        }

        activity.AddEvent(new ActivityEvent("market-data.response.received"));
        activity.Dispose();
    }
}
