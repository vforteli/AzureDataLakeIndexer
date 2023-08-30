param EnvironmentName string = 'tklsrch'
param Location string = resourceGroup().location

resource funcStorage 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: 'srchteststg${EnvironmentName}'
  location: Location
  sku: {
    name: 'Standard_ZRS'
  }
  kind: 'StorageV2'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    isHnsEnabled: true
  }

  resource Identifier 'blobServices' = {
    name: 'default'

    resource Identifier 'containers' = {
      name: 'stuff'
    }
  }
}

resource search 'Microsoft.Search/searchServices@2022-09-01' = {
  name: 'srchhrurrdur${EnvironmentName}'
  location: Location
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'basic'
  }
  properties: {
    partitionCount: 1
    replicaCount: 1
  }
}

// azure search...
