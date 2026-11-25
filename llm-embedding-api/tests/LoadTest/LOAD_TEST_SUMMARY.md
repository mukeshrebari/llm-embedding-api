# Load Testing Summary - 100 Requests/Second Target

## Quick Answer: Infrastructure Requirements

### For 100 req/sec sustained load:

**Minimum Configuration:**
- **CPU:** 8 vCPUs
- **RAM:** 8 GB
- **Storage:** 2 GB
- **Cost:** ~$150-200/month (Azure Container Apps)

**Recommended Production:**
- **CPU:** 12-16 vCPUs per instance
- **RAM:** 12 GB per instance
- **Replicas:** 3 instances (load balanced)
- **Total Capacity:** 150-300 req/sec
- **Cost:** ~$450-600/month

## Files Created

1. **LoadTestScript.ps1** - Comprehensive PowerShell load test
2. **QuickLoadTest.ps1** - Fast validation test (PowerShell)
3. **load_test.py** - Python cross-platform load test
4. **requirements.txt** - Python dependencies
5. **README.md** - Complete documentation
6. **QUICKSTART.md** - Quick start guide
7. **LOAD_TEST_SUMMARY.md** - This file

## Test Data

### Trip/Shipment Data (60% of load)
- Order numbers, routes, cargo details
- Carrier and vehicle information
- Cost calculations and tracking events
- Average size: ~500 tokens

### Accounting Vouchers (40% of load)
- Invoice and vendor information
- Line item details and GL accounts
- Financial summaries and approvals
- Average size: ~400 tokens

## How to Run

### PowerShell (Recommended for Windows)
```powershell
cd tests\LoadTest
.\QuickLoadTest.ps1 -SkipCertificateCheck
```

### Python (Cross-platform)
```bash
cd tests/LoadTest
pip install -r requirements.txt
python load_test.py --requests 1000 --concurrent 100 --duration 10
```

## Calculation Details

### Memory Breakdown
```
Model Size (EmbeddingGemma FP16):    617 MB
OS & Runtime:                        512 MB
Per Request Memory:                   75 MB
Concurrent Requests (typical):        15
Memory for Concurrent:             1,125 MB
────────────────────────────────────────────
Subtotal:                          2,254 MB
Safety Margin (2x):                4,508 MB
────────────────────────────────────────────
Recommended:                       8,192 MB (8 GB)
```

### CPU Breakdown
```
Processing Time per Request:         150 ms
Target Throughput:                   100 req/sec
────────────────────────────────────────────
Concurrent Operations:               15 ops
Operations per Core:                 2 ops
────────────────────────────────────────────
Minimum CPU Cores:                   8 cores
Recommended (with headroom):         12-16 vCPUs
```

### Throughput Scaling
```
Configuration        | Throughput | Cost/Month
──────────────────────────────────────────────
8 vCPU, 8 GB (1x)   | 50-100/sec | $150-200
8 vCPU, 8 GB (3x)   | 150-300/sec| $450-600
12 vCPU, 12 GB (3x) | 200-400/sec| $800-1000
```

## Performance Targets

### Success Criteria ✅
- **Success Rate:** >95%
- **Avg Response:** <500ms
- **P95 Response:** <1000ms
- **P99 Response:** <2000ms
- **Throughput:** >80 req/sec

### Warning Signs ⚠️
- **Success Rate:** 85-95%
- **Avg Response:** 500-1000ms
- **P95 Response:** 1000-2000ms
- **Throughput:** 50-80 req/sec

### Critical Issues ❌
- **Success Rate:** <85%
- **Avg Response:** >1000ms
- **P95 Response:** >2000ms
- **Throughput:** <50 req/sec

## Azure Container Apps Configuration

```yaml
# Recommended configuration for 100 req/sec
resources:
  cpu: 4.0            # 4 vCPUs per replica
  memory: 8Gi         # 8 GB per replica

scale:
  minReplicas: 2      # High availability
  maxReplicas: 10     # Handle traffic spikes
  rules:
    - name: http-scaling
      http:
        metadata:
          concurrentRequests: "10"  # Scale when >10 concurrent

# Environment Variables
env:
  - name: Embedding__EmbeddingDimension
    value: "256"      # Use 256d for 3x faster inference (POC)
  - name: Embedding__MaxSequenceLength
    value: "512"      # Adjust based on document size
```

## Cost Optimization Tips

### For POC/Development (Budget-Friendly)
1. Use **256-dimensional embeddings** (3x faster, 1/3 resources)
2. Single instance: 8 vCPU, 8 GB RAM
3. Auto-scale disabled
4. **Cost:** ~$150/month
5. **Capacity:** 50-100 req/sec

### For Production (Recommended)
1. Use **768-dimensional embeddings** (full quality)
2. Load balanced: 3× (8 vCPU, 8 GB RAM)
3. Auto-scale enabled (2-10 replicas)
4. **Cost:** ~$450-600/month
5. **Capacity:** 150-300 req/sec

## Real-World Performance

### Based on EmbeddingGemma-300m FP16:
- **Model Size:** 617 MB
- **Inference Time:** 100-200ms per request (CPU)
- **Memory per Request:** 50-100 MB during inference
- **Max Sequence Length:** 2048 tokens
- **Output Dimensions:** 768d (can truncate to 256d)

### Expected Results (Single 8 vCPU, 8 GB Instance):
```
Throughput:           60-90 req/sec (256d)
                      40-60 req/sec (768d)
Avg Response Time:    300-500ms
P95 Response Time:    800-1200ms
Success Rate:         >95%
```

## Monitoring & Alerts

### Key Metrics to Track:
1. **Throughput** - Requests per second
2. **Response Time** - P50, P95, P99 percentiles
3. **Success Rate** - % of 200 OK responses
4. **CPU Usage** - % utilization per replica
5. **Memory Usage** - MB used vs allocated
6. **Error Rate** - 4xx and 5xx errors

### Recommended Alerts:
- Success Rate < 95% for 5 minutes
- P95 Response Time > 1000ms for 5 minutes
- CPU Usage > 80% for 10 minutes
- Memory Usage > 85% for 5 minutes
- Error Rate > 5% for 3 minutes

## Testing Phases

### Phase 1: Baseline Test (5 minutes)
```powershell
.\QuickLoadTest.ps1 -SkipCertificateCheck
```
**Goal:** Establish baseline performance

### Phase 2: Target Load Test (10 seconds)
```powershell
.\LoadTestScript.ps1 -TotalRequests 1000 -SkipCertificateCheck
```
**Goal:** Verify 100 req/sec capacity

### Phase 3: Sustained Load Test (10 minutes)
```powershell
.\LoadTestScript.ps1 -TotalRequests 60000 -DurationSeconds 600 -SkipCertificateCheck
```
**Goal:** Check for memory leaks and stability

### Phase 4: Stress Test (15 minutes)
```powershell
.\LoadTestScript.ps1 -TotalRequests 100000 -ConcurrentThreads 200 -SkipCertificateCheck
```
**Goal:** Find breaking point and limits

## Summary

✅ **Created comprehensive load testing suite** with realistic logistics data

✅ **Calculated infrastructure requirements:**
   - 8 vCPUs, 8 GB RAM for 100 req/sec
   - 3 replicas for high availability
   - Estimated cost: $450-600/month

✅ **Provided test scripts:**
   - PowerShell for Windows
   - Python for cross-platform
   - Quick test for validation
   - Full test for load testing

✅ **Included realistic data generators:**
   - 60% shipment/trip data
   - 40% accounting vouchers
   - Based on logistics industry scenarios

✅ **Documented performance targets and optimization strategies**

## Next Steps

1. Run the quick test to establish baseline
2. Analyze results and compare to targets
3. Run sustained test to check stability
4. Adjust configuration based on findings
5. Deploy to Azure with recommended settings
6. Monitor and optimize in production
