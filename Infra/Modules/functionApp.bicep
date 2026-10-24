param appInsightsName string

@description('The location of the resource group')
param location string = 'uksouth'

@allowed(['TEST', 'TEMP', 'PROD'])
param environmentName string

param cosmosDatabase string

@secure()
param cosmosKey string

@secure()
param cosmosUrl string

@description('The name of the data storage account')
param dataStorageAccountName string

@description('The name of the key vault')
param keyVaultName string

// @description('Connection string of the Service Bus')
// param serviceBusConnectionString string

@description('The resource ID of the managed identity')
param managedIdentityId string

@description('The client ID of the managed identity')
param managedIdentityClientId string

@description('The name of the function storage account')
param storageAccountName string

@description('The Version of the API being used for Swagger documentation')
param swaggerApiVersion string

@secure()
param swaggerApiKey string

var storageAccountKeys = listKeys(resourceId('Microsoft.Storage/storageAccounts', storageAccountName), '2022-09-01')
var storageAccountKey = storageAccountKeys.keys[0].value

var storageSuffix = environment().suffixes.storage

// Construct the connection string
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccountKey};EndpointSuffix=${storageSuffix}'

var dataStorageAccountKeys = listKeys(resourceId('Microsoft.Storage/storageAccounts', dataStorageAccountName), '2022-09-01')
var dataStorageAccountKey = dataStorageAccountKeys.keys[0].value

var dataStorageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${dataStorageAccountName};AccountKey=${dataStorageAccountKey};EndpointSuffix=${storageSuffix}'

var appPrefix = 'agent-trading-functions'
var appName = '${appPrefix}-${environmentName}'

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

// Flex Consumption Plan for the Function App
resource servicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${appPrefix}-plan-${environmentName}'
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // Reserved must be true for Linux plans
  }
}

// Linux Function App with Flex Consumption
resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: servicePlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storageAccountName}.blob.${storageSuffix}/function-releases'
          authentication: {
            storageAccountConnectionStringName: 'AzureWebJobsStorage'
            type: 'StorageAccountConnectionString'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 40
        instanceMemoryMB: 512
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'AZURE_FUNCTIONS_ENVIRONMENT'
          value: environmentName
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        {
          name: 'AZURE_KEYVAULT_RESOURCEENDPOINT'
          value: ''
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'Aspire__Microsoft__EntityFrameworkCore__Cosmos__cosmosDbName__AccountEndpoint'
          value: cosmosUrl
        }
        {
          name: 'Aspire__Microsoft__EntityFrameworkCore__Cosmos__cosmosDbName__DatabaseName'
          value: cosmosDatabase
        }
        {
          name: 'Database__Key'
          value: cosmosKey
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'Functions__IsDebug'
          value: 'false'
        }
        {
          name: 'KeyVault__KeyVaultName'
          value: keyVaultName
        }
        {
          name: 'OpenApi__DocVersion'
          value: swaggerApiVersion
        }
        {
          name: 'REPORT_BLOBS_CONNECTIONSTRING'
          value: dataStorageConnectionString
        }
        {
          name: 'Swagger__ApiKey'
          value: swaggerApiKey
        }
      ]
      localMySqlEnabled: false
    }
    httpsOnly: true
  }
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
}

output functionAppId string = functionApp.id
output functionAppIdentity string = managedIdentityId
output functionAppName string = functionApp.name
