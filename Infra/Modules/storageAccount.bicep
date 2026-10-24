@description('The location of the resource group')
param location string = 'uksouth'

@allowed(['TEST', 'TEMP', 'PROD'])
param environmentName string

@description('Prefix for the storage account name')
param accountPrefix string = 'agenttradingdata'

var environmentLower = toLower(environmentName)

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${accountPrefix}${environmentLower}'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
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
    lastAccessTimeTrackingPolicy: {
      enable: true
    }
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

resource aiAnalysisBlobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'ai-analysis'
  properties: {
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource cacheBlobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'cache'
  properties: {
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
    publicAccess: 'None'
  }
}

resource settingsBlobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  parent: blobService
  name: 'settings'
  properties: {
    defaultEncryptionScope: '$account-encryption-key'
    denyEncryptionScopeOverride: false
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
          name: 'ManageCacheData'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterLastAccessTimeGreaterThan: 30
                }
              }
              snapshot: {
                delete: {
                  daysAfterCreationGreaterThan: 7
                }
              }
            }
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'cache/'
              ]
            }
          }
        }
        {
          enabled: true
          name: 'ManageAIAnalysis'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterLastAccessTimeGreaterThan: 30
                }
                tierToArchive: {
                  daysAfterLastAccessTimeGreaterThan: 180
                }
                delete: {
                  daysAfterLastAccessTimeGreaterThan: 730 // 2 years
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
                'ai-analysis/'
              ]
            }
          }
        }
        {
          enabled: true
          name: 'ManageStrategyAIComments'
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                delete: {
                  daysAfterCreationGreaterThan: 30
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
                'ai-analysis/Strategy-'
              ]
            }
          }
        }
      ]
    }
  }
}

output storageAccountName string = storageAccount.name
