param EnvironmentName string = 'dev'
param Location string = resourceGroup().location
param AppName string = 'tklsrch'

resource datalakeThingy 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'srchteststg${AppName}'
  location: Location
  sku: { name: 'Standard_ZRS' }
  kind: 'StorageV2'
  identity: { type: 'SystemAssigned' }
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

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${AppName}sb'
  location: Location
  sku: {
    name: 'Basic'
  }

  resource blobCreatedEventQueue 'queues' = {
    name: 'blob-created-event-queue'
  }

  resource blobDeletedEventQueue 'queues' = {
    name: 'blob-deleted-event-queue'
  }
}

resource search 'Microsoft.Search/searchServices@2022-09-01' = {
  name: 'srchhrurrdur${AppName}'
  location: Location
  identity: { type: 'SystemAssigned' }
  sku: { name: 'basic' }
  properties: {
    partitionCount: 1
    replicaCount: 1
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${AppName}-workspace-${EnvironmentName}'
  location: Location
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${AppName}-appinsights-${EnvironmentName}'
  location: Location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'hp${EnvironmentName}'
  location: Location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: { reserved: true }
}

resource funcStorage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'func${AppName}${EnvironmentName}'
  location: Location
  sku: {
    name: 'Standard_ZRS'
  }
  kind: 'StorageV2'
  identity: {
    type: 'SystemAssigned'
  }
}

var datalakeIndexerFuncName = 'datalake-indexer-func-${EnvironmentName}'
resource datalakeIndexerFunc 'Microsoft.Web/sites@2022-09-01' = {
  name: datalakeIndexerFuncName
  location: Location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    reserved: true
    serverFarmId: hostingPlan.id
    siteConfig: {
      functionAppScaleLimit: 1
      linuxFxVersion: 'DOTNET-ISOLATED|7.0'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: funcStorage.name
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${funcStorage.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorage.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(datalakeIndexerFuncName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AzureSearchServiceUri'
          value: 'https://${search.name}.search.windows.net'
        }
        {
          name: 'DatalakeConnectionString'
          value: 'https://${datalakeThingy.name}.dfs.core.windows.net'
        }
        {
          name: 'ServiceBusConnection__fullyQualifiedNamespace'
          value: '${serviceBus.name}.servicebus.windows.net'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
      ]
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v7.0'
    }
    httpsOnly: true
  }
}

resource systemTopic 'Microsoft.EventGrid/systemTopics@2023-06-01-preview' = {
  name: 'sometopic'
  location: Location
  properties: {
    source: datalakeThingy.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }

  resource blobCreatedToQueueSubscription 'eventSubscriptions' = {
    name: 'blobCreatedToQueueSubscription'
    properties: {
      destination: {
        properties: {
          resourceId: serviceBus::blobCreatedEventQueue.id
        }
        endpointType: 'ServiceBusQueue'
      }
      filter: {
        includedEventTypes: [
          'Microsoft.Storage.BlobCreated'
        ]
      }
    }
  }

  resource blobDeletedToQueueSubscription 'eventSubscriptions' = {
    name: 'blobDeletedToQueueSubscription'
    properties: {
      destination: {
        properties: {
          resourceId: serviceBus::blobDeletedEventQueue.id
        }
        endpointType: 'ServiceBusQueue'
      }
      filter: {
        includedEventTypes: [
          'Microsoft.Storage.BlobDeleted'
        ]
      }
    }
  }
}

resource searchIndexDataContributor 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
}

resource serviceBusReceiverRole 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
}

resource storageBlobDataContributor 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
}

resource funcSearchDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, datalakeIndexerFuncName, 'azuresearchdatacontributor')
  properties: {
    principalType: 'ServicePrincipal'
    principalId: datalakeIndexerFunc.identity.principalId
    roleDefinitionId: searchIndexDataContributor.id
  }
}

resource functionSBRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name: guid(resourceGroup().id, datalakeIndexerFuncName, serviceBusReceiverRole.name)
  scope: serviceBus
  properties: {
    principalType: 'ServicePrincipal'
    principalId: datalakeIndexerFunc.identity.principalId
    roleDefinitionId: serviceBusReceiverRole.id
  }
}

resource functionStorageDataContributor 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name: guid(resourceGroup().id, datalakeIndexerFuncName, storageBlobDataContributor.name, funcStorage.name)
  scope: funcStorage
  properties: {
    principalType: 'ServicePrincipal'
    principalId: datalakeIndexerFunc.identity.principalId
    roleDefinitionId: storageBlobDataContributor.id
  }
}

resource functionDatalakeStorageDataContributor 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name: guid(resourceGroup().id, datalakeIndexerFuncName, storageBlobDataContributor.name)
  scope: datalakeThingy
  properties: {
    principalType: 'ServicePrincipal'
    principalId: datalakeIndexerFunc.identity.principalId
    roleDefinitionId: storageBlobDataContributor.id
  }
}
