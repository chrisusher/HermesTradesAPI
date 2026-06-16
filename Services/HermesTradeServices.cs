using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Azure;
using Services.Database;
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
        });

        #endregion

        var functionsConfig = configuration.GetSection("Functions").Get<FunctionsConfig>() ?? new FunctionsConfig();

        services.AddDbContext<DatabaseContext>(options =>
        {
            options.UseCosmos(
                configuration["Database:AccountName"]!,
                configuration["Database:Key"]!,
                configuration["Database:DatabaseName"]!
            );

            options.EnableSensitiveDataLogging();

#if DEBUG
            options.EnableDetailedErrors();
            options.LogTo(Console.WriteLine, LogLevel.Information);
#endif
        });

        #region Clients

        #endregion

        #region Config
        services.AddSingleton(functionsConfig!);
        #endregion

        #region Repositories

        // services.AddScoped<Repositories.MigrationsRepository>();
        // services.AddScoped<PortfolioRepository>();
        // services.AddScoped<StockRepository>();
        // services.AddScoped<StrategyRepository>();
        // services.AddScoped<StrategyVersionRepository>();
        // services.AddScoped<TransactionRepository>();

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

        services.AddSingleton(s => services.BuildServiceProvider());

        return services;
    }

    /// <summary>
    /// Helper method to centralise creation of BlobStorageService for a given client name.
    /// </summary>
    /// <param name="sp">The service provider.</param>
    /// <param name="clientName">The Azure Blob client name.</param>
    /// <returns>A configured BlobStorageService instance.</returns>
    private static BlobStorageService CreateBlobStorageService(IServiceProvider sp, string clientName)
    {
        var clientFactory = sp.GetRequiredService<IAzureClientFactory<BlobServiceClient>>();

        var blobClient = clientFactory.CreateClient(clientName);
        return new BlobStorageService(blobClient);
    }
}
