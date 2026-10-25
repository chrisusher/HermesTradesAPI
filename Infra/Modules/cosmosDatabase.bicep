@secure()
@description('Endpoint URL for the configured Cosmos DB account, such as https://example.documents.azure.com:443/.')
param cosmosAccountUrl string

@description('Name of the SQL database to create in the Cosmos DB account.')
param databaseName string

var cosmosAccountName = split(replace(replace(replace(cosmosAccountUrl, 'https://', ''), 'http://', ''), '/', ''), '.')[0]

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

var containers = [
  {
    name: 'Migrations'
    partitionKeyPath: '/id'
  }
  {
    name: 'Portfolio'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'PortfolioHoldings'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'Settings'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'Strategies'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'StrategyExecutions'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'StrategyVersions'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'Transactions'
    partitionKeyPath: '/PartitionKey'
  }
  {
    name: 'Users'
    partitionKeyPath: '/PartitionKey'
  }
]

resource sqlContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = [for container in containers: {
  parent: database
  name: container.name
  properties: {
    resource: {
      id: container.name
      partitionKey: {
        paths: [
          container.partitionKeyPath
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}]

output databaseId string = database.id
output containerNames array = [for container in containers: container.name]
