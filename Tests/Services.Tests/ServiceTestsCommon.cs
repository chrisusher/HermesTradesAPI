using Bogus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Serilog;
using Shared.Config;

namespace Services.Tests;

public static class ServiceTestsCommon
{
    private static Faker? _faker;
    private static ServiceProvider? _services;

    public static IConfigurationRoot? Configuration { get; internal set; }

    public static TestConfig? Config { get; internal set; }

    public static Faker Faker
    {
        get
        {
            if (_faker == null)
            {
                _faker = new Faker();
            }
            return _faker;
        }
    }

    public static ServiceProvider Services
    {
        get
        {
            if (_services == null)
            {
                _services = RegisterServices();
            }
            return _services;
        }
    }

    public static DateTime RandomDate
    {
        get
        {
            var latest = DateTime.UtcNow.AddYears(-1);
            var earliest = DateTime.UtcNow.AddYears(-11);

            var date = Faker.Date.Between(earliest, latest);

            return new(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Utc);
        }
    }

    private static ServiceProvider RegisterServices()
    {
        var services = new ServiceCollection();

        // Configure Serilog from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        Configuration = configuration;

        // Create Serilog logger
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Add Serilog to the DI container
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(Log.Logger);
        });

        services.AddHermesTradeServices(Configuration!);
        
        // Test-only: register a default (unnamed) BlobServiceClient so services that inject
        // BlobServiceClient (without a name) resolve successfully (e.g., ExchangeDataService)
        services.AddAzureClients(config =>
        {
            var cs = "UseDevelopmentStorage=true";
            config.AddBlobServiceClient(cs);
        });
        
        services.AddSingleton<IConfiguration>(Configuration!);

        var serviceCollection = services.BuildServiceProvider();

        return serviceCollection;
    }
}