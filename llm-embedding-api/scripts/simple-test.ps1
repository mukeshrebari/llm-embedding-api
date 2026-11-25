#!/usr/bin/env pwsh

Write-Host "=== Simple Deployment Script Repair Validation ===" -ForegroundColor Cyan
Write-Host ""

# Load all content first
$deployScriptContent = Get-Content -Path "$PSScriptRoot\..\infra\Deploy-ACI.ps1" -Raw
$dockerfileContent = Get-Content -Path "$PSScriptRoot\..\Dockerfile" -Raw
$composeContent = Get-Content -Path "$PSScriptRoot\..\docker-compose.yml" -Raw

# Test 1: Check for logging initialization
Write-Host "Test 1: Checking for logging initialization..." -ForegroundColor Yellow
if ($deployScriptContent.Contains("Start-Transcript")) {
    Write-Host "✓ Logging initialization found" -ForegroundColor Green
} else {
    Write-Host "✗ Logging initialization missing" -ForegroundColor Red
    exit 1
}

# Test 2: Check for Dockerfile environment variable consistency
Write-Host "Test 2: Checking Dockerfile environment variables..." -ForegroundColor Yellow
$dockerfileContent = Get-Content -Path "$PSScriptRoot\..\Dockerfile" -Raw
if ($dockerfileContent.Contains("ENV ASPNETCORE_ENVIRONMENT=Production") -and
    $dockerfileContent.Contains("ENV Embedding__AutoDownload=true")) {
    Write-Host "✓ Dockerfile environment variables consistent" -ForegroundColor Green
} else {
    Write-Host "✗ Dockerfile environment variables inconsistent" -ForegroundColor Red
    exit 1
}

# Test 3: Check for model download script
Write-Host "Test 3: Checking for model download script..." -ForegroundColor Yellow
$modelScriptPath = "$PSScriptRoot\download-model.sh"
if (Test-Path $modelScriptPath) {
    Write-Host "✓ Model download script exists" -ForegroundColor Green
} else {
    Write-Host "✗ Model download script missing" -ForegroundColor Red
    exit 1
}

# Test 4: Check for security improvements
Write-Host "Test 4: Checking for security improvements..." -ForegroundColor Yellow
$composeContent = Get-Content -Path "$PSScriptRoot\..\docker-compose.yml" -Raw
if ($composeContent.Contains("security_opt") -and $composeContent.Contains("cap_drop")) {
    Write-Host "✓ Security improvements found" -ForegroundColor Green
} else {
    Write-Host "✗ Security improvements missing" -ForegroundColor Red
    exit 1
}

# Test 5: Check for Hugging Face token handling
Write-Host "Test 5: Checking for Hugging Face token handling..." -ForegroundColor Yellow
if ($deployScriptContent.Contains("HUGGINGFACE_TOKEN") -and $dockerfileContent.Contains("ARG HUGGINGFACE_TOKEN")) {
    Write-Host "✓ Hugging Face token handling implemented" -ForegroundColor Green
} else {
    Write-Host "✗ Hugging Face token handling missing" -ForegroundColor Red
    exit 1
}

# Test 6: Check for detailed deployment logging
Write-Host "Test 6: Checking for detailed deployment logging..." -ForegroundColor Yellow
if ($deployScriptContent.Contains("What-if duration") -and
    $deployScriptContent.Contains("Deployment duration") -and
    $deployScriptContent.Contains("Build duration")) {
    Write-Host "✓ Detailed deployment logging found" -ForegroundColor Green
} else {
    Write-Host "✗ Detailed deployment logging missing" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== All Tests Passed ===" -ForegroundColor Green
Write-Host "Deployment scripts have been successfully repaired!" -ForegroundColor White