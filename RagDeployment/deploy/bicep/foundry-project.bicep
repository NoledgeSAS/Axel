targetScope = 'resourceGroup'

param foundryName string
param foundryProjectName string
param location string

resource foundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: foundryName
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2026-05-01' = {
  name: foundryProjectName
  parent: foundry
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {}
}
