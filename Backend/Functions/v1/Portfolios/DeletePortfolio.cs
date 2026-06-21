using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios;

public sealed class DeletePortfolio : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<DeletePortfolio> _logger;

    public DeletePortfolio(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<DeletePortfolio> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("DeletePortfolio")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/{userId}/portfolios/{portfolioId:guid}")] HttpRequestData req,
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

            await _portfolioService.DeletePortfolioAsync(parsedUserId, parsedPortfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.NoContent, new { });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete portfolio {PortfolioId} for user {UserId}", portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to delete portfolio." 
            });
        }
    }
}
