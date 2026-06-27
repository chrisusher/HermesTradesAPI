using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared;
using Shared.DTOs.Transactions;
using Shared.Exceptions;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios.Stocks;

public sealed class SellPortfolioStock : HttpFunction
{
    private readonly PortfolioService _portfolioService;
    private readonly TransactionService _transactionService;
    private readonly ILogger<SellPortfolioStock> _logger;

    public SellPortfolioStock(
        PortfolioService portfolioService,
        TransactionService transactionService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<SellPortfolioStock> logger) : base(userService, apiKeyValidationService)
    {
        _portfolioService = portfolioService;
        _transactionService = transactionService;
        _logger = logger;
    }

    [Function("SellPortfolioStock")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/{userId}/portfolios/{portfolioId:guid}/stocks/sell")] HttpRequestData req,
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

            var requestBody = await req.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Request body is required."
                });
            }

            var sellRequest = JsonSerializer.Deserialize<SellTransactionRequestBody>(requestBody, SharedCommon.JsonOptions);

            if (sellRequest is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Invalid sell payload."
                });
            }

            if (string.IsNullOrWhiteSpace(sellRequest.Symbol))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Stock symbol is required."
                });
            }

            if (sellRequest.Quantity <= 0 || sellRequest.TotalCost <= 0)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Quantity and total cost must be greater than zero."
                });
            }

            var response = await _transactionService.SellStockAsync(parsedUserId, parsedPortfolioId, sellRequest);

            await _portfolioService.UpdateHoldingAsync(parsedUserId, parsedPortfolioId, response, req.FunctionContext.CancellationToken);

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, response);
        }
        catch (DataNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unable to process sell request for user {UserId} and portfolio {PortfolioId}.", userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sell stock for user {UserId} and portfolio {PortfolioId}", userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to sell stock."
            });
        }
    }
}
