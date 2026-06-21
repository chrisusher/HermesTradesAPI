using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared;
using Shared.DTOs.Portfolios;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios;

public sealed class CreatePortfolio : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<CreatePortfolio> _logger;

    public CreatePortfolio(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<CreatePortfolio> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("CreatePortfolio")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/{userId}/portfolios")] HttpRequestData req,
        string userId)
    {
        try
        {
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new 
                { 
                    error = "Invalid userId." 
                });
            }

            if (!await IsAuthorizedAsync(req, req.FunctionContext.CancellationToken))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.Unauthorized, new 
                { 
                    error = "Unauthorized." 
                });
            }

            if (!await UserExistsAsync(req, parsedUserId))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new 
                { 
                    error = "User not found." 
                });
            }

            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new 
                { 
                    error = "Request body is required." 
                });
            }

            var portfolioRequest = JsonSerializer.Deserialize<Portfolio>(requestBody, SharedCommon.JsonOptions);
            if (portfolioRequest is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new 
                { 
                    error = "Invalid portfolio payload." 
                });
            }

            portfolioRequest.UserId = parsedUserId;
            portfolioRequest.PortfolioId = portfolioRequest.PortfolioId == Guid.Empty ? Guid.NewGuid() : portfolioRequest.PortfolioId;

            var createdPortfolio = await _portfolioService.CreatePortfolioAsync(parsedUserId, portfolioRequest);
            var response = req.CreateResponse(HttpStatusCode.Created);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(createdPortfolio, SharedCommon.JsonOptions));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create portfolio for user {UserId}", userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to create portfolio." 
            });
        }
    }
}
