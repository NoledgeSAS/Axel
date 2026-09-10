param(
    [Parameter(Mandatory=$true)]
    [string]$ClientName,

    [Parameter(Mandatory=$true)]
    [string]$SearchServiceName,

    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory=$true)]
    [string]$FoundryName,

    [Parameter(Mandatory=$true)]
    [string]$SearchIndexName
)

$ErrorActionPreference = "Stop"


Write-Host "Déploiement de l'index Azure AI Search : $SearchIndexName"


# Chemin du template JSON
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

$templatePath = Join-Path $scriptPath "../templates/index.template.json"


if (-not (Test-Path $templatePath)) {
    throw "Template introuvable : $templatePath"
}


# Récupération de la clé admin Search

$keys = az search admin-key show `
    --resource-group $ResourceGroupName `
    --service-name $SearchServiceName `
    | ConvertFrom-Json

$adminKey = $keys.primaryKey

if (-not $adminKey) {
    throw "Impossible de récupérer la clé admin du Search Service"
}

# Récupération de la clé de la ressource Foundry

$foundryKeys = az cognitiveservices account keys list `
    --resource-group $ResourceGroupName `
    --name $FoundryName `
    | ConvertFrom-Json

$foundryApiKey = $foundryKeys.key1

if (-not $foundryApiKey) {
    throw "Impossible de récupérer la clé de la ressource Foundry."
}
# Chargement du template

$body = Get-Content $templatePath -Raw


# Remplacement des variables

$body = $body.Replace(
    "{{SearchIndexName}}",
    $SearchIndexName
)

$body = $body.Replace(
    "{{ClientName}}",
    $ClientName
)

$body = $body.Replace(
    "{{FoundryApiKey}}",
    $foundryApiKey
)

# Déploiement via API REST

$uri = "https://$SearchServiceName.search.windows.net/indexes/$SearchIndexName"
$uri += "?api-version=2024-07-01"

Invoke-RestMethod `
    -Uri $uri `
    -Method PUT `
    -Headers @{
        "api-key" = $($adminKey)
        "Content-Type" = "application/json"
    } `
    -Body $body


Write-Host "Index déployé avec succès : $SearchIndexName"