using Azure.Storage.Blobs;
using ChrisUsher.Core.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Azure;
using Services.Clients;
using Services.Database;
using Services.Repositories;
using Services.Strategies;
using Shared.Config;
using Shared.Interfaces;
using Services.Clients.Notifications;
using Services.Strategies.NasdaqBounce;
using Azure.Identity;
using ChrisUsher.Core.Services.Cache;
using ChrisUsher.Core.Services.Interfaces;

namespace Services;

public static class HermesTradeServices
{
    public static IServiceCollection AddHermesTradeServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddLogging(logging => {
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
            logging.AddFilter((category, level) => {
                // Filter out Azure Storage request/response logs that are Information level or lower

                if (category?.StartsWith("Azure.") == true && level <= LogLevel.Information)
                {
                    return false;
                }

                return true;
            });
        });

        #region Azure Services

        services.AddAzureClients(config => {
            var storageSetting = configuration["REPORT_BLOBS_CONNECTIONSTRING"]
                ?? throw new InvalidOperationException("REPORT_BLOBS_CONNECTIONSTRING is not set in configuration");
            var storageConnectionString = configuration.GetConnectionString(storageSetting) ?? storageSetting;

            config.AddBlobServiceClient(storageConnectionString)
                .WithName("TradeAPI");

            var keyVaultUri = configuration["KEYVAULT_URI"]
                ?? throw new InvalidOperationException("KEYVAULT_URI is not set in configuration");
            config.AddSecretClient(new Uri(keyVaultUri));
            config.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions {
                ManagedIdentityClientId = configuration["AZURE_CLIENT_ID"]
            }));
        });

        #endregion

        var functionsConfig = configuration
            .GetSection("Functions")
            .Get<FunctionsConfig>() ?? new FunctionsConfig();

        var globalConfig = configuration
            .GetSection("Global")
            .Get<GlobalConfig>() ?? new GlobalConfig();

        services.AddDbContextFactory<DatabaseContext>(options => {
            var accountEndpoint = NormaliseCosmosAccountEndpoint(configuration["Database:AccountName"] ?? configuration["Database__AccountName"] ?? string.Empty);
            var accountKey = configuration["Database:Key"] ?? configuration["Database__Key"] ?? string.Empty;

            var databaseName = ResolveCosmosDatabaseName(configuration, globalConfig.Environment);

            Console.WriteLine($"[HermesTradeServices] Using Cosmos endpoint '{accountEndpoint}' and database '{databaseName}'.");

            options.UseCosmos(
                accountEndpoint,
                accountKey,
                databaseName
            );

#if DEBUG
            options.EnableDetailedErrors();
#endif
        });

        services.AddTransient<IStorageService>(services => {
            var blobServiceClient = services.GetRequiredService<IAzureClientFactory<BlobServiceClient>>().CreateClient("TradeAPI");

            return new BlobStorageService(blobServiceClient);
        });

        #region Clients

        services.AddSingleton<ICacheService>(serviceProvider => new BlobCacheService(
            serviceProvider.GetRequiredService<IAzureClientFactory<BlobServiceClient>>().CreateClient("TradeAPI"),
            serviceProvider.GetRequiredService<ILogger<BlobCacheService>>()));

        services.AddHttpClient<CandleClient>();
        services.AddHttpClient<FxRateClient>();
        services.AddHttpClient<StockClient>();
        services.AddHttpClient<CalendarClient>();
        services.AddScoped<ISecretClient, KeyVaultSecretClient>();
        services.AddHttpClient<SlackNotificationClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<INotificationClient>(sp => sp.GetRequiredService<SlackNotificationClient>());
        var nasdaqBounceOptions = configuration
            .GetSection(NasdaqBounceOptions.SectionName)
            .Get<NasdaqBounceOptions>() ?? new NasdaqBounceOptions();
        services.AddSingleton(nasdaqBounceOptions);
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<INasdaqBounceMarketData, NasdaqBounceMarketData>();
        services.AddScoped<INasdaqBounceStore, NasdaqBounceService>();
        services.AddScoped<NasdaqBounceCalendar>();
        services.AddSingleton<INasdaqBounceSupportAnalyser, FoundryNasdaqBounceSupportAnalyser>();
        services.AddScoped<NasdaqBounceStrategy>();
        services.AddSingleton<IStrategyLock>(sp => new BlobNasdaqBounceLock(
            sp.GetRequiredService<IAzureClientFactory<BlobServiceClient>>().CreateClient("TradeAPI"),
            sp.GetRequiredService<ILogger<BlobNasdaqBounceLock>>()));

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
        services.AddScoped<UserRepository>();
        services.AddScoped<INasdaqBounceRepository, NasdaqBounceRepository>();

        services.AddSingleton(sp => {
            // ReportRepository needs Backend storage for reports container
            var backendStorage = sp.GetRequiredService<IStorageService>();

            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return new ReportRepository(backendStorage, loggerFactory);
        });

        #endregion

        #region Services

        services.AddSingleton<IApiKeyValidationService, ApiKeyValidationService>();
        services.AddScoped<UserService>();
        services.AddScoped<ReportService>();

        #region EF Core Services

        services.AddScoped<MigrationsService>();
        services.AddScoped<PortfolioService>();
        services.AddScoped<StrategyService>();
        services.AddScoped<StrategyVersionService>();
        services.AddScoped<TransactionService>();

        services.AddScoped(sp => {
            // SettingsService needs Backend storage for settings container
            var backendStorage = sp.GetRequiredService<IStorageService>();

            var logger = sp.GetRequiredService<ILoggerFactory>();

            return new SettingsService(backendStorage, logger);
        });

        #endregion

        #endregion

        #region Clients

        #endregion

        services.AddSingleton(s => services.BuildServiceProvider());

        return services;
    }

    internal static string ResolveCosmosDatabaseName(IConfiguration configuration, string environment)
    {
        var configuredDatabaseName = configuration["Database:DatabaseName"] ?? configuration["Database__DatabaseName"];

        if (!string.IsNullOrWhiteSpace(configuredDatabaseName))
        {
            return configuredDatabaseName;
        }

        return $"StockTraderAgent-{environment ?? "Development"}";
    }

    internal static string NormaliseCosmosAccountEndpoint(string? accountEndpoint)
    {
        if (string.IsNullOrWhiteSpace(accountEndpoint))
        {
            return string.Empty;
        }

        var trimmed = accountEndpoint.Trim();

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.TrimEnd('/') + "/";
        }

        if (trimmed.Contains("documents.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{trimmed.Trim('/')}";
        }

        return $"https://{trimmed.Trim('/')}.documents.azure.com:443/";
    }
}
