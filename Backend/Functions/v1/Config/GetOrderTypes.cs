using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Enums;
using Shared.Interfaces;

namespace Backend.Functions.v1.Config;

public sealed class GetOrderTypes : HttpFunction
{
    private readonly ILogger<GetOrderTypes> _logger;

    public GetOrderTypes(
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetOrderTypes> logger) : base(userService, apiKeyValidationService)
    {
        _logger = logger;
    }

    [Function("GetOrderTypes")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/config/ordertypes")] HttpRequestData req)
    {
        try
        {
            if (!await IsAuthorisedAsync(req, req.FunctionContext.CancellationToken))
            {
                return await CreateJsonResponseAsync(req, HttpStatusCode.Unauthorized, new
                {
                    error = "Unauthorized."
                });
            }

            var orderTypes = OrderTypeExtensions.GetSupportedOrderTypes();
            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, new
            {
                orderTypes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get supported order types.");
            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new
            {
                error = "Failed to get supported order types."
            });
        }
    }
}
