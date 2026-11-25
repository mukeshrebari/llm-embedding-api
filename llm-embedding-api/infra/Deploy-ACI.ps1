#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy LLM Embedding API to Azure Container Instances

.DESCRIPTION
    This script deploys the LLM Embedding API to Azure Container Instances with
    2 CPU cores and 4GB RAM, optimized for production workloads.

.PARAMETER ResourceGroupName
    Name of the Azure resource group (will be created if it doesn't exist)

.PARAMETER Location
    Azure region for deployment (default: eastus)

.PARAMETER ContainerRegistry
    Azure Container Registry name (e.g., myregistry)

.PARAMETER ContainerRegistryResourceGroup
    Resource group containing the Azure Container Registry (defaults to deployment resource group)

.PARAMETER ImageTag
    Docker image tag (default: latest)

.PARAMETER UseManagedIdentity
    Use a user-assigned managed identity for pulling images from ACR (preferred over admin credentials)

.PARAMETER UserAssignedIdentityName
    Name of the user-assigned managed identity (default: acr-pull-identity)

.PARAMETER UserAssignedIdentityResourceGroup
    Resource group where the user-assigned identity lives (defaults to deployment resource group when omitted)

.PARAMETER UserAssignedIdentityId
    Resource ID of the user-assigned managed identity (optional when name is provided)

.PARAMETER UserAssignedIdentityClientId
    Client ID of the user-assigned managed identity (required when using identity)

.PARAMETER HuggingFaceToken
    Optional Hugging Face API token for model downloads

.PARAMETER VnetResourceId
    Optional VNet resource ID for private networking (e.g., /subscriptions/.../virtualNetworks/my-vnet)

.PARAMETER SubnetName
    Name of the subnet for ACI (default: aci-subnet). Subnet must be delegated to Microsoft.ContainerInstance/containerGroups

.PARAMETER EnablePublicIp
    Enable public IP address when using VNet (default: true). Set to false for private-only access

.PARAMETER DeployVnet
    Deploy a new VNet infrastructure using vnet.bicep template

.PARAMETER SkipBuild
    Skip Docker image build and push steps

.PARAMETER WhatIf
    Preview the deployment without making changes

.EXAMPLE
    .\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry"
    
    Deploy with public IP only (no VNet)

.EXAMPLE
    .\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" -DeployVnet
    
    Deploy VNet infrastructure first, then deploy container with dual networking (public + private)

.EXAMPLE
    .\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" -VnetResourceId "/subscriptions/.../virtualNetworks/my-vnet" -EnablePublicIp $false
    
    Deploy with existing VNet in private-only mode (no public IP)

.EXAMPLE
    .\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" -HuggingFaceToken "hf_xxx" -WhatIf
    
    Preview deployment without making changes

.EXAMPLE
    .\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" -UseManagedIdentity -UserAssignedIdentityName "acr-pull-identity"
    
    Deploy using a user-assigned managed identity for ACR pulls (public networking)
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $false)]
    [string]$Location = "eastus",

    [Parameter(Mandatory = $true)]
    [string]$ContainerRegistry,

    [Parameter(Mandatory = $false)]
    [string]$ContainerRegistryResourceGroup = "",

    [Parameter(Mandatory = $false)]
    [string]$ImageTag = "latest",

    [Parameter(Mandatory = $false)]
    [string]$ContainerRepository = "llm-embedding-api",

    [Parameter(Mandatory = $false)]
    [switch]$UseAcrBuild,

    [Parameter(Mandatory = $false)]
    [switch]$UseManagedIdentity,

    [Parameter(Mandatory = $false)]
    [string]$UserAssignedIdentityId = "",

    [Parameter(Mandatory = $false)]
    [string]$UserAssignedIdentityClientId = "",

    [Parameter(Mandatory = $false)]
    [string]$UserAssignedIdentityName = "acr-pull-identity",

    [Parameter(Mandatory = $false)]
    [string]$UserAssignedIdentityResourceGroup = "",

    [Parameter(Mandatory = $false)]
    [string]$HuggingFaceToken = "",

    [Parameter(Mandatory = $false)]
    [string]$StorageAccountName = "",

    [Parameter(Mandatory = $false)]
    [string]$StorageAccountKey = "",

    [Parameter(Mandatory = $false)]
    [string]$FileShareName = "llm-models",

    [Parameter(Mandatory = $false)]
    [string]$ApiKeyHash = "",

    [Parameter(Mandatory = $false)]
    [string]$ApiKeyName = "aci-deployment",

    [Parameter(Mandatory = $false)]
    [bool]$AuthRemoteValidationEnabled = $false,

    [Parameter(Mandatory = $false)]
    [string]$AuthRemoteValidationEndpoint = "",

    [Parameter(Mandatory = $false)]
    [string]$VnetResourceId = "",

    [Parameter(Mandatory = $false)]
    [string]$SubnetName = "aci-subnet",

    [Parameter(Mandatory = $false)]
    [bool]$EnablePublicIp = $true,

    [Parameter(Mandatory = $false)]
    [switch]$DeployVnet,

    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild,

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

# Initialize logging
$logFile = Join-Path $ScriptDir "deploy-$(Get-Date -Format 'yyyyMMdd-HHmmss').log"
Start-Transcript -Path $logFile -Append
Write-Host "Logging to: $logFile" -ForegroundColor Gray

Write-Host "=== LLM Embedding API - Azure Container Instance Deployment ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Script Directory: $ScriptDir" -ForegroundColor Gray
Write-Host "Repo Root: $RepoRoot" -ForegroundColor Gray
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

$azVersion = az version --output json 2>&1 | ConvertFrom-Json
if (-not $azVersion) {
    Write-Error "Azure CLI is not installed. Please install from https://aka.ms/azure-cli"
    exit 1
}
Write-Host "[OK] Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
Write-Host "Azure CLI path: $(Get-Command az).Source" -ForegroundColor Gray

if (-not $SkipBuild) {
    $dockerVersion = docker --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker is not installed. Please install Docker Desktop"
        exit 1
    }
    Write-Host "[OK] Docker: $dockerVersion" -ForegroundColor Green
    Write-Host "Docker path: $(Get-Command docker).Source" -ForegroundColor Gray
    # Query docker daemon status in a PowerShell-safe way (avoid Bash '||' syntax)
    $dockerDaemonStatus = 'Unknown'
    try {
        $dockerInfo = & docker info --format '{{.ServerErrors}}' 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dockerInfo)) {
            $dockerDaemonStatus = 'Running'
        }
        else {
            $dockerDaemonStatus = $dockerInfo
        }
    }
    catch {
        $dockerDaemonStatus = 'Running'
    }
    Write-Host "Docker daemon status: $dockerDaemonStatus" -ForegroundColor Gray
}

# Check if logged in to Azure
$account = az account show --output json 2>&1 | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure. Running 'az login'..." -ForegroundColor Yellow
    az login
    $account = az account show --output json | ConvertFrom-Json
}
Write-Host "[OK] Azure Subscription: $($account.name)" -ForegroundColor Green
Write-Host ""

# Create resource group if it doesn't exist
Write-Host "Ensuring resource group exists..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -eq "false") {
    Write-Host "Creating resource group: $ResourceGroupName in $Location" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --output none
    Write-Host "[OK] Resource group created" -ForegroundColor Green
} else {
    Write-Host "[OK] Resource group exists" -ForegroundColor Green
}
Write-Host ""

$ContainerRegistryResourceGroup = if ($ContainerRegistryResourceGroup) { $ContainerRegistryResourceGroup } else { $ResourceGroupName }

$useManagedIdentity = $UseManagedIdentity.IsPresent -or -not [string]::IsNullOrWhiteSpace($UserAssignedIdentityId) -or -not [string]::IsNullOrWhiteSpace($UserAssignedIdentityClientId)

if ($useManagedIdentity) {
    if (-not $UserAssignedIdentityResourceGroup) {
        $UserAssignedIdentityResourceGroup = $ResourceGroupName
    }

    if (-not $UserAssignedIdentityId -and $UserAssignedIdentityName) {
        Write-Host "Resolving managed identity '$UserAssignedIdentityName' in resource group '$UserAssignedIdentityResourceGroup'..." -ForegroundColor Yellow
        $identity = az identity show `
            --name $UserAssignedIdentityName `
            --resource-group $UserAssignedIdentityResourceGroup `
            --output json | ConvertFrom-Json

        if ($LASTEXITCODE -ne 0 -or -not $identity) {
            Write-Error "Managed identity '$UserAssignedIdentityName' not found in resource group '$UserAssignedIdentityResourceGroup'"
            exit 1
        }

        $UserAssignedIdentityId = $identity.id
        if (-not $UserAssignedIdentityClientId) {
            $UserAssignedIdentityClientId = $identity.clientId
        }
    }

    if (-not $UserAssignedIdentityClientId) {
        Write-Error "UserAssignedIdentityClientId is required when using a managed identity for ACR pulls."
        exit 1
    }

    Write-Host "[OK] Using managed identity for ACR pull: $UserAssignedIdentityId" -ForegroundColor Green
    Write-Host "  Client ID: $UserAssignedIdentityClientId" -ForegroundColor Gray
    Write-Host ""

    # Ensure the managed identity has AcrPull permission on the ACR
    try {
        $acrShowCmd = @(
            'acr', 'show',
            '--name', $ContainerRegistry
        )

        if ($ContainerRegistryResourceGroup) {
            $acrShowCmd += @('--resource-group', $ContainerRegistryResourceGroup)
        }

        $acrShowCmd += @('--query', 'id', '--output', 'tsv')
        $acrResourceId = (az @acrShowCmd) -as [string]
        if ($acrResourceId) {
            $principalId = $identity.principalId
            Write-Host "Assigning 'AcrPull' role to identity principal $principalId on registry $ContainerRegistry (resource group: $ContainerRegistryResourceGroup)..." -ForegroundColor Yellow
            az role assignment create --assignee-object-id $principalId --role "AcrPull" --scope $acrResourceId --output none 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Assigned AcrPull to managed identity" -ForegroundColor Green
            }
        }
    }
    catch {
        Write-Warning "Could not automatically assign AcrPull role to managed identity. Ensure the identity has 'AcrPull' permission on the registry."    
    }
}

# Deploy VNet if requested
if ($DeployVnet) {
    Write-Host "Deploying Virtual Network infrastructure..." -ForegroundColor Yellow
    
    $vnetDeploymentName = "vnet-deployment-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $vnetResult = az deployment group create `
        --resource-group $ResourceGroupName `
        --name $vnetDeploymentName `
        --template-file "$ScriptDir/vnet.bicep" `
        --parameters "$ScriptDir/vnet.parameters.json" `
        --parameters location=$Location `
        --output json | ConvertFrom-Json
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "VNet deployment failed"
        exit 1
    }
    
    # Get VNet resource ID from deployment outputs
    $VnetResourceId = $vnetResult.properties.outputs.vnetResourceId.value
    $SubnetName = $vnetResult.properties.outputs.subnetName.value
    
    Write-Host "✓ VNet deployed successfully" -ForegroundColor Green
    Write-Host "  VNet Resource ID: $VnetResourceId" -ForegroundColor Gray
    Write-Host "  Subnet Name: $SubnetName" -ForegroundColor Gray
    Write-Host ""
}

# Build and push Docker image (unless skipped)
$imageName = "${ContainerRegistry}.azurecr.io/${ContainerRepository}:${ImageTag}"

# Short image name used for ACR build API (registry will be prefixed automatically)
$acrBuildImage = "${ContainerRepository}:${ImageTag}"

Write-Host "Image configuration:" -ForegroundColor Cyan
Write-Host "  Container Registry: $ContainerRegistry" -ForegroundColor Gray
Write-Host "  Repository: $ContainerRepository" -ForegroundColor Gray
Write-Host "  Image Tag: $ImageTag" -ForegroundColor Gray
Write-Host "  Full Image Name: $imageName" -ForegroundColor Gray
Write-Host "  Skip Build: $SkipBuild" -ForegroundColor Gray
Write-Host "  Use ACR Build: $($UseAcrBuild.IsPresent)" -ForegroundColor Gray
Write-Host ""

if (-not $SkipBuild) {
    if ($UseAcrBuild.IsPresent) {
        Write-Host "Building image in ACR using 'az acr build'..." -ForegroundColor Yellow
        # Use ACR's cloud build so no local Docker daemon is required
        Set-Location $RepoRoot
        Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray

        $acrBuildCmd = @(
            'acr', 'build',
            '--registry', $ContainerRegistry,
            '--image', $acrBuildImage,
            '--file', 'Dockerfile',
            '--build-arg', "HUGGINGFACE_TOKEN=$HuggingFaceToken",
            '.'
        )

        Write-Host "Running: az $($acrBuildCmd -join ' ')" -ForegroundColor Gray
        $acrBuildStart = Get-Date
        az @acrBuildCmd
        $acrBuildEnd = Get-Date
        if ($LASTEXITCODE -ne 0) {
            Write-Error "ACR cloud build failed (exit code: $LASTEXITCODE)"
            Write-Host "Build duration: $($acrBuildEnd - $acrBuildStart)" -ForegroundColor Gray
            exit 1
        }
        Write-Host "Build duration: $($acrBuildEnd - $acrBuildStart)" -ForegroundColor Gray

        Write-Host "✓ ACR cloud build completed" -ForegroundColor Green
        # Full image name in registry
        $imageName = "${ContainerRegistry}.azurecr.io/$acrBuildImage"
        Write-Host "Built image: $imageName" -ForegroundColor Green
    }
    else {
        Write-Host "Building and pushing Docker image locally..." -ForegroundColor Yellow

        # Login to ACR
        Write-Host "Logging in to Azure Container Registry..." -ForegroundColor Yellow
        $acrLoginStart = Get-Date
        az acr login --name $ContainerRegistry
        $acrLoginEnd = Get-Date
        if ($LASTEXITCODE -ne 0) {
            Write-Error "ACR login failed (exit code: $LASTEXITCODE)"
            exit 1
        }
        Write-Host "ACR login duration: $($acrLoginEnd - $acrLoginStart)" -ForegroundColor Gray

        # Build image locally
        Write-Host "Building Docker image: $imageName" -ForegroundColor Yellow
        Set-Location $RepoRoot
        Write-Host "Working directory: $(Get-Location)" -ForegroundColor Gray

        $dockerBuildStart = Get-Date
        if ($HuggingFaceToken) {
            docker build -t $imageName -f Dockerfile --build-arg HUGGINGFACE_TOKEN=$HuggingFaceToken .
        } else {
            docker build -t $imageName -f Dockerfile .
        }
        $dockerBuildEnd = Get-Date

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Docker build failed (exit code: $LASTEXITCODE)"
            Write-Host "Build duration: $($dockerBuildEnd - $dockerBuildStart)" -ForegroundColor Gray
            exit 1
        }
        Write-Host "Build duration: $($dockerBuildEnd - $dockerBuildStart)" -ForegroundColor Gray
        Write-Host "✓ Docker image built" -ForegroundColor Green

        # Push image
        Write-Host "Pushing image to registry..." -ForegroundColor Yellow
        $dockerPushStart = Get-Date
        docker push $imageName
        $dockerPushEnd = Get-Date

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Docker push failed (exit code: $LASTEXITCODE)"
            Write-Host "Push duration: $($dockerPushEnd - $dockerPushStart)" -ForegroundColor Gray
            exit 1
        }
        Write-Host "Push duration: $($dockerPushEnd - $dockerPushStart)" -ForegroundColor Gray
        Write-Host "✓ Image pushed to registry" -ForegroundColor Green
        Write-Host ""
    }
}

# Get ACR credentials when not using managed identity
if (-not $useManagedIdentity) {
    Write-Host "Retrieving container registry credentials..." -ForegroundColor Yellow
    $acrUsername = az acr credential show --name $ContainerRegistry --query username --output tsv
    $acrPassword = az acr credential show --name $ContainerRegistry --query "passwords[0].value" --output tsv

    if (-not $acrUsername -or -not $acrPassword) {
        Write-Error "Failed to retrieve ACR credentials. Enable the admin user or use a managed identity."
        exit 1
    }
    Write-Host "✓ Registry credentials retrieved" -ForegroundColor Green
    Write-Host ""
} else {
    $acrUsername = ""
    $acrPassword = ""
}

# Prepare deployment parameters
$deploymentName = "llm-embedding-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$parametersFile = Join-Path $ScriptDir "main.parameters.json"

# Create temporary parameters file with actual values
$params = Get-Content $parametersFile | ConvertFrom-Json
$params.parameters.containerImage.value = $imageName
$params.parameters.registryServer.value = "${ContainerRegistry}.azurecr.io"
$params.parameters.registryUsername.value = $acrUsername
$params.parameters.registryPassword.value = $acrPassword
$params.parameters.location.value = $Location
$params.parameters.dnsNameLabel.value = "llm-embed-$(Get-Random -Minimum 1000 -Maximum 9999)"

if ($useManagedIdentity) {
    $params.parameters.userAssignedIdentityResourceId.value = $UserAssignedIdentityId
    $params.parameters.userAssignedIdentityClientId.value = $UserAssignedIdentityClientId
} else {
    $params.parameters.userAssignedIdentityResourceId.value = ""
    $params.parameters.userAssignedIdentityClientId.value = ""
}

if ($HuggingFaceToken) {
    $params.parameters.huggingFaceToken.value = $HuggingFaceToken
}

# Allow passing the storage account key via environment variable to avoid shell quoting issues
if (-not $StorageAccountKey -and $env:ACI_STORAGE_ACCOUNT_KEY) {
    Write-Host "Using storage account key from environment variable ACI_STORAGE_ACCOUNT_KEY" -ForegroundColor Yellow
    $StorageAccountKey = $env:ACI_STORAGE_ACCOUNT_KEY
}

# Pass Azure File mount parameters to Bicep
if ($StorageAccountName) {
    $params.parameters.storageAccountName.value = $StorageAccountName
}

if ($StorageAccountKey) {
    $params.parameters.storageAccountKey.value = $StorageAccountKey
}

if ($FileShareName) {
    $params.parameters.fileShareName.value = $FileShareName
}

# Allow passing the API key hash via environment variable to avoid shell quoting issues.
# NEVER pass the plaintext API key here — compute the SHA-256 hash first (tools/hash-key).
if (-not $ApiKeyHash -and $env:ACI_API_KEY_HASH) {
    Write-Host "Using API key hash from environment variable ACI_API_KEY_HASH" -ForegroundColor Yellow
    $ApiKeyHash = $env:ACI_API_KEY_HASH
}

if ($ApiKeyHash) {
    $params.parameters.apiKeyHash.value = $ApiKeyHash
    $params.parameters.apiKeyName.value = $ApiKeyName
} else {
    Write-Host "WARNING: No API key hash provided (-ApiKeyHash or ACI_API_KEY_HASH). All /api/* requests will be rejected unless AuthRemoteValidationEnabled is set." -ForegroundColor Yellow
}

$params.parameters.authRemoteValidationEnabled.value = $AuthRemoteValidationEnabled
if ($AuthRemoteValidationEndpoint) {
    $params.parameters.authRemoteValidationEndpoint.value = $AuthRemoteValidationEndpoint
}

# Add VNet parameters if provided
if ($VnetResourceId) {
    $params.parameters.vnetResourceId.value = $VnetResourceId
    $params.parameters.subnetName.value = $SubnetName
    $params.parameters.enablePublicIp.value = $EnablePublicIp
    
    $networkMode = if ($EnablePublicIp) { "Dual (Private VNet + Public IP)" } else { "Private (VNet only)" }
    Write-Host "Network Mode: $networkMode" -ForegroundColor Cyan
    Write-Host "  VNet Resource ID: $VnetResourceId" -ForegroundColor Gray
    Write-Host "  Subnet Name: $SubnetName" -ForegroundColor Gray
    Write-Host ""
}

$tempParamsFile = Join-Path $env:TEMP "aci-params-$(Get-Date -Format 'yyyyMMddHHmmss').json"
$params | ConvertTo-Json -Depth 10 | Set-Content $tempParamsFile

Write-Host "Deployment parameters saved to: $tempParamsFile" -ForegroundColor Gray
Write-Host "Parameter file content:" -ForegroundColor Gray
Get-Content $tempParamsFile | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
Write-Host ""

# Preview deployment with what-if
Write-Host "Running deployment preview (what-if)..." -ForegroundColor Yellow
$whatIfStart = Get-Date
az deployment group what-if `
    --name $deploymentName `
    --resource-group $ResourceGroupName `
    --template-file (Join-Path $ScriptDir "main.bicep") `
    --parameters "@$tempParamsFile"
$whatIfEnd = Get-Date
Write-Host "What-if duration: $($whatIfEnd - $whatIfStart)" -ForegroundColor Gray

Write-Host ""

if ($WhatIf) {
    Write-Host "WhatIf mode - deployment not executed" -ForegroundColor Yellow
    if (Test-Path $tempParamsFile) { Remove-Item $tempParamsFile -Force }
    exit 0
}

# Confirm deployment
$confirm = Read-Host "Proceed with deployment? (Y/N)"
if ($confirm -ne "Y" -and $confirm -ne "y") {
    Write-Host "Deployment cancelled" -ForegroundColor Yellow
    if (Test-Path $tempParamsFile) { Remove-Item $tempParamsFile -Force }
    exit 0
}

# Execute deployment
Write-Host "Deploying to Azure Container Instances..." -ForegroundColor Yellow
Write-Host "Deployment name: $deploymentName" -ForegroundColor Cyan
Write-Host "Template file: $(Join-Path $ScriptDir "main.bicep")" -ForegroundColor Gray
Write-Host "Parameters file: $tempParamsFile" -ForegroundColor Gray

$deploymentStart = Get-Date
$deployment = az deployment group create `
    --name $deploymentName `
    --resource-group $ResourceGroupName `
    --template-file (Join-Path $ScriptDir "main.bicep") `
    --parameters "@$tempParamsFile" `
    --output json | ConvertFrom-Json
$deploymentEnd = Get-Date

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed (exit code: $LASTEXITCODE)"
    Write-Host "Deployment duration: $($deploymentEnd - $deploymentStart)" -ForegroundColor Gray
    if (Test-Path $tempParamsFile) { Remove-Item $tempParamsFile -Force }
    exit 1
}
Write-Host "Deployment duration: $($deploymentEnd - $deploymentStart)" -ForegroundColor Gray

Write-Host "? Deployment completed successfully" -ForegroundColor Green
Write-Host ""

# Display outputs
Write-Host "=== Deployment Outputs ===" -ForegroundColor Cyan
$outputs = $deployment.properties.outputs

$fqdn = $outputs.fqdn.value
$ipAddress = $outputs.ipAddress.value
$apiEndpoint = $outputs.apiEndpoint.value
$healthEndpoint = $outputs.healthEndpoint.value
$networkMode = $outputs.networkMode.value

Write-Host "Network Mode:    $networkMode" -ForegroundColor White
Write-Host "FQDN:            $fqdn" -ForegroundColor White
Write-Host "IP Address:      $ipAddress" -ForegroundColor White

# Show private IP if available
if ($outputs.PSObject.Properties.Name -contains 'privateIpAddress') {
    $privateIpAddress = $outputs.privateIpAddress.value
    Write-Host "Private IP:      $privateIpAddress" -ForegroundColor White
}

Write-Host "API Endpoint:    $apiEndpoint" -ForegroundColor White
Write-Host "Health Endpoint: $healthEndpoint" -ForegroundColor White
Write-Host ""

# Test health endpoint
Write-Host "Testing health endpoint..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

try {
    $healthResponse = Invoke-RestMethod -Uri $healthEndpoint -TimeoutSec 30
    Write-Host "? Health check passed: $healthResponse" -ForegroundColor Green
}
catch {
    Write-Warning "Health check failed (container may still be starting): $_"
}

Write-Host ""
Write-Host "=== Quick Test ===" -ForegroundColor Cyan
Write-Host "Test the embedding endpoint with:" -ForegroundColor White
$quickTest = @"
`$body = @{ text = "Hello, world!" } | ConvertTo-Json
Invoke-RestMethod -Uri "$apiEndpoint/api/embed" -Method Post -Body `$body -ContentType "application/json"
"@
Write-Host $quickTest -ForegroundColor Gray

Write-Host ""
Write-Host "=== Azure Portal Link ===" -ForegroundColor Cyan
$portalUrl = "https://portal.azure.com/#@/resource/subscriptions/$($account.id)/resourceGroups/$ResourceGroupName/overview"
Write-Host $portalUrl -ForegroundColor White

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green

# Clean up temp file
if (Test-Path $tempParamsFile) {
    Remove-Item $tempParamsFile -Force
}
