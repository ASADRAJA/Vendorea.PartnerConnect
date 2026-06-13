// Vendorea PartnerConnect - Azure Infrastructure
// Deploys PartnerConnect into its own resource group, modeled on the Merchant360 estate
// conventions (App Service + Azure SQL + Storage + App Insights, all on one plan).

@description('Environment name (e.g., test, preprod, prod)')
@allowed(['test', 'preprod', 'prod'])
param environment string = 'test'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name for all resources')
param baseName string = 'partnerconnect'

@description('SQL Server admin username')
param sqlAdminUsername string

@description('SQL Server admin password')
@secure()
param sqlAdminPassword string

@description('App Service Plan SKU')
@allowed(['F1', 'B1', 'B2', 'S1', 'S2', 'P1v2'])
param appServicePlanSku string = 'B1'

@description('SQL Database SKU')
@allowed(['Basic', 'S0', 'S1', 'GP_S_Gen5_1'])
param sqlDatabaseSku string = 'Basic'

@description('Base URL of the Merchant360 API this PartnerConnect talks to')
param merchant360BaseUrl string = ''

@description('Shared X-Api-Key for PartnerConnect <-> Merchant360 callbacks')
@secure()
param merchant360ApiKey string = ''

// Globally unique, short suffix for the storage account name.
var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 6)
var resourcePrefix = '${baseName}-${environment}'
var documentsContainerName = 'partner-documents'

var tags = {
  Application: 'PartnerConnect'
  Environment: environment
  ManagedBy: 'Bicep'
}

// ============================================================================
// Application Insights
// ============================================================================
module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights-deployment'
  params: {
    name: '${resourcePrefix}-insights'
    location: location
    tags: tags
  }
}

// ============================================================================
// Storage Account (raw partner documents)
// ============================================================================
module storage 'modules/storageAccount.bicep' = {
  name: 'storage-deployment'
  params: {
    name: toLower('pc${environment}${uniqueSuffix}')
    location: location
    skuName: 'Standard_LRS'
    documentsContainerName: documentsContainerName
    tags: tags
  }
}

// ============================================================================
// Azure SQL (PartnerConnect's own logical server + database)
// ============================================================================
module sqlDatabase 'modules/sqlDatabase.bicep' = {
  name: 'sql-deployment'
  params: {
    serverName: '${resourcePrefix}-sql'
    databaseName: 'PartnerConnect'
    location: location
    adminUsername: sqlAdminUsername
    adminPassword: sqlAdminPassword
    skuName: sqlDatabaseSku
    tags: tags
  }
}

// ============================================================================
// App Service Plan (shared by all PartnerConnect apps)
// ============================================================================
module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan-deployment'
  params: {
    name: '${resourcePrefix}-plan'
    location: location
    skuName: appServicePlanSku
    tags: tags
  }
}

// Shared storage app settings for the apps that read/write documents.
var storageAppSettings = [
  {
    name: 'StorageConnectionString'
    value: storage.outputs.connectionString
  }
  {
    name: 'StorageBlobEndpoint'
    value: storage.outputs.primaryEndpoint
  }
  {
    name: 'Storage__AzureBlob__ConnectionString'
    value: storage.outputs.connectionString
  }
  {
    name: 'Storage__AzureBlob__ContainerName'
    value: documentsContainerName
  }
]

var merchant360AppSettings = [
  {
    name: 'Merchant360__BaseUrl'
    value: merchant360BaseUrl
  }
  {
    name: 'Merchant360__ApiKey'
    value: merchant360ApiKey
  }
]

var sqlConnectionStrings = [
  {
    name: 'DefaultConnection'
    connectionString: sqlDatabase.outputs.connectionString
    type: 'SQLAzure'
  }
]

// ============================================================================
// API - inbound order submission from M360, admin/data APIs, M360 callbacks
// ============================================================================
module apiApp 'modules/webApp.bicep' = {
  name: 'apiApp-deployment'
  params: {
    name: '${resourcePrefix}-api'
    location: location
    appServicePlanId: appServicePlan.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    tags: tags
    appSettings: concat(storageAppSettings, merchant360AppSettings)
    connectionStrings: sqlConnectionStrings
  }
}

// ============================================================================
// Admin Portal (Blazor UI -> API)
// ============================================================================
module adminApp 'modules/webApp.bicep' = {
  name: 'adminApp-deployment'
  params: {
    name: '${resourcePrefix}-admin'
    location: location
    appServicePlanId: appServicePlan.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    tags: tags
    appSettings: [
      {
        name: 'ApiBaseUrl'
        value: 'https://${resourcePrefix}-api.azurewebsites.net'
      }
    ]
  }
}

// ============================================================================
// Background Workers (outbox -> M360 callbacks, SPR polling, doc processing)
// alwaysOn so the host stays loaded; exposes /health for App Service warmup.
// ============================================================================
module workersApp 'modules/webApp.bicep' = {
  name: 'workersApp-deployment'
  params: {
    name: '${resourcePrefix}-workers'
    location: location
    appServicePlanId: appServicePlan.outputs.id
    appInsightsConnectionString: appInsights.outputs.connectionString
    alwaysOn: true
    tags: tags
    appSettings: concat(storageAppSettings, merchant360AppSettings)
    connectionStrings: sqlConnectionStrings
  }
}

// ============================================================================
// Outputs
// ============================================================================
output apiAppUrl string = 'https://${apiApp.outputs.defaultHostname}'
output adminAppUrl string = 'https://${adminApp.outputs.defaultHostname}'
output workersAppName string = workersApp.outputs.name
output sqlServerFqdn string = sqlDatabase.outputs.serverFqdn
output storageEndpoint string = storage.outputs.primaryEndpoint
output appInsightsName string = appInsights.outputs.name
