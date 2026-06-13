@description('Name of the App Service Plan')
param name string

@description('Azure region for the App Service Plan')
param location string = resourceGroup().location

@description('SKU name for the App Service Plan')
@allowed(['F1', 'B1', 'B2', 'S1', 'S2', 'P1v2', 'P2v2'])
param skuName string = 'B1'

@description('Tags to apply to resources')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    capacity: 1
  }
  properties: {
    reserved: false // false = Windows, true = Linux
  }
}

output id string = appServicePlan.id
output name string = appServicePlan.name
