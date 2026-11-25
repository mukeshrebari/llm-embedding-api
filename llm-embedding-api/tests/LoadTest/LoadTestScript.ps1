# ============================================================================
# Load Testing Script for LLM Embedding API
# Logistics and Transportation Industry Realistic Data
# Target: 100 transactions per second
# ============================================================================

param(
    [string]$ApiUrl = "https://localhost:7009/api/embed",
    [int]$TotalRequests = 1000,
    [int]$ConcurrentThreads = 100,
    [int]$DurationSeconds = 10,
    [switch]$SkipCertificateCheck
)

# ============================================================================
# Realistic Logistics and Transportation Data Generator
# ============================================================================

$cities = @(
    "Los Angeles, CA", "Chicago, IL", "Houston, TX", "Phoenix, AZ", "Philadelphia, PA",
    "San Antonio, TX", "San Diego, CA", "Dallas, TX", "San Jose, CA", "Austin, TX",
    "Jacksonville, FL", "Fort Worth, TX", "Columbus, OH", "Charlotte, NC", "Seattle, WA",
    "Denver, CO", "Boston, MA", "Portland, OR", "Detroit, MI", "Memphis, TN"
)

$cargoTypes = @(
    "Electronics and Computer Equipment", "Automotive Parts and Accessories", 
    "Pharmaceuticals and Medical Supplies", "Perishable Food Products",
    "Industrial Machinery and Tools", "Textiles and Apparel", 
    "Chemical Products and Raw Materials", "Construction Materials",
    "Consumer Packaged Goods", "Agricultural Products and Feed",
    "Furniture and Home Goods", "Paper Products and Printing Materials",
    "Steel and Metal Products", "Plastics and Polymers",
    "Beverages and Bottled Goods"
)

$carriers = @(
    "FedEx Freight", "UPS Freight", "XPO Logistics", "Old Dominion Freight",
    "YRC Worldwide", "Estes Express Lines", "ABF Freight", "SAIA Motor Freight",
    "Southeastern Freight Lines", "AAA Cooper Transportation", "R+L Carriers",
    "Central Transport", "Dayton Freight", "Oak Harbor Freight Lines"
)

$vehicles = @(
    "53' Dry Van Semi-Trailer", "48' Flatbed Trailer", "Refrigerated Reefer Truck",
    "Box Truck 26ft", "Curtain Side Trailer", "Step Deck Trailer",
    "Tanker Truck 8000 Gallon", "Intermodal Container 40ft", "Lowboy Trailer"
)

$statuses = @(
    "Pending Pickup", "In Transit", "At Distribution Center", "Out for Delivery",
    "Delivered", "Delayed - Weather", "Delayed - Mechanical", "On Hold - Customs",
    "Rescheduled", "Exception - Address Correction Required"
)

$costCenters = @(
    "Transportation - Linehaul", "Transportation - Local Delivery", 
    "Warehousing - Receiving", "Warehousing - Storage", "Warehousing - Picking",
    "Fuel and Energy", "Equipment Maintenance", "Insurance and Liability",
    "Administrative Overhead", "Technology and Systems", "Labor - Drivers",
    "Labor - Warehouse", "Compliance and Regulatory"
)

$glAccounts = @(
    "5000-100 - Freight Revenue", "5100-200 - Fuel Surcharge Revenue",
    "5200-300 - Accessorial Revenue", "6000-100 - Driver Wages",
    "6100-200 - Fuel Expense", "6200-300 - Equipment Lease",
    "6300-400 - Maintenance and Repairs", "6400-500 - Insurance Expense",
    "6500-600 - Depreciation - Equipment", "7000-100 - Administrative Salaries"
)

function Get-RandomTripData {
    $orderNum = "ORD-$(Get-Random -Minimum 100000 -Maximum 999999)"
    $origin = $cities | Get-Random
    $destination = $cities | Get-Random -Count 1 | Where-Object { $_ -ne $origin }
    $cargo = $cargoTypes | Get-Random
    $carrier = $carriers | Get-Random
    $vehicle = $vehicles | Get-Random
    $status = $statuses | Get-Random
    $weight = Get-Random -Minimum 500 -Maximum 45000
    $distance = Get-Random -Minimum 50 -Maximum 2800
    $rate = [math]::Round((Get-Random -Minimum 1.5 -Maximum 3.5), 2)
    $cost = [math]::Round($distance * $rate, 2)
    $pickupDate = (Get-Date).AddDays(-(Get-Random -Minimum 1 -Maximum 30))
    $deliveryDate = $pickupDate.AddDays((Get-Random -Minimum 1 -Maximum 7))
    
    $tripData = @"
SHIPMENT ORDER: $orderNum
========================================
ORIGIN: $origin
DESTINATION: $destination
PICKUP DATE: $($pickupDate.ToString("yyyy-MM-dd HH:mm"))
DELIVERY DATE: $($deliveryDate.ToString("yyyy-MM-dd HH:mm"))
STATUS: $status

CARGO DETAILS:
- Type: $cargo
- Weight: $weight lbs
- Pallets: $(Get-Random -Minimum 1 -Maximum 26)
- Declared Value: `$$(Get-Random -Minimum 5000 -Maximum 250000)
- Special Instructions: $(if((Get-Random -Minimum 0 -Maximum 2) -eq 1){"Temperature controlled, maintain 35-45°F"}else{"Handle with care, fragile items"})

CARRIER INFORMATION:
- Carrier: $carrier
- Vehicle: $vehicle
- Driver: $(("John","Mike","Sarah","David","Lisa","Tom") | Get-Random) $(("Smith","Johnson","Williams","Brown","Jones","Garcia") | Get-Random)
- Driver License: CDL-$(Get-Random -Minimum 100000 -Maximum 999999)
- Trailer Number: TRL-$(Get-Random -Minimum 1000 -Maximum 9999)

ROUTE DETAILS:
- Distance: $distance miles
- Rate per Mile: `$$rate
- Base Freight Charge: `$$cost
- Fuel Surcharge (18%): `$$([math]::Round($cost * 0.18, 2))
- Total Charge: `$$([math]::Round($cost * 1.18, 2))
- Estimated Transit Time: $(Get-Random -Minimum 1 -Maximum 7) days

TRACKING EVENTS:
- $($pickupDate.ToString("yyyy-MM-dd HH:mm")) - Order received and confirmed
- $($pickupDate.AddHours(4).ToString("yyyy-MM-dd HH:mm")) - Dispatched to carrier
- $($pickupDate.AddDays(1).ToString("yyyy-MM-dd HH:mm")) - Picked up from origin
- $($pickupDate.AddDays(2).ToString("yyyy-MM-dd HH:mm")) - In transit checkpoint 1
- $($pickupDate.AddDays(3).ToString("yyyy-MM-dd HH:mm")) - Arrived at distribution center

NOTES:
BOL Number: BOL-$(Get-Random -Minimum 100000 -Maximum 999999)
PO Number: PO-$(Get-Random -Minimum 10000 -Maximum 99999)
Reference: REF-$(Get-Random -Minimum 1000 -Maximum 9999)
Customer: $(("ABC Logistics","XYZ Manufacturing","Global Supply Co","Premier Distribution","Midwest Freight") | Get-Random)
"@
    
    return $tripData
}

function Get-RandomVoucherData {
    $voucherNum = "VCH-$(Get-Date -Format "yyyyMMdd")-$(Get-Random -Minimum 1000 -Maximum 9999)"
    $vendor = $carriers | Get-Random
    $costCenter = $costCenters | Get-Random
    $glAccount = $glAccounts | Get-Random
    $invoiceDate = (Get-Date).AddDays(-(Get-Random -Minimum 1 -Maximum 30))
    $dueDate = $invoiceDate.AddDays(30)
    
    $lineItems = @()
    $lineCount = Get-Random -Minimum 3 -Maximum 8
    $totalAmount = 0
    
    for ($i = 1; $i -le $lineCount; $i++) {
        $desc = switch (Get-Random -Minimum 1 -Maximum 6) {
            1 { "Freight Charges - Order #$(Get-Random -Minimum 100000 -Maximum 999999)" }
            2 { "Fuel Surcharge - $(Get-Random -Minimum 15 -Maximum 25)% of base rate" }
            3 { "Detention Fee - $(Get-Random -Minimum 1 -Maximum 4) hours @ `$75/hr" }
            4 { "Accessorial - Liftgate Service" }
            5 { "Lumper Fee - Unloading at warehouse" }
            6 { "Insurance Premium - Cargo coverage" }
        }
        $amount = [math]::Round((Get-Random -Minimum 100 -Maximum 5000), 2)
        $totalAmount += $amount
        $lineItems += "  Line $i`: $desc - `$$amount"
    }
    
    $voucherData = @"
ACCOUNTING VOUCHER: $voucherNum
========================================
VOUCHER DATE: $(Get-Date -Format "yyyy-MM-dd")
INVOICE DATE: $($invoiceDate.ToString("yyyy-MM-dd"))
DUE DATE: $($dueDate.ToString("yyyy-MM-dd"))
PAYMENT TERMS: Net 30

VENDOR INFORMATION:
- Vendor Name: $vendor
- Vendor ID: VND-$(Get-Random -Minimum 1000 -Maximum 9999)
- Invoice Number: INV-$(Get-Random -Minimum 100000 -Maximum 999999)
- Payment Method: ACH Direct Deposit
- Bank Account: ****$(Get-Random -Minimum 1000 -Maximum 9999)

COST ALLOCATION:
- Cost Center: $costCenter
- GL Account: $glAccount
- Department: $(("Operations","Finance","Administration","Fleet Management") | Get-Random)
- Location: $(("Corporate HQ","Distribution Center East","Distribution Center West","Regional Office") | Get-Random)

LINE ITEM DETAILS:
$($lineItems -join "`n")

FINANCIAL SUMMARY:
- Subtotal: `$$totalAmount
- Tax ($(Get-Random -Minimum 6 -Maximum 10)%): `$$([math]::Round($totalAmount * 0.08, 2))
- Total Amount Due: `$$([math]::Round($totalAmount * 1.08, 2))
- Amount Paid: `$0.00
- Balance Due: `$$([math]::Round($totalAmount * 1.08, 2))

APPROVAL WORKFLOW:
- Requested By: $(("Manager A","Manager B","Supervisor C","Director D") | Get-Random)
- Approved By: $(("Controller","CFO","VP Finance") | Get-Random)
- Approval Date: $(Get-Date -Format "yyyy-MM-dd")
- Status: $(("Pending Approval","Approved","Paid","Scheduled for Payment") | Get-Random)

NOTES:
Purchase Order: PO-$(Get-Random -Minimum 10000 -Maximum 99999)
Project Code: PRJ-$(Get-Random -Minimum 100 -Maximum 999)
Reference: Batch payment scheduled for $((Get-Date).AddDays(15).ToString("yyyy-MM-dd"))
"@
    
    return $voucherData
}

# ============================================================================
# Load Testing Functions
# ============================================================================

$global:results = @{
    TotalRequests = 0
    SuccessfulRequests = 0
    FailedRequests = 0
    TotalResponseTime = 0
    MinResponseTime = [double]::MaxValue
    MaxResponseTime = 0
    Errors = @()
}

$global:responseTimes = [System.Collections.Concurrent.ConcurrentBag[double]]::new()

function Send-EmbeddingRequest {
    param([string]$text, [int]$requestId)
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    try {
        $body = @{ text = $text } | ConvertTo-Json -Depth 10
        
        $params = @{
            Uri = $ApiUrl
            Method = "Post"
            Body = $body
            ContentType = "application/json"
            TimeoutSec = 30
        }
        
        if ($SkipCertificateCheck) {
            $params.SkipCertificateCheck = $true
        }
        
        $response = Invoke-RestMethod @params
        
        $stopwatch.Stop()
        $responseTime = $stopwatch.Elapsed.TotalMilliseconds
        
        [void]$global:responseTimes.Add($responseTime)
        
        $global:results.SuccessfulRequests++
        $global:results.TotalResponseTime += $responseTime
        
        if ($responseTime -lt $global:results.MinResponseTime) {
            $global:results.MinResponseTime = $responseTime
        }
        if ($responseTime -gt $global:results.MaxResponseTime) {
            $global:results.MaxResponseTime = $responseTime
        }
        
        Write-Host "✓ Request $requestId`: $([math]::Round($responseTime, 2))ms - Embedding: $($response.embedding.Count)d" -ForegroundColor Green
        
    } catch {
        $stopwatch.Stop()
        $global:results.FailedRequests++
        $errorMsg = "Request $requestId failed: $($_.Exception.Message)"
        $global:results.Errors += $errorMsg
        Write-Host "✗ $errorMsg" -ForegroundColor Red
    }
}

# ============================================================================
# Main Load Test Execution
# ============================================================================

Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "LOAD TESTING SCRIPT - LLM EMBEDDING API" -ForegroundColor Cyan
Write-Host "Logistics and Transportation Industry Data" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  API URL: $ApiUrl"
Write-Host "  Total Requests: $TotalRequests"
Write-Host "  Concurrent Threads: $ConcurrentThreads"
Write-Host "  Target Duration: $DurationSeconds seconds"
Write-Host "  Target Rate: $([math]::Round($TotalRequests / $DurationSeconds, 2)) req/sec"
Write-Host ""

# Generate test data upfront
Write-Host "Generating test data..." -ForegroundColor Yellow
$testData = @()
$tripCount = [math]::Floor($TotalRequests * 0.6)  # 60% trip data
$voucherCount = $TotalRequests - $tripCount        # 40% voucher data

for ($i = 0; $i -lt $tripCount; $i++) {
    $testData += @{ type = "trip"; data = Get-RandomTripData }
}
for ($i = 0; $i -lt $voucherCount; $i++) {
    $testData += @{ type = "voucher"; data = Get-RandomVoucherData }
}

# Shuffle the data
$testData = $testData | Sort-Object { Get-Random }
Write-Host "Generated $($testData.Count) test records (Trip: $tripCount, Voucher: $voucherCount)" -ForegroundColor Green
Write-Host ""

# Start load test
Write-Host "Starting load test..." -ForegroundColor Yellow
Write-Host ""

$startTime = Get-Date
$global:results.TotalRequests = $TotalRequests

# Execute requests in parallel using runspaces
$runspacePool = [runspacefactory]::CreateRunspacePool(1, $ConcurrentThreads)
$runspacePool.Open()

$jobs = @()

for ($i = 0; $i -lt $TotalRequests; $i++) {
    $testRecord = $testData[$i]
    
    $powershell = [powershell]::Create().AddScript({
        param($ApiUrl, $Text, $RequestId, $SkipCert)
        
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        
        try {
            $body = @{ text = $Text } | ConvertTo-Json -Depth 10
            
            $params = @{
                Uri = $ApiUrl
                Method = "Post"
                Body = $body
                ContentType = "application/json"
                TimeoutSec = 30
            }
            
            if ($SkipCert) {
                $params.SkipCertificateCheck = $true
            }
            
            $response = Invoke-RestMethod @params
            $stopwatch.Stop()
            
            return @{
                Success = $true
                RequestId = $RequestId
                ResponseTime = $stopwatch.Elapsed.TotalMilliseconds
                EmbeddingCount = $response.embedding.Count
            }
            
        } catch {
            $stopwatch.Stop()
            return @{
                Success = $false
                RequestId = $RequestId
                ResponseTime = $stopwatch.Elapsed.TotalMilliseconds
                Error = $_.Exception.Message
            }
        }
    }).AddArgument($ApiUrl).AddArgument($testRecord.data).AddArgument($i + 1).AddArgument($SkipCertificateCheck.IsPresent)
    
    $powershell.RunspacePool = $runspacePool
    
    $jobs += @{
        PowerShell = $powershell
        Handle = $powershell.BeginInvoke()
    }
    
    # Throttle to maintain target rate
    if ($i -gt 0 -and $i % $ConcurrentThreads -eq 0) {
        Start-Sleep -Milliseconds ([math]::Max(1, [int](($DurationSeconds * 1000) / ($TotalRequests / $ConcurrentThreads))))
    }
}

# Wait for all jobs to complete and collect results
Write-Host "Waiting for all requests to complete..." -ForegroundColor Yellow

foreach ($job in $jobs) {
    $result = $job.PowerShell.EndInvoke($job.Handle)
    
    if ($result.Success) {
        $global:results.SuccessfulRequests++
        $global:results.TotalResponseTime += $result.ResponseTime
        [void]$global:responseTimes.Add($result.ResponseTime)
        
        if ($result.ResponseTime -lt $global:results.MinResponseTime) {
            $global:results.MinResponseTime = $result.ResponseTime
        }
        if ($result.ResponseTime -gt $global:results.MaxResponseTime) {
            $global:results.MaxResponseTime = $result.ResponseTime
        }
        
        Write-Host "✓ Request $($result.RequestId): $([math]::Round($result.ResponseTime, 2))ms - Embedding: $($result.EmbeddingCount)d" -ForegroundColor Green
    } else {
        $global:results.FailedRequests++
        $errorMsg = "Request $($result.RequestId) failed: $($result.Error)"
        $global:results.Errors += $errorMsg
        Write-Host "✗ $errorMsg" -ForegroundColor Red
    }
    
    $job.PowerShell.Dispose()
}

$runspacePool.Close()
$runspacePool.Dispose()

$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

# ============================================================================
# Calculate Statistics
# ============================================================================

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "LOAD TEST RESULTS" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

$successRate = ($global:results.SuccessfulRequests / $global:results.TotalRequests) * 100
$avgResponseTime = if ($global:results.SuccessfulRequests -gt 0) { $global:results.TotalResponseTime / $global:results.SuccessfulRequests } else { 0 }
$throughput = $global:results.SuccessfulRequests / $duration

Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  Total Requests: $($global:results.TotalRequests)"
Write-Host "  Successful: $($global:results.SuccessfulRequests)" -ForegroundColor Green
Write-Host "  Failed: $($global:results.FailedRequests)" -ForegroundColor $(if ($global:results.FailedRequests -gt 0) { "Red" } else { "Green" })
Write-Host "  Success Rate: $([math]::Round($successRate, 2))%"
Write-Host "  Duration: $([math]::Round($duration, 2)) seconds"
Write-Host ""

Write-Host "Performance Metrics:" -ForegroundColor Yellow
Write-Host "  Throughput: $([math]::Round($throughput, 2)) req/sec"
Write-Host "  Avg Response Time: $([math]::Round($avgResponseTime, 2)) ms"
Write-Host "  Min Response Time: $([math]::Round($global:results.MinResponseTime, 2)) ms"
Write-Host "  Max Response Time: $([math]::Round($global:results.MaxResponseTime, 2)) ms"

# Calculate percentiles
if ($global:responseTimes.Count -gt 0) {
    $sortedTimes = $global:responseTimes | Sort-Object
    $p50 = $sortedTimes[[math]::Floor($sortedTimes.Count * 0.50)]
    $p90 = $sortedTimes[[math]::Floor($sortedTimes.Count * 0.90)]
    $p95 = $sortedTimes[[math]::Floor($sortedTimes.Count * 0.95)]
    $p99 = $sortedTimes[[math]::Floor($sortedTimes.Count * 0.99)]
    
    Write-Host ""
    Write-Host "Response Time Percentiles:" -ForegroundColor Yellow
    Write-Host "  P50 (Median): $([math]::Round($p50, 2)) ms"
    Write-Host "  P90: $([math]::Round($p90, 2)) ms"
    Write-Host "  P95: $([math]::Round($p95, 2)) ms"
    Write-Host "  P99: $([math]::Round($p99, 2)) ms"
}

if ($global:results.Errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Errors (first 10):" -ForegroundColor Red
    $global:results.Errors | Select-Object -First 10 | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "INFRASTRUCTURE REQUIREMENTS" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Calculate resource requirements
# Assumptions:
# - Each request processes ~500 tokens (average logistics document)
# - Model: EmbeddingGemma 300M FP16 (617 MB)
# - Memory per request: ~50-100 MB during inference
# - CPU per request: ~100-200ms on modern CPU

$targetThroughput = 100  # req/sec
$avgRequestMemory = 75   # MB
$avgRequestCpuTime = 150 # milliseconds
$modelSize = 617         # MB

$concurrentRequests = [math]::Ceiling($targetThroughput * ($avgRequestCpuTime / 1000))
$memoryForRequests = $concurrentRequests * $avgRequestMemory
$totalMemory = $modelSize + $memoryForRequests + 512  # +512 MB for OS and overhead

$cpuCoresNeeded = [math]::Ceiling($concurrentRequests / 2)  # Assume 2 requests per core

Write-Host "Target: 100 requests/second" -ForegroundColor Yellow
Write-Host ""
Write-Host "Estimated Requirements:" -ForegroundColor Yellow
Write-Host "  CPU Cores: $cpuCoresNeeded cores (minimum)" -ForegroundColor Cyan
Write-Host "  RAM: $([math]::Ceiling($totalMemory / 1024)) GB" -ForegroundColor Cyan
Write-Host "  Concurrent Requests: $concurrentRequests"
Write-Host "  Model Size: 617 MB (FP16)"
Write-Host "  Memory per Request: ~$avgRequestMemory MB"
Write-Host ""

Write-Host "Recommended Production Setup:" -ForegroundColor Yellow
Write-Host "  Single Instance:" -ForegroundColor Cyan
Write-Host "    - CPU: 8-16 vCPUs"
Write-Host "    - RAM: 8-12 GB"
Write-Host "    - Expected Throughput: 50-100 req/sec"
Write-Host ""
Write-Host "  Load Balanced (3 instances):" -ForegroundColor Cyan
Write-Host "    - 3x instances with 8 vCPUs, 8 GB RAM each"
Write-Host "    - Expected Throughput: 150-300 req/sec"
Write-Host "    - Includes failover and redundancy"
Write-Host ""

Write-Host "Azure Container Apps Recommended Configuration:" -ForegroundColor Yellow
Write-Host "  Scale Rule: HTTP concurrent requests = 10"
Write-Host "  Min Replicas: 2"
Write-Host "  Max Replicas: 10"
Write-Host "  CPU: 4 vCPU per replica"
Write-Host "  Memory: 8 GB per replica"
Write-Host ""

Write-Host "Load test completed successfully!" -ForegroundColor Green
