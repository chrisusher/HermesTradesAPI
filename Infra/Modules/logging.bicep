@description('The location of the resource group')
param location string = 'uksouth'

@allowed(['TEST', 'TEMP', 'PROD'])
param environmentName string

param resourcePrefix string = 'agent-trading-api'

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${resourcePrefix}-insights-${environmentName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: '${resourcePrefix}-workspace-${environmentName}'
  scope: resourceGroup()
}

output appInsightName string = appInsights.properties.Name
output logAnalyticsName string = logAnalyticsWorkspace.name
