using CLI.Commands;
using CLI.Commands.Infrastructure;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;

var rootCommand = new RootCommand("Stock Trader Agent - Execute trading strategy operations from the command line");

// Global options
var verboseOption = new Option<bool>(aliases: new[] { "--verbose", "-v" }, description: "Enable verbose logging (Information level)");
var debugEfOption = new Option<bool>(aliases: new[] { "--debug-ef" }, description: "Enable Entity Framework Core debug logging");
rootCommand.AddGlobalOption(verboseOption);
rootCommand.AddGlobalOption(debugEfOption);

rootCommand.AddCommand(new Command("infra", "Infrastructure related commands")
{
    DatabaseCommand.Create(),
});

try
{
    DotEnv.Load(new DotEnvOptions(probeForEnv: true, ignoreExceptions: true));
}
catch
{
    Console.WriteLine("Note: No .env file found, using configuration from appsettings.json and environment variables only.");
}

try
{
    // Build configuration
    var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    // Determine logging level from args (enable Information when --verbose is present)
    var verbose = args.Contains("--verbose") || args.Contains("-v");
    var debugEf = args.Contains("--debug-ef");

    // Build host for DI
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddLogging(options =>
            {
                options.AddConsole();
                options.SetMinimumLevel(verbose ? LogLevel.Information : LogLevel.Warning);

                // Suppress EF Core debug messages by default (even in verbose mode)
                if (!debugEf)
                {
                    options.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                    options.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                }
            });

            services.AddHermesTradeServices(config);
        })
        .Build();

    // Resolve services from a scoped provider to allow scoped lifetimes
    using var scope = host.Services.CreateScope();
    CommandBase.ServiceProvider = scope.ServiceProvider;

    return await rootCommand.InvokeAsync(args);
}
catch (Exception ex)
{
    Console.WriteLine($"Application error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
    }
    return 1;
}