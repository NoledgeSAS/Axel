param(
    [Parameter(Mandatory = $true)]
    [string]$AgentName,

    [Parameter(Mandatory = $true)]
    [string]$FoundryName,

    [Parameter(Mandatory = $true)]
    [string]$FoundryProjectName,

    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$ModelDeploymentName,

    [Parameter(Mandatory = $true)]
    [string]$AgentInstructions
)

$ErrorActionPreference = "Stop"


Write-Host "Déploiement de l'agent Azure AI Foundry : $AgentName"

# Chemin du template
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$templatePath = Join-Path $scriptPath "../templates/agent.template.json"

if (-not (Test-Path $templatePath)) {
    throw "Template introuvable : $templatePath"
}

# Récupération de la clé Foundry
$keys = az cognitiveservices account keys list `
    --name $FoundryName `
    --resource-group $ResourceGroupName `
    | ConvertFrom-Json

$foundryApiKey = $keys.key1

if (-not $foundryApiKey) {
    throw "Impossible de récupérer la clé Foundry."
}

# Chargement du template
$body = Get-Content $templatePath -Raw

# Remplacement des placeholders
$body = $body.Replace("{{AgentName}}", $AgentName)
$body = $body.Replace("{{ModelDeploymentName}}", $ModelDeploymentName)
$body = $body.Replace("{{AgentInstructions}}", $AgentInstructions)

# URI de l'API Foundry
$uri = "https://$FoundryName.services.ai.azure.com/api/projects/$FoundryProjectName/agents"
$uri += "?api-version=2025-05-15-preview"

Write-Host "POST $uri"

Invoke-RestMethod `
    -Uri $uri `
    -Method POST `
    -Headers @{
        "api-key"      = $foundryApiKey
        "Content-Type" = "application/json"
    } `
    -Body $body

Write-Host "Agent déployé avec succès : $agentName"