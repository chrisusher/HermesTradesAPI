@description('The location of the resource group')
param location string = 'uksouth'

@description('Prefix for the storage account name')
param accountPrefix string = 'agenttradesfunc'

@description('Random integer to ensure unique storage account name')
var randomInteger = uniqueString(az.resourceGroup().id, '')

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${accountPrefix}${randomInteger}'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    allowSharedKeyAccess: true
    allowCrossTenantReplication: false
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource releasesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'function-releases'
  properties: {
    publicAccess: 'None'
  }
}

resource lifecycleManagement 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'ManageFunctionReleases'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 90
                }
                delete: {
                  daysAfterModificationGreaterThan: 365
                }
              }
              snapshot: {
                delete: {
                  daysAfterCreationGreaterThan: 30
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'function-releases/'
              ]
            }
          }
        }
      ]
    }
  }
}

output storageAccountName string = storageAccount.name
