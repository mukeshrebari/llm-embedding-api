# Quick Test Script for Performance Monitoring
# This script verifies that all performance monitoring endpoints are working

param(
    [string]$BaseUrl = "http://localhost:5299"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Performance Monitoring Test Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$allTestsPassed = $true

# Test 1: Health Check
Write-Host "Test 1: Health Check..." -NoNewline
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5
    Write-Host " ? PASSED" -ForegroundColor Green
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 2: Current Metrics Endpoint
Write-Host "Test 2: Get Current Metrics..." -NoNewline
try {
    $currentMetrics = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/current" -Method Get -TimeoutSec 5
    
    # Verify required fields
    $requiredFields = @('timestamp', 'cpuUsagePercent', 'workingSetMB', 'privateMemoryMB', 'gcHeapSizeMB')
    $missingFields = @()
    
    foreach ($field in $requiredFields) {
     if ($null -eq $currentMetrics.$field) {
            $missingFields += $field
     }
    }
    
 if ($missingFields.Count -eq 0) {
   Write-Host " ? PASSED" -ForegroundColor Green
        Write-Host "  CPU: $([math]::Round($currentMetrics.cpuUsagePercent, 2))%, Memory: $([math]::Round($currentMetrics.workingSetMB, 2))MB" -ForegroundColor Gray
    } else {
    Write-Host " ? FAILED" -ForegroundColor Red
        Write-Host "  Missing fields: $($missingFields -join ', ')" -ForegroundColor Red
     $allTestsPassed = $false
    }
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
 $allTestsPassed = $false
}

# Test 3: Statistics Endpoint
Write-Host "Test 3: Get Performance Statistics..." -NoNewline
try {
    $stats = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/statistics" -Method Get -TimeoutSec 5
  
    # Verify Azure recommendations exist
    if ($null -ne $stats.azureRecommendations -and 
     $null -ne $stats.azureRecommendations.minimumCpuCores -and 
        $null -ne $stats.azureRecommendations.minimumMemoryGB) {
        Write-Host " ? PASSED" -ForegroundColor Green
        Write-Host "    Azure Recommendations: $($stats.azureRecommendations.minimumCpuCores) vCPU, $($stats.azureRecommendations.minimumMemoryGB)GB RAM" -ForegroundColor Gray
    } else {
 Write-Host " ? FAILED" -ForegroundColor Red
        Write-Host "  Missing Azure recommendations" -ForegroundColor Red
   $allTestsPassed = $false
    }
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
  $allTestsPassed = $false
}

# Test 4: Snapshots Endpoint
Write-Host "Test 4: Get Performance Snapshots..." -NoNewline
try {
    $snapshots = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/snapshots?limit=5" -Method Get -TimeoutSec 5
    
    if ($null -ne $snapshots.count -and $snapshots.count -ge 0) {
        Write-Host " ? PASSED" -ForegroundColor Green
        Write-Host "    Snapshots collected: $($snapshots.count)" -ForegroundColor Gray
    } else {
 Write-Host " ? FAILED" -ForegroundColor Red
        Write-Host "  Invalid response format" -ForegroundColor Red
        $allTestsPassed = $false
    }
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    $allTestsPassed = $false
}

# Test 5: Test Embedding Endpoint (optional, requires model)
Write-Host "Test 5: Test Embedding Generation..." -NoNewline
try {
    $body = @{ text = "Performance monitoring test" } | ConvertTo-Json
    $embedResponse = Invoke-RestMethod -Uri "$BaseUrl/api/embed" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
    
    if ($null -ne $embedResponse.embeddings -and $embedResponse.embeddings.Count -gt 0) {
        Write-Host " ? PASSED" -ForegroundColor Green
     Write-Host "    Embedding dimensions: $($embedResponse.embeddings.Count)" -ForegroundColor Gray
    } else {
     Write-Host " ? FAILED" -ForegroundColor Red
        Write-Host "  No embeddings returned" -ForegroundColor Red
     $allTestsPassed = $false
    }
} catch {
    Write-Host " ? SKIPPED" -ForegroundColor Yellow
    Write-Host "  Note: Model may not be loaded (this is OK for testing monitoring)" -ForegroundColor Yellow
}

# Test 6: GC Endpoint
Write-Host "Test 6: Force Garbage Collection..." -NoNewline
try {
    $gcResult = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/gc" -Method Post -TimeoutSec 10
    
    if ($null -ne $gcResult.message -and $null -ne $gcResult.freedMB) {
    Write-Host " ? PASSED" -ForegroundColor Green
        Write-Host "    Memory freed: $([math]::Round($gcResult.freedMB, 2))MB" -ForegroundColor Gray
    } else {
        Write-Host " ? FAILED" -ForegroundColor Red
        Write-Host "  Invalid response format" -ForegroundColor Red
        $allTestsPassed = $false
    }
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
  $allTestsPassed = $false
}

# Test 7: Verify Metrics After Some Activity
Write-Host "Test 7: Verify Metrics Collection..." -NoNewline
Start-Sleep -Seconds 2
try {
    $metrics1 = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/current" -Method Get
    Start-Sleep -Seconds 3
    $metrics2 = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/current" -Method Get
    
    # Check if metrics are being collected (timestamp should change)
    if ($metrics1.timestamp -ne $metrics2.timestamp) {
      Write-Host " ? PASSED" -ForegroundColor Green
        Write-Host "    Metrics are being collected continuously" -ForegroundColor Gray
    } else {
        Write-Host " ? WARNING" -ForegroundColor Yellow
        Write-Host "  Timestamps are identical (may be caching issue)" -ForegroundColor Yellow
    }
} catch {
    Write-Host " ? FAILED" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    $allTestsPassed = $false
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan

if ($allTestsPassed) {
    Write-Host "All Tests Passed! ?" -ForegroundColor Green
    Write-Host ""
  Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Run load test: .\scripts\Run-LoadTest.ps1" -ForegroundColor White
    Write-Host "  2. Watch metrics: .\scripts\Watch-Performance.ps1" -ForegroundColor White
    Write-Host "  3. Get recommendations: Invoke-RestMethod $BaseUrl/api/metrics/statistics" -ForegroundColor White
} else {
    Write-Host "Some Tests Failed ?" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Cyan
    Write-Host "  - Ensure API is running: dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj" -ForegroundColor White
    Write-Host "  - Check logs in the API terminal" -ForegroundColor White
    Write-Host "  - Verify the API is accessible at $BaseUrl" -ForegroundColor White
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
