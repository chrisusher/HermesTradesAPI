@description('The location of the resource group')
param location string = 'uksouth'

@description('The environment name')
@allowed(['TEST', 'TEMP', 'PROD'])
param environmentName string

@description('The name of the managed identity')
param resourcePrefix string = 'agent-trading-api'

// User-assigned managed identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-mi-${environmentName}'
  location: location
  tags: {
    environment: environmentName
    purpose: 'function-app-identity'
  }
}

// Outputs
output managedIdentityId string = managedIdentity.id
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityName string = managedIdentity.name
