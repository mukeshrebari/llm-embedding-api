# Quick Start - Load Testing Guide

## Choose Your Platform

### Option 1: PowerShell (Windows) ⚡

**Quick Test (100 requests in ~10 seconds):**
```powershell
cd tests\LoadTest
.\QuickLoadTest.ps1 -SkipCertificateCheck
```

**Full Load Test (1000 requests, 100 req/sec target):**
```powershell
.\LoadTestScript.ps1 -SkipCertificateCheck
```

**Custom Load Test:**
```powershell
.\LoadTestScript.ps1 `
    -ApiUrl "https://localhost:7009/api/embed" `
    -TotalRequests 500 `
    -ConcurrentThreads 50 `
    -DurationSeconds 10 `
    -SkipCertificateCheck
```

### Option 2: Python (Cross-Platform) 🐍

**Setup:**
```bash
# Install dependencies
pip install -r requirements.txt
```

**Quick Test:**
```bash
python load_test.py --requests 100 --concurrent 20 --duration 10
```

**Full Load Test (1000 requests, 100 req/sec target):**
```bash
python load_test.py --requests 1000 --concurrent 100 --duration 10
```

**Custom endpoint:**
```bash
python load_test.py \
    --url "https://your-api.azurecontainerapps.io/api/embed" \
    --requests 1000 \
    --concurrent 100 \
    --duration 10
```

## Expected Results (100 req/sec target)

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
  
Recommended Production Setup:
  Single Instance:
    - CPU: 8-16 vCPUs
    - RAM: 8-12 GB
    - Expected Throughput: 50-100 req/sec
```

## Infrastructure Calculations

### Memory Requirements

For **100 req/sec** sustained load:

```
Component                      Memory (MB)
─────────────────────────────────────────
Model (EmbeddingGemma FP16)        617 MB
OS & Runtime Overhead              512 MB
Concurrent Requests (15 × 75MB)  1,125 MB
─────────────────────────────────────────
Total Required                   2,254 MB (2.2 GB)

Recommended (with 2x margin):      8 GB
```

### CPU Requirements

```
Average Processing Time: 150ms per request
Target Throughput: 100 req/sec

Concurrent Operations = 100 × 0.15 = 15 requests
CPU Cores (2 ops/core) = 15 ÷ 2 = 7.5 → 8 cores

Recommended (with headroom): 12-16 vCPUs
```

### Cost Breakdown (Azure Container Apps)

**Development/POC (8 vCPU, 8 GB RAM):**
- Single instance
- 50-100 req/sec capacity
- ~$150-200/month

**Production (8 vCPU, 8 GB RAM × 3 replicas):**
- High availability
- 150-300 req/sec capacity
- Auto-scaling enabled
- ~$450-600/month

## Performance Targets

| Metric | Good | Acceptable | Poor |
|--------|------|------------|------|
| Success Rate | >95% | 85-95% | <85% |
| Avg Response | <500ms | 500-1000ms | >1000ms |
| P95 Response | <1000ms | 1000-2000ms | >2000ms |
| Throughput | >80 req/sec | 50-80 req/sec | <50 req/sec |

## Troubleshooting

### High Response Times (>1000ms average)

**Symptoms:**
- Slow response times
- High CPU usage
- Request timeouts

**Solutions:**
1. Increase CPU allocation to 12-16 vCPUs
2. Reduce `EmbeddingDimension` to 256 in config
3. Check for CPU throttling in monitoring
4. Scale horizontally (add more replicas)

### Out of Memory Errors

**Symptoms:**
- 500 errors
- Application restarts
- Memory exhaustion in logs

**Solutions:**
1. Increase RAM to 12 GB
2. Reduce `MaxSequenceLength` in config
3. Lower concurrent request limit
4. Check for memory leaks (run sustained test)

### High Failure Rate (>10%)

**Symptoms:**
- Many failed requests
- Connection timeouts
- 503 Service Unavailable

**Solutions:**
1. Reduce concurrent threads
2. Increase timeout values
3. Check network connectivity
4. Verify API is running and healthy

### Low Throughput (<50 req/sec)

**Symptoms:**
- Below target throughput
- Queueing delays
- Slow processing

**Solutions:**
1. Scale horizontally (add replicas)
2. Optimize configuration (use 256d embeddings)
3. Enable HTTP/2
4. Use connection pooling

## Next Steps

1. ✅ Run `QuickLoadTest.ps1` to establish baseline
2. ✅ Run full load test with 1000 requests
3. ✅ Analyze results against targets
4. ✅ Run sustained 10-minute test
5. ✅ Adjust infrastructure based on findings
6. ✅ Deploy to production with recommended config

## Need Help?

Check the detailed README.md in this directory for:
- Complete test script documentation
- Infrastructure sizing calculator
- Azure Container Apps configuration
- Performance optimization tips
- Sample outputs and interpretations
