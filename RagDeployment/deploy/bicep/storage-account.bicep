targetScope = 'resourceGroup'

param storageAccountName string
param location string

// TODO: A jouter le "blob soft delete" sur 7 jours

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  location: location
  name: storageAccountName
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  name: 'default'
  parent: storageAccount

  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }

    containerDeleteRetentionPolicy: {
      enabled: true
      days: 7
    }
    changeFeed: {
      enabled: true
    }
  }
}
