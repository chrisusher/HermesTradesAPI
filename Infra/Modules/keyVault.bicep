param location string = resourceGroup().location
param functionAppIdentityId string

@description('The name of the key vault')
param keyVaultName string

var tenantId = subscription().tenantId

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: [
      {
        tenantId: tenantId
        objectId: '5d05bc79-07dd-48a8-9420-43dea2004856'
        permissions: {
          secrets: ['get', 'list', 'set', 'delete', 'restore']
          keys: ['get', 'list', 'create', 'delete', 'purge', 'recover']
        }
      }
      {
        tenantId: tenantId
        objectId: functionAppIdentityId
        permissions: {
          secrets: ['get', 'list', 'set', 'delete']
          keys: ['get', 'list']
        }
      }
    ]
    tenantId: tenantId
    softDeleteRetentionInDays: 7
  }
}
