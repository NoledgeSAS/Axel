$ErrorActionPreference = "Stop"

# Chemins
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

$configPath = Join-Path $scriptPath "parameters/customer.json"

$bicepFolder = Join-Path $scriptPath "bicep"

$bicepFiles = @{
    ResourceGroup = Join-Path $bicepFolder "resource-group.bicep"
    StorageAccount = Join-Path $bicepFolder "storage-account.bicep"
    BlobContainer = Join-Path $bicepFolder "blob-container.bicep"
    SearchService = Join-Path $bicepFolder "search-service.bicep"
    SearchIndex = Join-Path $bicepFolder "search-index.bicep"
    SearchIndexer = Join-Path $bicepFolder "search-indexer.bicep"
    SearchDatasource = Join-Path $bicepFolder "search-datasource.bicep"
    SearchSkillset = Join-Path $bicepFolder "search-skillset.bicep"
    Foundry = Join-Path $bicepFolder "foundry.bicep"
    FoundryProject = Join-Path $bicepFolder "foundry-project.bicep"
    FoundrySearchConnection = Join-Path $bicepFolder "search-connection.bicep"
}

# Lecture configuration client
$config = Get-Content $configPath | ConvertFrom-Json

$clientName = $config.clientName.ToLower()
$location = $config.location
$agentInstructions = $config.agentInstructions


# Convention de nommage
$names = @{
    ResourceGroup = "RG_IA_$clientName"
    StorageAccount = "sa" + $clientName + "noledge"
    BlobContainer = "$clientName-blob-container"
    Foundry = "$clientName-foundry-noledge"
    ModelDeployement = "$clientName-model-deployment"
    FoundryProject = "$clientName-foundry-project"
    Agent = "$clientName-title-agent"
    SearchService = "$clientName-search-service-noledge"
    SearchDatasource = "$clientName-datasource"
    SearchIndex = "$clientName-index"
    SearchSkillset = "$clientName-skillset"
    SearchIndexer = "$clientName-indexer"
}

Write-Host "Déploiement pour le client : $clientName"
Write-Host "Resource Group : " + $names.ResourceGroup
Write-Host "Location : $location"


# Vérification connexion Azure
$account = az account show | ConvertFrom-Json

if (-not $account) {
    Write-Error "Aucune connexion Azure détectée. Lancez 'az login'"
}

# Déploiement Bicep

Write-Host "=== Resource Group - Name : $($names.ResourceGroup) ==="

az deployment sub create `
    --name "deploy-$clientName" `
    --location $location `
    --template-file $bicepFiles.ResourceGroup `
    --parameters `
        resourceGroupName=$($names.ResourceGroup) `
        location=$location

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep échoué."
}


Write-Host "=== Storage Account - Name : $($names.StorageAccount) ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.StorageAccount `
    --parameters `
        storageAccountName=$($names.StorageAccount) `
        location=$location

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep échoué."
}

Write-Host "=== Blob Container - Name :  $($names.BlobContainer) ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.BlobContainer `
    --parameters `
        storageAccountName=$($names.StorageAccount) `
        blobContainerName=$($names.BlobContainer)

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep échoué."
}

Write-Host "=== Foundry - Name :  $($names.Foundry) ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.Foundry `
    --parameters `
        foundryName=$($names.Foundry) `
        modelDeploymentName=$($names.ModelDeployement) `
        location=$location

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep echoue."
}

Write-Host "=== Foundry Project - Name :  $($names.FoundryProject) ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.FoundryProject `
    --parameters `
        foundryName=$($names.Foundry) `
        foundryProjectName=$($names.FoundryProject) `
        location=$location

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep échoué."
}

Write-Host "=== Agent - Name :  $($names.Agent) ==="

$searchScript = Join-Path $scriptPath "powershell/foundry/Deploy-Agent.ps1"

& $searchScript `
    -AgentName $names.Agent `
    -FoundryName $names.Foundry `
    -FoundryProjectName $names.FoundryProject `
    -ResourceGroupName $names.ResourceGroup `
    -ModelDeploymentName $names.ModelDeployement `
    -AgentInstructions $agentInstructions

Write-Host "=== Search Service - Name :  $($names.SearchService) ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.SearchService `
    --parameters `
        searchServiceName=$($names.SearchService) `
        location=$location

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep échoué."
}

Write-Host "=== Search Datasource - Name :  $($names.SearchDatasource) ==="

$searchScript = Join-Path $scriptPath "powershell/search/Deploy-SearchDatasource.ps1"

& $searchScript `
    -SearchServiceName $names.SearchService `
    -DatasourceName $names.SearchDatasource `
    -StorageAccountName $names.StorageAccount `
    -BlobContainerName $names.BlobContainer `
    -ResourceGroupName $names.ResourceGroup


Write-Host "=== Search Index - Name :  $($names.SearchIndex) ==="

$searchScript = Join-Path $scriptPath "powershell/search/Deploy-SearchIndex.ps1"

& $searchScript `
    -ClientName $clientName `
    -SearchServiceName $names.SearchService `
    -ResourceGroupName $names.ResourceGroup `
    -FoundryName $($names.Foundry) `
    -SearchIndexName $names.SearchIndex 

Write-Host "=== Search SkillSet - Name :  $($names.SearchSkillset) ==="

$searchScript = Join-Path $scriptPath "powershell/search/Deploy-SearchSkillset.ps1"

& $searchScript `
    -ClientName $clientName `
    -SearchServiceName $names.SearchService `
    -ResourceGroupName $names.ResourceGroup `
    -SkillsetName $names.SearchSkillset `
    -FoundryName $names.Foundry `
    -SearchIndexName $names.SearchIndex 

Write-Host "=== Search Indexer - Name :  $($names.SearchIndexer) ==="

$searchScript = Join-Path $scriptPath "powershell/search/Deploy-SearchIndexer.ps1"

& $searchScript `
    -ClientName $clientName `
    -SearchServiceName $names.SearchService `
    -ResourceGroupName $names.ResourceGroup `
    -IndexerName $names.SearchIndexer `
    -DataSourceName $names.SearchDatasource `
    -SkillsetName $names.SearchSkillset `
    -IndexName $names.SearchIndex 

Write-Host "=== Ajout de la connexion au searchService dans la ressource Foundry ==="

az deployment group create `
    --resource-group $names.ResourceGroup `
    --template-file $bicepFiles.FoundrySearchConnection `
    --parameters `
        foundryName=$($names.Foundry) `
        searchServiceName=$($names.SearchService) `
        ressourceGroupName=$($names.RessourceGroup)

if ($LASTEXITCODE -ne 0) {
    throw "Le déploiement Bicep echoue."
}

Write-Host "Déploiement terminé"