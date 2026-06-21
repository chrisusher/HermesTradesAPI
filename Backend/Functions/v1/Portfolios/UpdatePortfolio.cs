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

public sealed class UpdatePortfolio : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<UpdatePortfolio> _logger;

    public UpdatePortfolio(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<UpdatePortfolio> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("UpdatePortfolio")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/{userId}/portfolios/{portfolioId:guid}")] HttpRequestData req,
        string userId,
        string portfolioId)
    {
        try
        {
            if (!Guid.TryParse(userId, out var parsedUserId) || !Guid.TryParse(portfolioId, out var parsedPortfolioId))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new 
                { 
                    error = "Invalid userId or portfolioId." 
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
            portfolioRequest.PortfolioId = parsedPortfolioId;

            var updatedPortfolio = await _portfolioService.UpdatePortfolioAsync(portfolioRequest);
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, updatedPortfolio);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update portfolio {PortfolioId} for user {UserId}", portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to update portfolio." 
            });
        }
    }
}
