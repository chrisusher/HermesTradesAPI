using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Azure;
using Services.Clients;
using Services.Database;
using Services.Repositories;
using Shared.Config;

namespace Services;

public static class HermesTradeServices
{
    public static IServiceCollection AddHermesTradeServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddLogging(logging =>
        {
            // Prevent Azure Storage logs from spamming unless there are errors
            logging.AddFilter("Azure.Storage.Blobs", LogLevel.Error);
            logging.AddFilter("Azure.Storage.Queues", LogLevel.Error);
            logging.AddFilter("Azure.Storage.Common", LogLevel.Error);
            logging.AddFilter("Azure.Storage", LogLevel.Error);
            logging.AddFilter("Azure.Core", LogLevel.Error);
            logging.AddFilter("Azure", LogLevel.Error);
            logging.AddFilter("Microsoft.Azure.Storage", LogLevel.Error);
            logging.AddFilter("Microsoft.Azure.WebJobs.Host.Blobs", LogLevel.Error);
            logging.AddFilter("Microsoft.Azure.WebJobs.Extensions.Storage", LogLevel.Error);

            // Suppress Azure Functions host internal storage operations
            logging.AddFilter((category, level) =>
            {
                // Filter out Azure Storage request/response logs that are Information level or lower
                if (category?.StartsWith("Azure.") == true && level <= LogLevel.Information)
                {
                    return false;
                }
                return true;
            });
        });

        #region Azure Services

        services.AddAzureClients(config =>
        {
            var storageConnectionString = configuration.GetConnectionString("BlobStorage");

            config.AddBlobServiceClient(storageConnectionString)
                .WithName("TradeAPI");

            // Add KeyVault
            var keyVaultConfig = configuration
                .GetSection("KeyVault")
                .Get<KeyVaultConfig>();

            config.AddSecretClient(new Uri($"https://{keyVaultConfig?.KeyVaultName}.vault.azure.net/"));
        });

        #endregion

        var functionsConfig = configuration
            .GetSection("Functions")
            .Get<FunctionsConfig>() ?? new FunctionsConfig();

        var globalConfig = configuration
            .GetSection("Global")
            .Get<GlobalConfig>() ?? new GlobalConfig();

        services.AddDbContext<DatabaseContext>(options =>
        {
            options.UseCosmos(
                configuration["Database:AccountName"]!,
                configuration["Database:Key"]!,
                $"StockTraderAgent-{globalConfig.Environment}"
            );

            options.EnableSensitiveDataLogging();

#if DEBUG
            options.EnableDetailedErrors();
            options.LogTo(Console.WriteLine, LogLevel.Information);
#endif
        });

        services.AddTransient(services =>
        {
            var blobServiceClient = services.GetRequiredService<BlobServiceClient>();
            return new BlobStorageService(blobServiceClient);
        });

        #region Clients

        services.AddHttpClient<FxRateClient>();
        services.AddHttpClient<StockClient>();

        #endregion

        #region Config
        services.AddSingleton(functionsConfig!);
        services.AddSingleton(globalConfig!);
        #endregion

        #region Repositories

        services.AddScoped<MigrationsRepository>();
        services.AddScoped<PortfolioRepository>();
        services.AddScoped<ReportRepository>();
        services.AddScoped<StrategyRepository>();
        services.AddScoped<StrategyVersionRepository>();
        services.AddScoped<TransactionRepository>();

        // services.AddSingleton(sp =>
        // {
        //     // ReportRepository needs Backend storage for reports container
        //     var backendStorage = CreateBlobStorageService(sp, "Backend");

        //     var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        //     return new ReportRepository(backendStorage, loggerFactory);
        // });

        #endregion

        #region Services

        // Register IStorageService to use Backend storage (for Backend-specific containers)
        // services.AddTransient<IStorageService>(sp =>
        // {
        //     var backendStorage = CreateBlobStorageService(sp, "Backend");
        //     return backendStorage;
        // });

        // services.AddScoped<ReportService>();

        #region EF Core Services

        services.AddScoped<MigrationsService>();
        // services.AddScoped<PortfolioService>();
        // services.AddScoped<StrategyService>();
        // services.AddScoped<StrategyVersionService>();
        // services.AddScoped<TransactionService>();

        // services.AddScoped(sp =>
        // {
        //     // SettingsService needs Backend storage for settings container
        //     var backendStorage = CreateBlobStorageService(sp, "Backend");
        //     var logger = sp.GetRequiredService<ILoggerFactory>();
        //     return new SettingsService(backendStorage, logger);
        // });

        #endregion

        #endregion

        #region Clients

        #endregion

        services.AddSingleton(s => services.BuildServiceProvider());

        return services;
    }
}
