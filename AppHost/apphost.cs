#:sdk Aspire.AppHost.Sdk@13.4.4
#:package Aspire.Hosting.Azure.CosmosDB@13.4.4
#:package Aspire.Hosting.Azure.Functions@13.4.4
#:package Aspire.Hosting.Azure.KeyVault@13.4.5
#:package Aspire.Hosting.Azure.Storage@13.4.4
#:project ../Backend/Backend.csproj

var builder = DistributedApplication.CreateBuilder(args);

#region Parameters
    
var apiKey = builder.AddParameter("apiKey", true);
var environment = builder.AddParameter("environment", false);
var existingCosmosName = builder.AddParameter("cosmosName", true);
var existingCosmosResourceGroup = builder.AddParameter("cosmosResourceGroup", "Shared-Resources", true);
var cosmosKey = builder.AddParameter("cosmosKey", true);

#endregion

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        azurite.WithDataVolume("data");
    });

var blobService = storage.AddBlobs("blobs");
var reportContainer = storage.AddBlobContainer("Report-Blobs", "reports");

#region KeyVault

var keyVault = builder.AddAzureKeyVault("KeyVault");

keyVault.AddSecret("API-KEY", apiKey);

#endregion

#region CosmosDB

// Point to my existing Dev CosmosDB Account in Azure. Do not create a new database resource here.
var cosmos = builder.AddAzureCosmosDB("CosmosDB")
    .AsExisting(existingCosmosName, existingCosmosResourceGroup);

var database = cosmos.AddCosmosDatabase("Database", $"StockTraderAgent-{builder.Configuration["environment"]}");
    
#endregion

var api = builder.AddAzureFunctionsProject("Backend", "../Backend/Backend.csproj")
    .WaitFor(database)
    .WaitFor(storage)
    .WithArgs("", "--script-root", @"..\..\..")
    .WithEnvironment("Database__AccountName", existingCosmosName)
    .WithEnvironment("Database__Key", cosmosKey)
    .WithEnvironment("Global__Environment", environment)
    .WithHostStorage(storage)
    .WithHttpHealthCheck("/api/health")
    .WithExternalHttpEndpoints()
    .WithReference(database)
    .WithReference(keyVault)
    .WithReference(reportContainer);

builder.Build().Run();
