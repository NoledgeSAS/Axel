param(
    [Parameter(Mandatory = $true)]
    [string]$ClientName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$SearchServiceName,

    [Parameter(Mandatory = $true)]
    [string]$SkillsetName,

    [Parameter(Mandatory = $true)]
    [string]$SearchIndexName,

    [Parameter(Mandatory = $true)]
    [string]$FoundryName
)
$ErrorActionPreference = "Stop"

Write-Host "Déploiement du Skillset Azure AI Search : $SkillsetName"

# Chemin du template

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$templatePath = Join-Path $scriptPath "../templates/skillset.template.json"

if (-not (Test-Path $templatePath)) {
    throw "Template introuvable : $templatePath"
}

# Clé admin Search

$keys = az search admin-key show `
    --resource-group $ResourceGroupName `
    --service-name $SearchServiceName `
    | ConvertFrom-Json

$adminKey = $keys.primaryKey

if (-not $adminKey) {
    throw "Impossible de récupérer la clé admin du Search Service."
}

# Clé Foundry

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

# Remplacement des placeholders

$body = $body.Replace(
    "{{ClientName}}",
    $ClientName
)

$body = $body.Replace(
    "{{SkillsetName}}",
    $SkillsetName
)

$body = $body.Replace(
    "{{FoundryApiKey}}",
    $foundryApiKey
)

$body = $body.Replace(
    "{{IndexName}}",
    $SearchIndexName
)

# Déploiement

$uri = "https://$SearchServiceName.search.windows.net/skillsets/$SkillsetName"
$uri+= "?api-version=2024-07-01"

Write-Host "PUT $uri"

Invoke-RestMethod `
    -Uri $uri `
    -Method PUT `
    -Headers @{
        "api-key"      = $adminKey
        "Content-Type" = "application/json"
    } `
    -Body $body

Write-Host "Skillset déployé avec succès : $SkillsetName"