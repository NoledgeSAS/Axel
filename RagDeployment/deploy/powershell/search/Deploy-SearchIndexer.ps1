param(
    [Parameter(Mandatory = $true)]
    [string]$ClientName,

    [Parameter(Mandatory = $true)]
    [string]$SearchServiceName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$IndexerName,

    [Parameter(Mandatory = $true)]
    [string]$DataSourceName,

    [Parameter(Mandatory = $true)]
    [string]$SkillsetName,

    [Parameter(Mandatory = $true)]
    [string]$IndexName
)

$ErrorActionPreference = "Stop"

Write-Host "Déploiement de l'Indexer Azure AI Search : $IndexerName"

# Chemin du template

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$templatePath = Join-Path $scriptPath "../templates/indexer.template.json"

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

# Chargement du template

$body = Get-Content $templatePath -Raw

# Remplacement des placeholders

$body = $body.Replace(
    "{{ClientName}}",
    $ClientName
)
$body = $body.Replace(
    "{{IndexerName}}",
    $IndexerName
)
$body = $body.Replace(
    "{{DatasourceName}}",
    $DataSourceName
)
$body = $body.Replace(
    "{{SkillsetName}}",
    $SkillsetName
)
$body = $body.Replace(
    "{{IndexName}}",
    $IndexName
)

# Déploiement

$uri = "https://$SearchServiceName.search.windows.net/indexers/$IndexerName"
$uri += "?api-version=2024-07-01"

Write-Host "PUT $uri"

Invoke-RestMethod `
    -Uri $uri `
    -Method PUT `
    -Headers @{
        "api-key"      = $adminKey
        "Content-Type" = "application/json"
    } `
    -Body $body

Write-Host "Indexer déployé avec succès : $IndexerName"