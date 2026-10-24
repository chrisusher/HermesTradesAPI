var environmentName = 'TEST'
var location = resourceGroup().location

var keyVaultName = 'agent-trading-${environmentName}-kv'

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

module functionApp '../../Modules/functionApp.bicep' = {
  name: 'functionApp'
  dependsOn: [
    keyVaultReference
  ]
  params: {
    appInsightsName: logging.outputs.appInsightName
    cosmosKey: keyVaultReference.getSecret('CosmosKey')
    dataStorageAccountName: dataStorageAccount.outputs.storageAccountName
    environmentName: environmentName
    location: location
    keyVaultName: keyVaultName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
    storageAccountName: functionsStorageAccount.outputs.storageAccountName
    swaggerApiKey: keyVaultReference.getSecret('SWAGGER-API-KEY')
    swaggerApiVersion: '0.16.0'
  }
}
