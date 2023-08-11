resource databaseAccount 'Microsoft.DocumentDB/databaseAccounts@2023-04-15' = {
  name: 'cosmosvideotranscriber'
  location: 'uksouth'
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: 'uksouth'
        failoverPriority: 0
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-04-15' = {
  name: 'transcriptions'
  location: 'uksouth'
  parent: databaseAccount
  properties: {
    resource: {
      id: 'transcriptions'
    }
    options: {
      throughput: 400
    }
  }
}
