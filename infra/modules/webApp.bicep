@description('Name of the Web App')
param name string

@description('Azure region')
param location string = resourceGroup().location

@description('App Service Plan ID')
param appServicePlanId string

@description('Application Insights Connection String')
param appInsightsConnectionString string = ''

@description('Keep the app loaded (required for the background workers app)')
param alwaysOn bool = false

@description('App settings')
param appSettings array = []

@description('Connection strings')
param connectionStrings array = []

@description('Tags to apply to resources')
param tags object = {}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: alwaysOn
      http20Enabled: true
      minTlsVersion: '1.2'
      appSettings: concat([
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
      ], appSettings)
      connectionStrings: connectionStrings
    }
  }
}

output id string = webApp.id
output name string = webApp.name
output defaultHostname string = webApp.properties.defaultHostName
