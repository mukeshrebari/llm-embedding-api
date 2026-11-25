# 📊 Load Testing Infrastructure Calculator

## 🎯 Target: 100 Requests/Second

### 📋 Quick Reference Card

```
┌─────────────────────────────────────────────────────────────┐
│  INFRASTRUCTURE REQUIREMENTS FOR 100 REQ/SEC                │
├─────────────────────────────────────────────────────────────┤
│  CPU:     8-16 vCPUs                                        │
│  RAM:     8-12 GB                                           │
│  Storage: 2 GB                                              │
│  Network: 100 Mbps+                                         │
│  Cost:    ~$150-200/month (single instance)                 │
│           ~$450-600/month (3 replicas, HA)                  │
└─────────────────────────────────────────────────────────────┘
```

## 📈 Scaling Calculator

### Single Instance Performance

| vCPUs | RAM  | 256d Throughput | 768d Throughput | Cost/Month |
|-------|------|-----------------|-----------------|------------|
| 4     | 4 GB | 30-50 req/sec   | 20-30 req/sec   | $75-100    |
| 8     | 8 GB | 60-90 req/sec   | 40-60 req/sec   | $150-200   |
| 12    | 12GB | 90-120 req/sec  | 60-80 req/sec   | $225-300   |
| 16    | 16GB | 120-150 req/sec | 80-100 req/sec  | $300-400   |

### Load Balanced (3 Replicas)

| vCPUs | RAM  | 256d Throughput | 768d Throughput | Cost/Month |
|-------|------|-----------------|-----------------|------------|
| 8     | 8 GB | 180-270 req/sec | 120-180 req/sec | $450-600   |
| 12    | 12GB | 270-360 req/sec | 180-240 req/sec | $675-900   |
| 16    | 16GB | 360-450 req/sec | 240-300 req/sec | $900-1200  |

## 🧮 Memory Calculation

```
┌─────────────────────────────────────────────────────┐
│  MEMORY BREAKDOWN (100 req/sec)                     │
├─────────────────────────────────────────────────────┤
│  Model (EmbeddingGemma FP16)            617 MB      │
│  OS & .NET Runtime                      512 MB      │
│  Concurrent Requests (15 × 75MB)      1,125 MB      │
│  ─────────────────────────────────────────────      │
│  Subtotal                             2,254 MB      │
│  Safety Margin (2x)                   4,508 MB      │
│  ─────────────────────────────────────────────      │
│  RECOMMENDED                          8,192 MB      │
│                                       (8 GB)        │
└─────────────────────────────────────────────────────┘
```

## ⚙️ CPU Calculation

```
┌─────────────────────────────────────────────────────┐
│  CPU BREAKDOWN (100 req/sec)                        │
├─────────────────────────────────────────────────────┤
│  Processing Time per Request          150 ms        │
│  Target Throughput                    100 req/sec   │
│  ─────────────────────────────────────────────      │
│  Concurrent Operations Needed         15            │
│  (100 × 0.15s = 15)                                 │
│  ─────────────────────────────────────────────      │
│  Operations per Core                  2             │
│  ─────────────────────────────────────────────      │
│  Minimum CPU Cores                    8 vCPUs       │
│  (15 ÷ 2 = 7.5 → round up)                          │
│  ─────────────────────────────────────────────      │
│  RECOMMENDED (with headroom)          12-16 vCPUs   │
└─────────────────────────────────────────────────────┘
```

## 📊 Performance Comparison: 256d vs 768d

### 256-Dimensional Embeddings (POC Optimized)
```
✅ Pros:
   • 3x faster inference
   • 1/3 less memory
   • 3x more throughput
   • Lower cost

⚠️ Cons:
   • Slightly reduced quality
   • Not recommended for production

Configuration:
   "EmbeddingDimension": 256

Performance:
   8 vCPU → 60-90 req/sec
   Cost: ~$150/month
```

### 768-Dimensional Embeddings (Full Quality)
```
✅ Pros:
   • Full embedding quality
   • Better semantic accuracy
   • Production-ready

⚠️ Cons:
   • Slower inference
   • More memory usage
   • Lower throughput

Configuration:
   "EmbeddingDimension": 768

Performance:
   8 vCPU → 40-60 req/sec
   Cost: ~$150/month
```

## 🎯 Decision Matrix

### Choose 256d If:
- ✅ POC/Development phase
- ✅ Budget constraints (<$200/month)
- ✅ High throughput needed (>100 req/sec)
- ✅ Acceptable quality trade-off

### Choose 768d If:
- ✅ Production deployment
- ✅ Quality is critical
- ✅ Moderate throughput needed (<60 req/sec)
- ✅ Budget allows ($300-600/month for HA)

## 🚀 Deployment Scenarios

### Scenario 1: POC/Development (Budget: $100-150/month)
```yaml
Replicas: 1
CPU: 8 vCPUs
Memory: 8 GB
Embedding Dimension: 256d
Expected Throughput: 60-90 req/sec
High Availability: ❌
Cost: ~$150/month
```

### Scenario 2: Small Production (Budget: $200-300/month)
```yaml
Replicas: 2
CPU: 8 vCPUs per replica
Memory: 8 GB per replica
Embedding Dimension: 256d or 768d
Expected Throughput: 120-180 req/sec (256d)
                     80-120 req/sec (768d)
High Availability: ✅
Cost: ~$300/month
```

### Scenario 3: Enterprise Production (Budget: $600-800/month)
```yaml
Replicas: 3-5 (auto-scale)
CPU: 12 vCPUs per replica
Memory: 12 GB per replica
Embedding Dimension: 768d
Expected Throughput: 180-400 req/sec
High Availability: ✅
Auto-scaling: ✅
Cost: ~$675-900/month
```

## 📉 Cost Optimization Strategies

### 1. Use 256d During POC
```
Savings: 66% on infrastructure
Trade-off: 5-10% quality reduction
Recommendation: Switch to 768d for production
```

### 2. Implement Auto-Scaling
```yaml
minReplicas: 2      # Base capacity
maxReplicas: 10     # Peak capacity
Scale based on: Concurrent requests > 10

Cost Savings: 40-60% during off-peak hours
```

### 3. Regional Deployment
```
Use Azure regions with lower pricing:
- East US: $0.000024/vCPU-second
- Central India: $0.000020/vCPU-second

Potential Savings: 15-20%
```

### 4. Reserved Capacity
```
Commit to 1-year reserved capacity:
- 25% discount on compute
- Fixed monthly cost
- Suitable for stable workloads

Savings: 25% on compute costs
```

## 🔍 Monitoring Dashboard Metrics

### Key Metrics (Set Alerts)
```
┌──────────────────────┬──────────┬──────────┬──────────┐
│ Metric               │ Good     │ Warning  │ Critical │
├──────────────────────┼──────────┼──────────┼──────────┤
│ Success Rate         │ >95%     │ 85-95%   │ <85%     │
│ Avg Response Time    │ <500ms   │ 500-1s   │ >1s      │
│ P95 Response Time    │ <1s      │ 1-2s     │ >2s      │
│ CPU Usage            │ <70%     │ 70-85%   │ >85%     │
│ Memory Usage         │ <75%     │ 75-90%   │ >90%     │
│ Throughput           │ >80/sec  │ 50-80/sec│ <50/sec  │
└──────────────────────┴──────────┴──────────┴──────────┘
```

## 🎓 Load Testing Phases

### Phase 1: Quick Validation (5 minutes)
```powershell
.\QuickLoadTest.ps1 -SkipCertificateCheck
```
**Purpose:** Verify API works correctly  
**Load:** 100 requests over ~10 seconds  
**Expected:** >95% success rate

### Phase 2: Target Load (10 seconds)
```powershell
.\LoadTestScript.ps1 -TotalRequests 1000 -SkipCertificateCheck
```
**Purpose:** Test 100 req/sec capacity  
**Load:** 1000 requests in 10 seconds  
**Expected:** >90 req/sec throughput

### Phase 3: Sustained Load (10 minutes)
```powershell
.\LoadTestScript.ps1 -TotalRequests 60000 -DurationSeconds 600
```
**Purpose:** Check stability and memory leaks  
**Load:** 100 req/sec for 10 minutes  
**Expected:** Consistent performance, no degradation

### Phase 4: Stress Test (Until Failure)
```powershell
.\LoadTestScript.ps1 -TotalRequests 100000 -ConcurrentThreads 200
```
**Purpose:** Find breaking point  
**Load:** Gradually increase until failure  
**Expected:** Identify maximum capacity

## 📦 Files Included

```
tests/LoadTest/
├── LoadTestScript.ps1      # Comprehensive PowerShell test
├── QuickLoadTest.ps1       # Fast validation test
├── load_test.py            # Python cross-platform test
├── requirements.txt        # Python dependencies
├── README.md               # Complete documentation
├── QUICKSTART.md           # Quick start guide
├── LOAD_TEST_SUMMARY.md   # Executive summary
└── INFRASTRUCTURE_CALC.md # This file
```

## 🎯 Final Recommendations

### For 100 req/sec Sustained Load:

**Minimum Configuration (POC):**
```
1× Azure Container App
8 vCPUs, 8 GB RAM
256-dimensional embeddings
Cost: $150-200/month
Capacity: 60-90 req/sec
```

**Recommended Production:**
```
3× Azure Container Apps (load balanced)
8 vCPUs, 8 GB RAM each
768-dimensional embeddings
Auto-scaling: 2-10 replicas
Cost: $450-600/month
Capacity: 120-180 req/sec
High Availability: ✅
```

**Enterprise Production:**
```
3-5× Azure Container Apps (auto-scale)
12 vCPUs, 12 GB RAM each
768-dimensional embeddings
Auto-scaling: 3-10 replicas
Azure Front Door: Global load balancing
Cost: $675-900/month
Capacity: 180-400 req/sec
High Availability: ✅
Global Distribution: ✅
```

---

**📌 Remember:** Start with POC configuration, measure actual performance with load tests, then scale based on real-world metrics!
