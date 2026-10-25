using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Services;
using Shared.Interfaces;

namespace Backend.Functions.v1.Strategies;

public sealed class GetStrategies : HttpFunction
{
    private readonly StrategyService _strategyService;
    private readonly ILogger<GetStrategies> _logger;

    public GetStrategies(
        StrategyService strategyService,
        UserService userService,
        IApiKeyValidationService apiKeyValidationService,
        ILogger<GetStrategies> logger) : base(userService, apiKeyValidationService)
    {
        _strategyService = strategyService;
        _logger = logger;
    }

    [Function("GetStrategies")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/strategies")] HttpRequestData req)
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

            var strategies = await _strategyService.GetStrategiesAsync();

            return await CreateJsonResponseAsync(req, HttpStatusCode.OK, strategies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Strategies");

            return await CreateJsonResponseAsync(req, HttpStatusCode.InternalServerError, new 
            { 
                error = "Failed to get strategies." 
            });
        }
    }
}
