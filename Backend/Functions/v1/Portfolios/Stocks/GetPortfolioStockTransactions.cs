using System.Globalization;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Exceptions;
using Shared.Interfaces;

namespace Backend.Functions.v1.Portfolios.Stocks;

public sealed class GetPortfolioStockTransactions : HttpFunction
{
    private readonly TransactionService _transactionService;
    private readonly ILogger<GetPortfolioStockTransactions> _logger;

    public GetPortfolioStockTransactions(
        TransactionService transactionService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetPortfolioStockTransactions> logger) : base(userService, apiKeyValidationService)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    [Function("GetPortfolioStockTransactions")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/{userId}/portfolios/{portfolioId:guid}/stocks/{stockId:guid}/transactions")] HttpRequestData req,
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

            if (!TryGetQueryDate(req, "fromDate", out var fromDate))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Invalid fromDate query parameter."
                });
            }

            if (!TryGetQueryDate(req, "toDate", out var toDate, allowMissing: true))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.BadRequest, new
                {
                    error = "Invalid toDate query parameter."
                });
            }

            var transactions = await _transactionService.GetTransactionsByStockAsync(parsedStockId, fromDate);
            var filteredTransactions = transactions
                .Where(x => x.PortfolioId == parsedPortfolioId)
                .Where(x => !toDate.HasValue || x.TransactionDate <= toDate.Value)
                .ToList();

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, filteredTransactions);
        }
        catch (DataNotFoundException ex)
        {
            _logger.LogWarning(ex, "Unable to retrieve transactions for stock {StockId} in portfolio {PortfolioId} for user {UserId}.", stockId, portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.NotFound, new
            {
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions for stock {StockId} in portfolio {PortfolioId} for user {UserId}", stockId, portfolioId, userId);
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to get stock transactions."
            });
        }
    }

    private static bool TryGetQueryDate(HttpRequestData req, string key, out DateTime? date, bool allowMissing = false)
    {
        date = null;

        var query = req.Url.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return allowMissing;
        }

        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (!parts[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (parts.Length == 1 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return allowMissing;
            }

            var rawValue = Uri.UnescapeDataString(parts[1]);
            if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
            {
                date = parsedDate;
                return true;
            }

            return false;
        }

        return allowMissing;
    }
}
