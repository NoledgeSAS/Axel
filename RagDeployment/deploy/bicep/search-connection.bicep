// Connection au search service a ajouter dans la ressource foundry
param foundryName string
param searchServiceName string
param ressourceGroupName string
param searchServices_ai_search_noledge_basic_plan_externalid string = '/subscriptions/07cee25d-6d44-4f8b-9fed-c160e59a3993/resourceGroups/${ressourceGroupName}/providers/Microsoft.Search/searchServices/${searchServiceName}'

// récupérer la ressource foundry existante
resource foundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: foundryName
}
resource searchService 'Microsoft.Search/searchServices@2025-05-01' existing = {
  name: searchServiceName
}


resource search_service_connection 'Microsoft.CognitiveServices/accounts/connections@2026-05-01' = {
  parent: foundry
  name: '${searchServiceName}-connection'
  properties: {
    authType: 'ApiKey'
    credentials: {
      key: searchService.listAdminKeys().primaryKey
    }
    category: 'CognitiveSearch'
    target: 'https://${searchServiceName}.search.windows.net/'
    useWorkspaceManagedIdentity: false
    isSharedToAll: false
    sharedUserList: []
    peRequirement: 'NotRequired'
    peStatus: 'NotApplicable'
    metadata: {
      displayName: searchServiceName
      type: 'azure_ai_search'
      ApiType: 'Azure'
      ResourceId: searchServices_ai_search_noledge_basic_plan_externalid
      ApiVersion: '2024-05-01-preview'
      DeploymentApiVersion: '2023-11-01'
    }
  }
}
