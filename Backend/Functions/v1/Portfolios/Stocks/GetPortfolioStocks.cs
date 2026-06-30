using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.DTOs.Portfolios;
using Shared.Exceptions;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios.Stocks;

public sealed class GetPortfolioStocks : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<GetPortfolioStocks> _logger;

    public GetPortfolioStocks(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetPortfolioStocks> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("GetPortfolioStocks")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/{userId}/portfolios/{portfolioId:guid}/stocks")] HttpRequestData req,
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

            var holdings = await _portfolioService.GetComposedPortfolioAsync(parsedUserId, parsedPortfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, holdings ?? new List<PortfolioHolding>());
        }
        catch (DataNotFoundException ex)
        {
            _logger.LogWarning(ex, "Portfolio {PortfolioId} for user {UserId} was not found.", portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get portfolio stocks for user {UserId} and portfolio {PortfolioId}", userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to get portfolio stocks."
            });
        }
    }
}
