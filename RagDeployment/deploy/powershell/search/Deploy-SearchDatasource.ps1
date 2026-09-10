param(
    [Parameter(Mandatory=$true)]
    [string]$SearchServiceName,

    [Parameter(Mandatory=$true)]
    [string]$DatasourceName,

    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName,

    [Parameter(Mandatory=$true)]
    [string]$BlobContainerName,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName
)


$ErrorActionPreference = "Stop"


Write-Host "Création de la datasource Azure AI Search : $DatasourceName"


# Récupération de la clé admin du Search Service
$keys = az search admin-key show `
    --resource-group $ResourceGroupName `
    --service-name $SearchServiceName `
    | ConvertFrom-Json


$adminKey = $keys.primaryKey


if (-not $adminKey) {
    throw "Impossible de récupérer la clé admin du Search Service"
}

# Récupération de la clé du Storage Account

$storageKey = az storage account keys list `
    --resource-group $ResourceGroupName `
    --account-name $StorageAccountName `
    | ConvertFrom-Json


$storageAccountKey = $storageKey[0].value


if (-not $storageAccountKey) {
    throw "Impossible de récupérer la clé du Storage Account"
}


# Construction de la datasource (json de la datasource)

$body = @{
    name = $DatasourceName

    type = "azureblob"

    credentials = @{
        connectionString = "DefaultEndpointsProtocol=https;AccountName=$StorageAccountName;AccountKey=$storageAccountKey;EndpointSuffix=core.windows.net"
    }

    container = @{
        name = $BlobContainerName
    }

    dataDeletionDetectionPolicy = @{
        "@odata.type" = "#Microsoft.Azure.Search.NativeBlobSoftDeleteDeletionDetectionPolicy"
    }

} | ConvertTo-Json -Depth 10

# Création via API REST

$uri = "https://$SearchServiceName.search.windows.net/datasources/$DatasourceName"
$uri += "?api-version=2024-07-01"

Invoke-RestMethod `
    -Uri $uri `
    -Method PUT `
    -Headers @{
        "api-key" = $adminKey
        "Content-Type" = "application/json"
    } `
    -Body $body


Write-Host "Datasource créée avec succès"