@description('Name of the SQL Server')
param serverName string

@description('Name of the SQL Database')
param databaseName string

@description('Azure region')
param location string = resourceGroup().location

@description('SQL Admin username')
param adminUsername string

@description('SQL Admin password')
@secure()
param adminPassword string

@description('SKU name for the database')
@allowed(['Basic', 'S0', 'S1', 'S2', 'GP_S_Gen5_1', 'GP_S_Gen5_2'])
param skuName string = 'Basic'

@description('Tags to apply to resources')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: adminUsername
    administratorLoginPassword: adminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow other Azure services (the App Services) to reach the SQL server.
resource sqlServerFirewallAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName == 'Basic' ? 'Basic' : (startsWith(skuName, 'GP') ? 'GeneralPurpose' : 'Standard')
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: skuName == 'Basic' ? 2147483648 : 268435456000 // 2GB for Basic, 250GB otherwise
  }
}

output serverId string = sqlServer.id
output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseId string = sqlDatabase.id
output databaseName string = sqlDatabase.name
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${databaseName};Persist Security Info=False;User ID=${adminUsername};Password=${adminPassword};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
