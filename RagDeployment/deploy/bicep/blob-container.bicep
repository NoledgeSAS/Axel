targetScope = 'resourceGroup'

param storageAccountName string
param blobContainerName string

// Récupérer l'objet Storage Account existant que l'on viens de créer (Le mot important est : "existing")
resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

// Récupérer le blobservice par défaut du Storage Account existant 
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' existing = {
  name: 'default'
  parent: storageAccount
}

// Créer le blob container dans le blob service existant
resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  
  name: blobContainerName

  parent: blobService

  properties: {
    publicAccess: 'None'
  }
}
