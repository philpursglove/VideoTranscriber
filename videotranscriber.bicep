targetScope = 'subscription'

resource rgvideotranscriber 'Microsoft.Resources/resourceGroups@2020-06-01' = {
  name: 'rgvideotranscriber'
  location: 'uksouth'
}

module storage 'StorageAccount.bicep' = {
  name: 'storage'
  scope: rgvideotranscriber
  params: {
  }
}

module cosmos 'CosmosDb.bicep' = {
  name: 'cosmos'
  scope: rgvideotranscriber
  params: {
  }
}

module functionapp 'FunctionApp.bicep' = {
  name: 'function'
  scope: rgvideotranscriber
  params: {
    storageAccountName: storage.outputs.storageAccountName
  }
}
