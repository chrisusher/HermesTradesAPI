@description('Resource group that contains the configured Cosmos DB account.')
param cosmosResourceGroupName string = 'Shared-Resources'

var environmentName = 'TEST'
var location = resourceGroup().location

var keyVaultName = 'agent-trading-${environmentName}-kv'
var databaseName = 'agent-trading-${toLower(environmentName)}'

module managedIdentity '../../Modules/managedIdentity.bicep' = {
  name: 'managed-identity'
  params: {
    location: location
    environmentName: environmentName
  }
}

module functionsStorageAccount '../../Modules/functionsStorageAccount.bicep' = {
  name: 'functions-storage'
  params: {
    location: location
  }
}

module dataStorageAccount '../../Modules/storageAccount.bicep' = {
  name: 'data-storage'
  params: {
    environmentName: environmentName
    location: location
  }
}

module logging '../../Modules/logging.bicep' = {
  name: 'logging'
  params: {
    environmentName: environmentName
    location: location
  }
}

module keyVault '../../Modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    functionAppIdentityId: managedIdentity.outputs.managedIdentityPrincipalId
    keyVaultName: keyVaultName
  }
}

resource keyVaultReference 'Microsoft.KeyVault/vaults@2023-02-01' existing = {
  name: keyVaultName
}

module cosmosDatabase '../../Modules/cosmosDatabase.bicep' = {
  name: 'cosmos-database'
  scope: resourceGroup(cosmosResourceGroupName)
  params: {
    cosmosAccountUrl: keyVaultReference.getSecret('CosmosUrl')
    databaseName: databaseName
  }
}

module functionApp '../../Modules/functionApp.bicep' = {
  name: 'functionApp'
  dependsOn: [
    keyVaultReference
    cosmosDatabase
  ]
  params: {
    appInsightsName: logging.outputs.appInsightName
    cosmosDatabase: databaseName
    cosmosKey: keyVaultReference.getSecret('CosmosKey')
    cosmosUrl: keyVaultReference.getSecret('CosmosUrl')
    dataStorageAccountName: dataStorageAccount.outputs.storageAccountName
    environmentName: environmentName
    location: location
    keyVaultName: keyVaultName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    storageAccountName: functionsStorageAccount.outputs.storageAccountName
    swaggerApiKey: keyVaultReference.getSecret('SWAGGER-API-KEY')
    swaggerApiVersion: '0.1.0'
  }
}
