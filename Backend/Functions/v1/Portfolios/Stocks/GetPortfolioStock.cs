using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Exceptions;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios.Stocks;

public sealed class GetPortfolioStock : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly ILogger<GetPortfolioStock> _logger;

    public GetPortfolioStock(
        PortfolioService portfolioService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetPortfolioStock> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _logger = logger;
    }

    [Function("GetPortfolioStock")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/{userId}/portfolios/{portfolioId:guid}/stocks/{stockId:guid}")] HttpRequestData req,
        string userId,
        string portfolioId,
        string stockId)
    {
        try
        {
            if (!Guid.TryParse(userId, out var parsedUserId) || !Guid.TryParse(portfolioId, out var parsedPortfolioId) || !Guid.TryParse(stockId, out var parsedStockId))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Invalid userId, portfolioId, or stockId."
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

            var portfolio = await _portfolioService.GetPortfolioAsync(parsedUserId, parsedPortfolioId);
            var stock = portfolio.Stocks?.FirstOrDefault(x => x.StockId == parsedStockId);

            if (stock is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
                {
                    error = "Stock not found in portfolio."
                });
            }

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, stock);
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
            _logger.LogError(ex, "Failed to get stock {StockId} for user {UserId} and portfolio {PortfolioId}", stockId, userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to get stock."
            });
        }
    }
}
