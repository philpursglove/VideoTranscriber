resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
  name: 'stvideotranscriber'
  location: 'uksouth'
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
  properties: {
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: 'videos'
  parent: blobServices
  properties: {
    publicAccess: 'Blob'
  }
}
