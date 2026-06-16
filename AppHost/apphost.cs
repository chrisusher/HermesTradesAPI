#:sdk Aspire.AppHost.Sdk@13.3.0
#:package Aspire.Hosting.Azure.CosmosDB@13.3.0
#:package Aspire.Hosting.Azure.Functions@13.3.0
#:package Aspire.Hosting.Azure.Storage@13.3.0
#:project ../Backend/Backend.csproj

var builder = DistributedApplication.CreateBuilder(args);

#region Parameters
var environment = builder.AddParameter("environment", "dev", true);
var existingCosmosName = builder.AddParameter("cosmosName", "movemate-cosmos-db-82233", true);
var existingCosmosResourceGroup = builder.AddParameter("cosmosResourceGroup", "Shared-Resources", true);
#endregion

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithDataVolume("data");
    });

var environmentName = builder.Configuration["environment"] ?? "dev";

#region CosmosDB

// Point to my existing Dev CosmosDB Account in Azure
var cosmos = builder.AddAzureCosmosDB("CosmosDB")
    .AsExisting(existingCosmosName, existingCosmosResourceGroup);

var database = cosmos.AddCosmosDatabase("Database", $"StockTraderAgent-{environmentName}");
    
#endregion

var api = builder.AddAzureFunctionsProject("Backend", "../Backend/Backend.csproj")
    .WaitFor(database)
    .WaitFor(storage)
    .WithArgs("--verbose", "--script-root", @"..\..\..")
    .WithHostStorage(storage)
    .WithHttpHealthCheck("/api/health")
    .WithExternalHttpEndpoints()
    .WithReference(database);

builder.Build().Run();
