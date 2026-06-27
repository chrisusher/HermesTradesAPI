using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Services.Clients;
using Shared;
using Shared.DTOs.Portfolios;
using Shared.DTOs.Transactions;
using Shared.Exceptions;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios.Stocks;

public sealed class BuyPortfolioStock : HttpFunction
{
    private readonly TransactionService _transactionService;
    private readonly StockClient _stockClient;
    private readonly ILogger<BuyPortfolioStock> _logger;

    public BuyPortfolioStock(
        TransactionService transactionService,
        StockClient stockClient,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<BuyPortfolioStock> logger) : base(userService, apiKeyValidationService)
    {
        _transactionService = transactionService;
        _stockClient = stockClient;
        _logger = logger;
    }

    [Function("BuyPortfolioStock")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/{userId}/portfolios/{portfolioId:guid}/stocks/buy")] HttpRequestData req,
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

            var buyRequest = JsonSerializer.Deserialize<BuyTransactionRequestBody>(requestBody, SharedCommon.JsonOptions);
            if (buyRequest is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Invalid buy payload."
                });
            }

            if (string.IsNullOrWhiteSpace(buyRequest.Symbol))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Stock symbol is required."
                });
            }

            if (buyRequest.Quantity <= 0 || buyRequest.TotalCost <= 0)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Quantity and total cost must be greater than zero."
                });
            }

            var stock = await _stockClient.GetStockAsync(buyRequest.Symbol, req.FunctionContext.CancellationToken);
            
            if (stock is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
                {
                    error = $"Stock '{buyRequest.Symbol}' was not found."
                });
            }

            var holding = new PortfolioHolding
            {
                PortfolioId = parsedPortfolioId,
                StockId = stock.StockId,
                Symbol = stock.Symbol,
                ExchangeName = stock.ExchangeName,
                CurrencyCode = stock.CurrencyCode,
                FirstPurchaseDate = buyRequest.Date ?? DateTime.UtcNow,
                CompanyName = stock.CompanyName,
                PreviousClosePrice = stock.PreviousClosePrice,
                TotalShares = buyRequest.Quantity,
                TotalInvested = buyRequest.TotalCost,
                CurrentValue = 0m,
                AveragePurchasePrice = buyRequest.Price
            };

            var transaction = new TransactionObject
            {
                Created = DateTime.UtcNow,
                Currency = stock.CurrencyCode,
                StockId = stock.StockId,
                Symbol = stock.Symbol,
                Type = buyRequest.Type,
                Price = buyRequest.Price,
                Quantity = buyRequest.Quantity,
                QuantityRemaining = buyRequest.Quantity,
                TotalCost = buyRequest.TotalCost,
                TotalCostToUser = buyRequest.TotalCost,
                TransactionDate = buyRequest.Date ?? DateTime.UtcNow
            };

            var response = await _transactionService.BuyStockAsync(parsedUserId, parsedPortfolioId, holding, transaction);
            if (response is null)
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Unable to process buy request."
                });
            }

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, response);
        }
        catch (DataNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unable to process buy request for user {UserId} and portfolio {PortfolioId}.", userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to buy stock for user {UserId} and portfolio {PortfolioId}", userId, portfolioId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to buy stock."
            });
        }
    }
}
