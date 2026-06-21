using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using Services;
using Shared;
using Shared.Interfaces;

namespace Backend.Functions.v1;

public abstract class HttpFunction
{
    private readonly UserService _userService;
    private readonly IApiKeyValidationService _apiKeyValidationService;

    protected HttpFunction(UserService userService, IApiKeyValidationService apiKeyValidationService)
    {
        _userService = userService;
        _apiKeyValidationService = apiKeyValidationService;
    }

    protected async Task<bool> IsAuthorizedAsync(HttpRequestData req, CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey(req);
        return await _apiKeyValidationService.IsValidApiKeyAsync(apiKey, cancellationToken);
    }

    protected static string? GetApiKey(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("x-api-key", out var values))
        {
            return null;
        }

        return values.FirstOrDefault();
    }

    protected static async Task<HttpResponseData> CreateJsonResponseAsync<T>(HttpRequestData req, HttpStatusCode statusCode, T body)
    {
        if (statusCode == HttpStatusCode.NoContent || body is null)
        {
            var noContentResponse = req.CreateResponse(statusCode);
            return noContentResponse;
        }

        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, SharedCommon.JsonOptions));
        return response;
    }

    protected async Task<bool> UserExistsAsync(HttpRequestData req, Guid userId)
    {
        return await _userService.UserExistsAsync(userId, req.FunctionContext.CancellationToken);
    }
}
