using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios;

public sealed class GetPortfolios : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<GetPortfolios> _logger;

    public GetPortfolios(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetPortfolios> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("GetPortfolios")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/{userId}/portfolios")] HttpRequestData req,
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

            if (!await IsAuthorisedAsync(req, req.FunctionContext.CancellationToken))
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

            var portfolios = await _portfolioService.GetPortfoliosAsync(parsedUserId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, portfolios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get portfolios for user {UserId}", userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to get portfolios." 
            });
        }
    }
}
