# Load Testing Guide for LLM Embedding API
# Logistics & Transportation Industry

## Overview

This directory contains load testing scripts designed to stress-test the LLM Embedding API with realistic logistics and transportation industry data.

## Test Scripts

### 1. **LoadTestScript.ps1** (Comprehensive)
Full-featured load testing script with detailed metrics and infrastructure calculations.

**Features:**
- Realistic logistics data generation (trip manifests, accounting vouchers)
- Configurable load parameters
- Parallel execution with runspace pools
- Detailed performance metrics (percentiles, throughput, response times)
- Infrastructure requirement calculations
- Error tracking and reporting

**Usage:**
```powershell
# Basic test - 1000 requests over 10 seconds
.\LoadTestScript.ps1 -SkipCertificateCheck

# Custom configuration
.\LoadTestScript.ps1 `
    -ApiUrl "https://localhost:7009/api/embed" `
    -TotalRequests 500 `
    -ConcurrentThreads 50 `
    -DurationSeconds 10 `
    -SkipCertificateCheck

# Production endpoint test
.\LoadTestScript.ps1 `
    -ApiUrl "https://your-api.azurecontainerapps.io/api/embed" `
    -TotalRequests 1000 `
    -ConcurrentThreads 100 `
    -DurationSeconds 10
```

### 2. **QuickLoadTest.ps1** (Fast Test)
Lightweight script for quick validation testing.

**Usage:**
```powershell
# Quick test - 100 requests with 20 concurrent threads
.\QuickLoadTest.ps1 -SkipCertificateCheck

# Custom endpoint
.\QuickLoadTest.ps1 -ApiUrl "https://your-api.azurecontainerapps.io/api/embed"
```

## Test Data Types

### Trip/Shipment Data (60% of load)
Realistic freight shipment documents including:
- Shipment order details (origin, destination, dates)
- Cargo information (type, weight, pallets, declared value)
- Carrier and vehicle details
- Route and cost calculations
- Tracking events timeline
- BOL, PO, and reference numbers

### Accounting Voucher Data (40% of load)
Financial transaction documents including:
- Vendor invoice information
- Cost center and GL account allocations
- Line item details with various charges
- Financial summaries with tax calculations
- Approval workflow status
- Payment terms and scheduling

## Performance Targets

### Goal: 100 Requests per Second

**Test Configuration:**
- Total Requests: 1,000
- Duration: 10 seconds
- Concurrent Threads: 100
- Target Rate: 100 req/sec

**Expected Results:**
- Success Rate: >95%
- Avg Response Time: <500ms
- P95 Response Time: <1000ms
- P99 Response Time: <2000ms

## Infrastructure Requirements

### For 100 req/sec Sustained Load

#### Minimum Requirements (Single Instance)
```
CPU: 8 vCPUs
RAM: 8 GB
Storage: 2 GB (for model)
Network: 100 Mbps
```

#### Recommended Production (Single Instance)
```
CPU: 12-16 vCPUs
RAM: 12 GB
Storage: 5 GB
Network: 1 Gbps
```

#### High Availability Setup (Load Balanced)
```
Instance Count: 3 replicas
CPU per Instance: 8 vCPUs
RAM per Instance: 8 GB
Load Balancer: Azure Application Gateway or Azure Front Door
Expected Total Throughput: 150-300 req/sec
```

### Azure Container Apps Configuration

```yaml
resources:
  cpu: 4.0
  memory: 8Gi

scale:
  minReplicas: 2
  maxReplicas: 10
  rules:
    - name: http-scaling-rule
      http:
        metadata:
          concurrentRequests: "10"
```

### Memory Calculation Breakdown

```
Model Size:                      617 MB  (EmbeddingGemma-300m FP16)
OS & Runtime Overhead:           512 MB
Average Memory per Request:       75 MB
Concurrent Requests (typical):    15
Memory for Concurrent Requests: 1,125 MB
─────────────────────────────────────────
Total Estimated Memory:        ~2,254 MB (2.2 GB minimum)

Recommended with 2x safety margin: 8 GB
```

### CPU Calculation Breakdown

```
Average CPU Time per Request: 150ms
Target Throughput: 100 req/sec

Concurrent Processing Needed:
  100 req/sec × 0.15 sec/req = 15 concurrent operations

CPU Cores Needed (2 ops per core):
  15 ÷ 2 = 7.5 → 8 cores minimum

Recommended with headroom: 12-16 vCPUs
```

## Performance Optimization Tips

### 1. **Model Configuration**
```json
{
  "EmbeddingDimension": 256,  // Use 256d for 3x faster inference (POC)
  "MaxSequenceLength": 512    // Reduce if documents are shorter
}
```

### 2. **Application Settings**
- Enable HTTP/2 for better throughput
- Use connection pooling for downstream services
- Implement response compression

### 3. **Infrastructure Tuning**
- Enable Azure Container Apps built-in load balancing
- Use Azure Front Door for global distribution
- Configure auto-scaling based on CPU/memory metrics

### 4. **Monitoring**
- Track P95/P99 response times
- Monitor memory usage patterns
- Set up alerts for error rates >5%

## Running the Tests

### Prerequisites
```powershell
# Ensure the API is running
cd llm-embedding-api
dotnet run --project src/LlmEmbeddingApi

# In another terminal, run the load test
cd tests\LoadTest
```

### Step 1: Quick Validation
```powershell
.\QuickLoadTest.ps1 -SkipCertificateCheck
```

Expected output:
```
Quick Load Test - 100 requests in 10 seconds
API URL: https://localhost:7009/api/embed

✓ Request 1: 234.56ms
✓ Request 2: 189.23ms
...

Results:
  Success: 100
  Failed: 0
  Duration: 8.45s
  Throughput: 11.83 req/sec
  Avg Response: 245.67ms
  Min Response: 156.23ms
  Max Response: 567.89ms
```

### Step 2: Full Load Test
```powershell
.\LoadTestScript.ps1 -TotalRequests 1000 -ConcurrentThreads 100 -SkipCertificateCheck
```

### Step 3: Sustained Load Test (10 minutes)
```powershell
.\LoadTestScript.ps1 `
    -TotalRequests 60000 `
    -ConcurrentThreads 100 `
    -DurationSeconds 600 `
    -SkipCertificateCheck
```

## Interpreting Results

### Success Metrics
✅ **Good Performance:**
- Success Rate: >95%
- Avg Response: <500ms
- P95: <1000ms
- Throughput: >80 req/sec

⚠️ **Needs Optimization:**
- Success Rate: 85-95%
- Avg Response: 500-1000ms
- P95: 1000-2000ms
- Throughput: 50-80 req/sec

❌ **Critical Issues:**
- Success Rate: <85%
- Avg Response: >1000ms
- P95: >2000ms
- Throughput: <50 req/sec
- Errors: Memory exhaustion, timeouts

### Common Issues

**Issue:** High response times (>1000ms average)
**Solution:** 
- Increase CPU allocation
- Reduce EmbeddingDimension to 256
- Check for CPU throttling

**Issue:** Out of memory errors
**Solution:**
- Increase RAM allocation to 12 GB
- Reduce concurrent requests
- Check for memory leaks

**Issue:** High failure rate
**Solution:**
- Reduce concurrent threads
- Increase timeout values
- Check network connectivity

## Sample Output

```
============================================================================
LOAD TEST RESULTS
============================================================================

Summary:
  Total Requests: 1000
  Successful: 978
  Failed: 22
  Success Rate: 97.80%
  Duration: 10.45 seconds

Performance Metrics:
  Throughput: 93.59 req/sec
  Avg Response Time: 342.56 ms
  Min Response Time: 156.23 ms
  Max Response Time: 1234.67 ms

Response Time Percentiles:
  P50 (Median): 298.45 ms
  P90: 567.89 ms
  P95: 789.12 ms
  P99: 1123.45 ms

============================================================================
INFRASTRUCTURE REQUIREMENTS
============================================================================

Target: 100 requests/second

Estimated Requirements:
  CPU Cores: 8 cores (minimum)
  RAM: 3 GB
  Concurrent Requests: 15
  Model Size: 617 MB (FP16)
  Memory per Request: ~75 MB

Recommended Production Setup:
  Single Instance:
    - CPU: 8-16 vCPUs
    - RAM: 8-12 GB
    - Expected Throughput: 50-100 req/sec

  Load Balanced (3 instances):
    - 3x instances with 8 vCPUs, 8 GB RAM each
    - Expected Throughput: 150-300 req/sec
    - Includes failover and redundancy
```

## Next Steps

1. **Baseline Test**: Run QuickLoadTest to establish baseline
2. **Full Load**: Run LoadTestScript with target load
3. **Sustained Load**: Run 10-minute test to check for memory leaks
4. **Stress Test**: Gradually increase load until failure point
5. **Optimize**: Adjust configuration based on results
6. **Production Deploy**: Use recommended infrastructure settings

## Additional Resources

- [Azure Container Apps Scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app)
- [ONNX Runtime Performance Tuning](https://onnxruntime.ai/docs/performance/tune-performance.html)
- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/performance-testing)
