# Quick Load Test - 100 requests in 10 seconds
# Usage: .\QuickLoadTest.ps1

param(
    [string]$ApiUrl = "https://localhost:7009/api/embed",
    [switch]$SkipCertificateCheck
)

Write-Host "Quick Load Test - 100 requests in 10 seconds" -ForegroundColor Cyan
Write-Host "API URL: $ApiUrl" -ForegroundColor Yellow
Write-Host ""

# Sample logistics text
$sampleTexts = @(
    "Shipment ORD-123456 from Los Angeles to Chicago, 45000 lbs of electronics, ETA 3 days",
    "Freight invoice INV-789012 for carrier XYZ Logistics, total amount $5,432.50, due date 2025-02-15",
    "Delivery confirmation for Order #445566 at Dallas Distribution Center, signed by John Smith",
    "Temperature controlled shipment of pharmaceuticals, maintain 35-45°F, special handling required",
    "Fuel surcharge voucher VCH-20250115-5678, diesel cost increase 18%, total adjustment $892.00"
)

$results = @{
    Success = 0
    Failed = 0
    TotalTime = 0
    ResponseTimes = @()
}

$startTime = Get-Date

1..100 | ForEach-Object -Parallel {
    $text = $using:sampleTexts[($_ % $using:sampleTexts.Count)]
    $url = $using:ApiUrl
    $skipCert = $using:SkipCertificateCheck
    
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        $body = @{ text = $text } | ConvertTo-Json
        $params = @{
            Uri = $url
            Method = "Post"
            Body = $body
            ContentType = "application/json"
            TimeoutSec = 30
        }
        
        if ($skipCert) {
            $params.SkipCertificateCheck = $true
        }
        
        $response = Invoke-RestMethod @params
        $sw.Stop()
        
        return @{
            Success = $true
            Time = $sw.Elapsed.TotalMilliseconds
            RequestId = $_
        }
    } catch {
        $sw.Stop()
        return @{
            Success = $false
            Time = $sw.Elapsed.TotalMilliseconds
            RequestId = $_
            Error = $_.Exception.Message
        }
    }
} -ThrottleLimit 20 | ForEach-Object {
    if ($_.Success) {
        $results.Success++
        Write-Host "✓ Request $($_.RequestId): $([math]::Round($_.Time, 2))ms" -ForegroundColor Green
    } else {
        $results.Failed++
        Write-Host "✗ Request $($_.RequestId): $($_.Error)" -ForegroundColor Red
    }
    $results.ResponseTimes += $_.Time
}

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

Write-Host ""
Write-Host "Results:" -ForegroundColor Cyan
Write-Host "  Success: $($results.Success)" -ForegroundColor Green
Write-Host "  Failed: $($results.Failed)" -ForegroundColor $(if ($results.Failed -gt 0) { "Red" } else { "Green" })
Write-Host "  Duration: $([math]::Round($duration, 2))s"
Write-Host "  Throughput: $([math]::Round($results.Success / $duration, 2)) req/sec"
Write-Host "  Avg Response: $([math]::Round(($results.ResponseTimes | Measure-Object -Average).Average, 2))ms"
Write-Host "  Min Response: $([math]::Round(($results.ResponseTimes | Measure-Object -Minimum).Minimum, 2))ms"
Write-Host "  Max Response: $([math]::Round(($results.ResponseTimes | Measure-Object -Maximum).Maximum, 2))ms"
