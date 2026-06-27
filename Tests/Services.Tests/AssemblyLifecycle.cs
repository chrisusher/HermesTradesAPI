using System.Diagnostics;
using Azure.Storage.Blobs;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using Shared.Config;
using Services.Database;

namespace Services.Tests;

[SetUpFixture]
public class AssemblyLifecycle
{
    private BlobServiceClient? _blobService;
    private BlobContainerClient? _staticDataContainer;
    private static bool? _runningInActions;

    private static bool IsRunningInGitHubActions
    {
        get
        {
            if (_runningInActions == null)
            {
                _runningInActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
            }
            return _runningInActions.Value;
        }
    }

    [OneTimeSetUp]
    public async Task AssemblySetup()
    {
        var initialConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        ServiceTestsCommon.Config = initialConfig.GetSection("Tests").Get<TestConfig>();

        if (ServiceTestsCommon.Config!.LocalSetup() || IsRunningInGitHubActions)
        {
            await SetupLocalEnvironmentAsync();

            DotEnv.Load(new DotEnvOptions(ignoreExceptions: true, envFilePaths: new[]
            {
                ".env"
            }));
        }
        else
        {
            DotEnv.Load(new DotEnvOptions(ignoreExceptions: true, envFilePaths: new[]
            {
                $".env.{ServiceTestsCommon.Config!.Environment}"
            }));
        }

        ServiceTestsCommon.Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        ServiceTestsCommon.Config = ServiceTestsCommon.Configuration.GetSection("Tests").Get<TestConfig>();

        await Task.CompletedTask;
    }

    private async Task SetupLocalEnvironmentAsync()
    {
        try
        {
            DotEnv.Load(new DotEnvOptions(ignoreExceptions: false, envFilePaths: new[] { ".env" }));
        }
        catch (Exception)
        {
            if (!IsRunningInGitHubActions)
            {
                throw;
            }
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "compose -f ./docker-compose.ci.yml up -d --remove-orphans",
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        });

        using var scope = ServiceTestsCommon.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Get the named "MarketData" BlobServiceClient for MarketData storage
        _blobService = ServiceTestsCommon.Services.GetRequiredService<BlobServiceClient>();

        await CreateBlobTestDataAsync();

        // await Task.Delay(3000);

        var migrationsService = ServiceTestsCommon.Services.GetRequiredService<MigrationsService>();
        await migrationsService.ApplyMigrationsAsync();
    }

    private async Task CreateBlobTestDataAsync()
    {
        await CreateBlobContainersAsync();
    }

    private async Task CreateBlobContainersAsync()
    {
        _staticDataContainer = _blobService!.GetBlobContainerClient("reports");

        if (!await _staticDataContainer.ExistsAsync())
        {
            await _staticDataContainer.CreateIfNotExistsAsync();
        }
    }

    [OneTimeTearDown]
    public async Task AssemblyTearDownAsync()
    {
        if (ServiceTestsCommon.Config!.LocalSetup())
        {
            var stopProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "compose -f ./docker-compose.ci.yml down",
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            });

            // using var scope = ServiceTestsCommon.Services.CreateScope();
            // var dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            // await dbContext.Database.EnsureDeletedAsync();

            if (stopProcess != null)
            {
                stopProcess.WaitForExit(30_000);
            }
        }
    }
}