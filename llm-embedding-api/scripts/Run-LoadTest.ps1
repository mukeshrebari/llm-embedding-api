# Performance Load Testing Script for LLM Embedding API
# This script runs load tests and collects performance metrics

param(
    [string]$BaseUrl = "http://localhost:5299",
    [int]$TotalRequests = 100,
    [int]$ConcurrentRequests = 5,
    [string]$TestText = "This is a sample text for testing embeddings. It should be representative of your typical workload.",
    [int]$WarmupRequests = 10
)

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "LLM Embedding API Load Testing Tool" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Check if API is accessible
Write-Host "Checking API availability..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get -TimeoutSec 5
    Write-Host "✓ API is healthy" -ForegroundColor Green
} catch {
  Write-Host "✗ API is not accessible at $BaseUrl" -ForegroundColor Red
    Write-Host "  Please ensure the API is running" -ForegroundColor Red
    exit 1
}

# Get initial metrics
Write-Host "`nCollecting initial metrics..." -ForegroundColor Yellow
try {
    $initialMetrics = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/current" -Method Get
  Write-Host "✓ Initial Memory: $([math]::Round($initialMetrics.workingSetMB, 2)) MB" -ForegroundColor Green
  Write-Host "✓ Initial CPU: $([math]::Round($initialMetrics.cpuUsagePercent, 2))%" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to collect initial metrics" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
}

# Warmup phase
Write-Host "`nWarming up ($WarmupRequests requests)..." -ForegroundColor Yellow
$warmupStart = Get-Date
for ($i = 1; $i -le $WarmupRequests; $i++) {
    try {
        $body = @{ text = $TestText } | ConvertTo-Json
   $response = Invoke-RestMethod -Uri "$BaseUrl/api/embed" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30
        Write-Progress -Activity "Warmup" -Status "$i of $WarmupRequests" -PercentComplete (($i / $WarmupRequests) * 100)
    } catch {
        Write-Host "  Warning: Warmup request $i failed" -ForegroundColor Yellow
    }
}
$warmupDuration = (Get-Date) - $warmupStart
Write-Host "✓ Warmup completed in $($warmupDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green

# Wait a moment for metrics to stabilize
Start-Sleep -Seconds 2

# Main load test
Write-Host "`nStarting load test..." -ForegroundColor Yellow
Write-Host "  Total Requests: $TotalRequests" -ForegroundColor Cyan
Write-Host "  Concurrent: $ConcurrentRequests" -ForegroundColor Cyan
Write-Host "  Test Text Length: $($TestText.Length) characters" -ForegroundColor Cyan
Write-Host ""

$testStart = Get-Date
$successCount = 0
$errorCount = 0
$durations = @()
$completed = 0

# Create job script block
$jobScript = {
    param($url, $text, $requestId)
  
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $body = @{ text = $text } | ConvertTo-Json
      $response = Invoke-RestMethod -Uri "$url/api/embed" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 60
 $stopwatch.Stop()
      
return @{
 Success = $true
       Duration = $stopwatch.Elapsed.TotalMilliseconds
            RequestId = $requestId
        EmbeddingLength = $response.embeddings.Length
        }
    } catch {
        $stopwatch.Stop()
  return @{
       Success = $false
     Duration = $stopwatch.Elapsed.TotalMilliseconds
            RequestId = $requestId
            Error = $_.Exception.Message
        }
    }
}

# Run load test with job management
$jobs = @()
$batchSize = $ConcurrentRequests

for ($i = 1; $i -le $TotalRequests; $i++) {
    # Start new job
    $job = Start-Job -ScriptBlock $jobScript -ArgumentList $BaseUrl, $TestText, $i
    $jobs += $job
    
    # Wait if we've reached max concurrent jobs
    if ($jobs.Count -ge $batchSize) {
        $completedJob = $jobs | Wait-Job -Any
        $result = Receive-Job -Job $completedJob
     Remove-Job -Job $completedJob
        $jobs = $jobs | Where-Object { $_.Id -ne $completedJob.Id }
  
        # Process result
    $completed++
        if ($result.Success) {
            $successCount++
            $durations += $result.Duration
        } else {
            $errorCount++
    }
   
      Write-Progress -Activity "Load Test" -Status "Completed: $completed of $TotalRequests (Success: $successCount, Errors: $errorCount)" -PercentComplete (($completed / $TotalRequests) * 100)
    }
}

# Wait for remaining jobs
while ($jobs.Count -gt 0) {
    $completedJob = $jobs | Wait-Job -Any
    $result = Receive-Job -Job $completedJob
    Remove-Job -Job $completedJob
    $jobs = $jobs | Where-Object { $_.Id -ne $completedJob.Id }
    
    # Process result
    $completed++
if ($result.Success) {
        $successCount++
 $durations += $result.Duration
    } else {
        $errorCount++
    }
    
    Write-Progress -Activity "Load Test" -Status "Completed: $completed of $TotalRequests (Success: $successCount, Errors: $errorCount)" -PercentComplete (($completed / $TotalRequests) * 100)
}

$testDuration = (Get-Date) - $testStart

Write-Host "`n✓ Load test completed in $($testDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
Write-Host ""

# Calculate statistics
$avgDuration = ($durations | Measure-Object -Average).Average
$minDuration = ($durations | Measure-Object -Minimum).Minimum
$maxDuration = ($durations | Measure-Object -Maximum).Maximum
$p50Duration = ($durations | Sort-Object)[[math]::Floor($durations.Count * 0.5)]
$p95Duration = ($durations | Sort-Object)[[math]::Floor($durations.Count * 0.95)]
$p99Duration = ($durations | Sort-Object)[[math]::Floor($durations.Count * 0.99)]
$throughput = $TotalRequests / $testDuration.TotalSeconds

# Display results
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Load Test Results" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Request Summary:" -ForegroundColor White
Write-Host "  Total Requests:    $TotalRequests"
Write-Host "  Successful:        $successCount" -ForegroundColor Green
Write-Host "  Failed:            $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
Write-Host "  Success Rate:      $([math]::Round(($successCount / $TotalRequests) * 100, 2))%"
Write-Host ""
Write-Host "Performance:" -ForegroundColor White
Write-Host "  Total Duration:    $($testDuration.TotalSeconds.ToString('F2')) seconds"
Write-Host "  Throughput: $([math]::Round($throughput, 2)) requests/second"
Write-Host ""
Write-Host "Response Times (ms):" -ForegroundColor White
Write-Host "  Average:           $([math]::Round($avgDuration, 2)) ms"
Write-Host "  Minimum:           $([math]::Round($minDuration, 2)) ms"
Write-Host "  Maximum:      $([math]::Round($maxDuration, 2)) ms"
Write-Host "  50th Percentile:   $([math]::Round($p50Duration, 2)) ms"
Write-Host "  95th Percentile:   $([math]::Round($p95Duration, 2)) ms"
Write-Host "  99th Percentile:   $([math]::Round($p99Duration, 2)) ms"
Write-Host ""

# Get final metrics and statistics
Write-Host "Collecting final performance metrics..." -ForegroundColor Yellow
Start-Sleep -Seconds 2  # Wait for metrics to update

try {
  $finalMetrics = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/current" -Method Get
    $statistics = Invoke-RestMethod -Uri "$BaseUrl/api/metrics/statistics" -Method Get
    
  Write-Host ""
    Write-Host "====================================" -ForegroundColor Cyan
    Write-Host "Performance Metrics" -ForegroundColor Cyan
    Write-Host "====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "CPU Usage:" -ForegroundColor White
    Write-Host "  Current:           $([math]::Round($finalMetrics.cpuUsagePercent, 2))%"
    Write-Host "  Peak:           $([math]::Round($statistics.cpuUsagePercent.peak, 2))%"
    Write-Host "  Average:    $([math]::Round($statistics.cpuUsagePercent.average, 2))%"
    Write-Host ""
    Write-Host "Memory Usage:" -ForegroundColor White
    Write-Host "  Current:           $([math]::Round($finalMetrics.workingSetMB, 2)) MB"
    Write-Host "  Peak:       $([math]::Round($statistics.workingSetMB.peak, 2)) MB"
    Write-Host "  Average:   $([math]::Round($statistics.workingSetMB.average, 2)) MB"
    Write-Host "  GC Heap:           $([math]::Round($finalMetrics.gcHeapSizeMB, 2)) MB"
    Write-Host ""
    Write-Host "Garbage Collection:" -ForegroundColor White
    Write-Host "  Gen 0:             $($finalMetrics.gen0Collections)"
    Write-Host "  Gen 1:             $($finalMetrics.gen1Collections)"
    Write-Host "  Gen 2:             $($finalMetrics.gen2Collections)"
    Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
    Write-Host "Azure Container Instance Recommendations" -ForegroundColor Cyan
 Write-Host "====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Based on Peak Usage:" -ForegroundColor White
  Write-Host "  Peak Memory:       $([math]::Round($statistics.azureRecommendations.peakMemoryMB, 2)) MB"
    Write-Host "  Peak CPU:          $([math]::Round($statistics.azureRecommendations.peakCpuPercent, 2))%"
    Write-Host ""
    Write-Host "Recommended Configuration:" -ForegroundColor Green
    Write-Host "  CPU Cores:    $($statistics.azureRecommendations.minimumCpuCores) vCPU" -ForegroundColor Green
  Write-Host "  Memory:            $($statistics.azureRecommendations.minimumMemoryGB) GB" -ForegroundColor Green
    Write-Host "  Buffers Applied:   CPU $($statistics.azureRecommendations.cpuBuffer), Memory $($statistics.azureRecommendations.memoryBuffer)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Recommendation: $($statistics.azureRecommendations.recommendation)" -ForegroundColor Cyan
    Write-Host ""
    
    # Export results to JSON
    $results = @{
        TestConfiguration = @{
 BaseUrl = $BaseUrl
         TotalRequests = $TotalRequests
            ConcurrentRequests = $ConcurrentRequests
            TestTextLength = $TestText.Length
      TestDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        }
LoadTestResults = @{
TotalRequests = $TotalRequests
   SuccessfulRequests = $successCount
FailedRequests = $errorCount
        SuccessRate = ($successCount / $TotalRequests) * 100
 TotalDurationSeconds = $testDuration.TotalSeconds
          ThroughputRequestsPerSecond = $throughput
    }
    ResponseTimes = @{
         AverageMs = [math]::Round($avgDuration, 2)
        MinimumMs = [math]::Round($minDuration, 2)
    MaximumMs = [math]::Round($maxDuration, 2)
            P50Ms = [math]::Round($p50Duration, 2)
  P95Ms = [math]::Round($p95Duration, 2)
      P99Ms = [math]::Round($p99Duration, 2)
   }
        PerformanceMetrics = @{
 CpuUsagePercent = @{
    Current = [math]::Round($finalMetrics.cpuUsagePercent, 2)
           Peak = [math]::Round($statistics.cpuUsagePercent.peak, 2)
                Average = [math]::Round($statistics.cpuUsagePercent.average, 2)
            }
  MemoryUsageMB = @{
            Current = [math]::Round($finalMetrics.workingSetMB, 2)
       Peak = [math]::Round($statistics.workingSetMB.peak, 2)
      Average = [math]::Round($statistics.workingSetMB.average, 2)
          }
}
  AzureRecommendations = $statistics.azureRecommendations
    }
 
    $resultsFile = "performance-test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
  $results | ConvertTo-Json -Depth 10 | Out-File -FilePath $resultsFile
    Write-Host "Results exported to: $resultsFile" -ForegroundColor Green
    
} catch {
    Write-Host "✗ Failed to collect final metrics" -ForegroundColor Red
Write-Host "  Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Test completed successfully!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Cyan
