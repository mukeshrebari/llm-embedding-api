# Performance Monitoring & Azure Container Instance Sizing

This directory contains comprehensive performance monitoring tools to help determine the optimal Azure Container Instance (ACI) configuration for the LLM Embedding API.

## ?? What's Included

### Services
- **`PerformanceMetricsService.cs`** - Core metrics collection using .NET Diagnostics API
- **`PerformanceCollectorService.cs`** - Background service that collects metrics every 10 seconds
- **`PerformanceMonitoringMiddleware.cs`** - HTTP request performance tracking

### Scripts
- **`Run-LoadTest.ps1`** - Automated load testing with detailed reporting
- **`Watch-Performance.ps1`** - Real-time performance dashboard

### Documentation
- **`QUICK_START_PERFORMANCE.md`** - Quick start guide
- **`PERFORMANCE_MONITORING.md`** - Complete reference documentation

## ?? Quick Start

### 1. Start the API
```powershell
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj
```

### 2. Run Load Test
```powershell
# Basic test
.\scripts\Run-LoadTest.ps1

# Heavy test
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10
```

### 3. Get Azure Recommendations
The load test automatically displays Azure Container Instance recommendations:
```
====================================
Azure Container Instance Recommendations
====================================

Recommended Configuration:
  CPU Cores: 1 vCPU
  Memory:            1 GB
  
Recommendation: Recommended ACI SKU: 1 vCPU, 1GB RAM
```

## ?? Real-Time Monitoring

Watch live metrics during testing:
```powershell
.\scripts\Watch-Performance.ps1
```

This displays:
- ? CPU usage with visual bars
- ? Memory consumption (working set, private, GC heap)
- ? Garbage collection statistics
- ? Live Azure ACI recommendations

## ?? API Endpoints

### Get Current Metrics
```bash
GET /api/metrics/current
```
Returns real-time CPU, memory, and GC metrics.

### Get Performance Statistics
```bash
GET /api/metrics/statistics
```
Returns aggregated statistics with **Azure ACI recommendations**.

### Get Historical Snapshots
```bash
GET /api/metrics/snapshots?limit=50
```
Returns historical performance data (up to 1000 snapshots).

### Force Garbage Collection
```bash
POST /api/metrics/gc
```
Triggers GC and reports memory freed (for testing purposes).

## ?? Metrics Collected

### System Metrics
- **CPU Usage**: Current, peak, and average CPU percentage
- **Memory Usage**: Working set, private memory, GC heap
- **Thread Count**: Active thread count
- **Handle Count**: OS handles in use

### Garbage Collection
- **Gen 0/1/2 Collections**: Count of each generation collection
- **GC Heap Size**: Current managed heap size

### Request Metrics
- **Request Duration**: Time to generate embeddings
- **Input Length**: Character count of input text
- **Error Rate**: Failed request tracking

## ?? Azure Container Instance Recommendations

### How Recommendations Are Calculated

**Memory Calculation:**
```
Recommended Memory = CEILING((Peak Memory MB / 1024) * 1.5)
```
- Uses peak memory observed
- Adds 50% buffer for safety
- Rounded to nearest 0.5 GB
- Minimum: 0.5 GB

**CPU Calculation:**
```
Recommended CPU = CEILING((Peak CPU % / 100) * 1.3)
```
- Uses peak CPU percentage
- Adds 30% buffer for bursts
- Rounded to nearest 0.25 cores
- Minimum: 0.5 cores

### Example Scenarios

| Peak Memory | Peak CPU | Recommended | ACI Cost (US East)* |
|-------------|----------|-------------|---------------------|
| 400 MB      | 45%      | 0.5 vCPU, 1GB | ~$0.0209/hour |
| 650 MB      | 75%      | 1 vCPU, 1.5GB | ~$0.0522/hour |
| 1200 MB     | 85%    | 2 vCPU, 2.5GB | ~$0.1044/hour |

*Pricing as of 2025 - check Azure pricing for current rates

## ?? Testing Scenarios

### Scenario 1: Light Load (Development/Testing)
```powershell
.\scripts\Run-LoadTest.ps1 -TotalRequests 50 -ConcurrentRequests 2
```
Simulates light usage with minimal concurrency.

### Scenario 2: Moderate Load (Production)
```powershell
.\scripts\Run-LoadTest.ps1 -TotalRequests 200 -ConcurrentRequests 5
```
Simulates typical production usage.

### Scenario 3: Heavy Load (Peak Traffic)
```powershell
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10
```
Stress test to determine maximum capacity.

### Scenario 4: Custom Workload
```powershell
$longText = "Your typical production text that represents real workload..." * 10
.\scripts\Run-LoadTest.ps1 -TestText $longText -TotalRequests 100
```
Test with your actual data.

## ?? Understanding Results

### Response Time Percentiles
- **P50 (Median)**: 50% of requests complete in this time or less
- **P95**: 95% of requests complete in this time or less
- **P99**: 99% of requests complete in this time or less

### Memory Patterns
- **Initial Spike**: Model loading (expected, one-time)
- **Stable Baseline**: Memory after model loaded
- **Request Spikes**: Temporary increases during inference
- **GC Cycles**: Gen 0/1 collections are normal, frequent Gen 2 indicates pressure

### CPU Patterns
- **Idle**: ~5-10% (background tasks)
- **Request Processing**: Spike to 50-100% during inference
- **Average**: Should stabilize below 70% under normal load

## ?? Troubleshooting

### High Memory Usage
**Symptom**: Memory exceeds 2GB
**Causes**:
- Large model file
- Many Gen 2 collections
- Memory leaks (check Gen 2 trend)

**Solutions**:
- Use smaller/quantized model
- Increase ACI memory allocation
- Force GC between requests in production

### High CPU Usage
**Symptom**: CPU consistently above 90%
**Causes**:
- Complex model
- Long input texts
- High concurrency

**Solutions**:
- Increase CPU cores
- Implement request rate limiting
- Consider horizontal scaling

### Slow Response Times
**Symptom**: P95 > 5 seconds
**Causes**:
- Model complexity
- CPU/memory constraints
- Request queuing

**Solutions**:
- Optimize model (quantization)
- Increase resources
- Add multiple container instances

## ?? Deployment Example

### Azure Container Instance (CLI)
```bash
az container create \
  --resource-group myResourceGroup \
  --name llm-embedding-api \
  --image myregistry.azurecr.io/llm-embedding-api:latest \
  --cpu 1 \
  --memory 1 \
  --ports 8080 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    Embedding__ModelPath=/models/model.onnx
```

### Docker Compose
```yaml
version: '3.8'
services:
  embedding-api:
  image: llm-embedding-api:latest
    ports:
      - "5000:8080"
    deploy:
      resources:
  limits:
          cpus: '1.0'
 memory: 1G
        reservations:
      cpus: '0.5'
          memory: 512M
```

## ?? Additional Resources

- [Performance Monitoring Guide](./PERFORMANCE_MONITORING.md) - Complete documentation
- [Quick Start Guide](./QUICK_START_PERFORMANCE.md) - Get started in 5 minutes
- [Azure Container Instances Pricing](https://azure.microsoft.com/pricing/details/container-instances/)
- [ONNX Runtime Performance](https://onnxruntime.ai/docs/performance/)

## ?? Best Practices

1. **Always load test** with representative data before production deployment
2. **Monitor for 15-30 minutes** to capture steady-state behavior
3. **Test multiple scenarios** (light, moderate, heavy load)
4. **Add safety buffers** - the auto-recommendations include 50% memory and 30% CPU buffers
5. **Monitor in production** using the `/api/metrics` endpoints
6. **Scale horizontally** for high throughput (multiple container instances)
7. **Use smaller models** when possible (quantized models reduce memory)

## ?? Example Workflow

```powershell
# 1. Start the API
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj

# 2. In another terminal, watch metrics
.\scripts\Watch-Performance.ps1

# 3. In a third terminal, run load test
.\scripts\Run-LoadTest.ps1 -TotalRequests 200 -ConcurrentRequests 5

# 4. Review results and adjust ACI configuration
# Results are saved to: performance-test-results-YYYYMMDD-HHMMSS.json

# 5. Deploy to Azure with recommended specs
az container create --cpu 1 --memory 1 ...
```

## ?? Support

- Check logs: The API logs performance metrics every 10 seconds
- View metrics: `GET /api/metrics/statistics`
- Review documentation: See `PERFORMANCE_MONITORING.md`
- Test configuration: Use scripts in `scripts/` directory

---

**Ready to start?** Run `.\scripts\Run-LoadTest.ps1` and get your Azure recommendations in minutes! ??
