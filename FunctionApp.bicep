param storageAccountName string

resource appServicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name: 'planvideotranscriber'
  location: 'uksouth'
  sku: {
    name: 'B1'
    capacity: 1
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' existing = {
  name: storageAccountName
}
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: 'funcvideotranscriber'
  location: 'uksouth'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, '2019-06-01').keys[0].value}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
      ]
    }
  }
  dependsOn: [
    appServicePlan
  ]
}
