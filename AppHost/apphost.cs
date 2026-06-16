#:sdk Aspire.AppHost.Sdk@13.2.4
#:package Aspire.Hosting.Azure.Functions@13.2.4
#:package Aspire.Hosting.Azure.Storage@13.2.4
#:project ../Backend/Backend.csproj

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithDataVolume("data");
    });

var api = builder.AddAzureFunctionsProject("Backend", "../Backend/Backend.csproj")
    .WaitFor(storage)
    .WithArgs("--verbose", "--script-root", @"..\..\..")
    .WithHostStorage(storage)
    .WithHttpHealthCheck("/api/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
