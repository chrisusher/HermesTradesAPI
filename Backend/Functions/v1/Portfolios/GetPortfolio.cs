using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios;

public sealed class GetPortfolio : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<GetPortfolio> _logger;

    public GetPortfolio(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetPortfolio> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("GetPortfolio")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/{userId}/portfolios/{portfolioId:guid}")] HttpRequestData req,
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

            var portfolio = await _portfolioService.GetPortfolioAsync(parsedUserId, parsedPortfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, new 
            { 
                data = portfolio 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get portfolio {PortfolioId} for user {UserId}", portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to get portfolio." 
            });
        }
    }
}
