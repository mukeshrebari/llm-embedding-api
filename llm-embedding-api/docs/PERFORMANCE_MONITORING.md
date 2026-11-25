# Performance Monitoring Guide

This guide explains how to use the performance monitoring features to determine Azure Container Instance (ACI) requirements for the LLM Embedding API.

## Overview

The application includes comprehensive performance monitoring capabilities:
- **Real-time metrics**: CPU usage, memory consumption, GC statistics
- **Historical tracking**: Performance snapshots collected every 10 seconds
- **Azure recommendations**: Automated calculation of minimum ACI requirements
- **Request tracking**: Per-request performance metrics

## Quick Start

### 1. Start the Application

```bash
dotnet run --project src/LlmEmbeddingApi/LlmEmbeddingApi.csproj
```

### 2. Access Performance Endpoints

#### Get Current Metrics
```bash
curl http://localhost:5000/api/metrics/current
```

**Response Example:**
```json
{
  "timestamp": "2025-01-31T10:30:00Z",
  "cpuUsagePercent": 45.2,
  "workingSetMB": 512.5,
  "privateMemoryMB": 480.3,
  "gcHeapSizeMB": 256.7,
  "gen0Collections": 12,
  "gen1Collections": 5,
  "gen2Collections": 2,
  "threadCount": 24,
  "handleCount": 456,
  "peakWorkingSetMB": 648.2,
  "peakPrivateMemoryMB": 612.1,
  "peakCpuPercent": 78.5
}
```

#### Get Performance Statistics with Azure Recommendations
```bash
curl http://localhost:5000/api/metrics/statistics
```

**Response Example:**
```json
{
  "sampleCount": 150,
  "collectionDurationMinutes": 25.0,
  "cpuUsagePercent": {
    "average": 35.2,
    "min": 5.0,
    "max": 78.5,
    "current": 42.1,
    "peak": 78.5
  },
  "workingSetMB": {
    "average": 485.3,
    "min": 420.0,
    "max": 648.2,
    "current": 512.5,
    "peak": 648.2
  },
  "azureRecommendations": {
    "minimumMemoryGB": 1.0,
    "minimumCpuCores": 1.0,
    "peakMemoryMB": 648.2,
    "peakCpuPercent": 78.5,
    "averageCpuPercent": 35.2,
    "memoryBuffer": "50%",
    "cpuBuffer": "30%",
    "recommendation": "Recommended ACI SKU: 1 vCPU, 1GB RAM"
  }
}
```

#### Get Historical Snapshots
```bash
# Get all snapshots
curl http://localhost:5000/api/metrics/snapshots

# Get last 50 snapshots
curl http://localhost:5000/api/metrics/snapshots?limit=50
```

## Load Testing Procedure

To accurately determine ACI requirements, perform load testing:

### 1. Prepare Test Data

Create a file with sample texts representing your typical workload:

```json
{
  "texts": [
    "Short text for embedding",
    "A longer paragraph with multiple sentences that represents typical usage...",
    "Another sample text with different length and complexity..."
  ]
}
```

### 2. Run Load Test Script

```bash
# Install hey (load testing tool)
# Windows: scoop install hey
# Linux: go install github.com/rakyll/hey@latest
# Mac: brew install hey

# Run load test (10 concurrent requests, 1000 total requests)
hey -n 1000 -c 10 -m POST \
  -H "Content-Type: application/json" \
  -d '{"text":"Your sample text here for embedding generation"}' \
  http://localhost:5000/api/embed
```

### 3. Monitor During Load Test

Open another terminal and monitor metrics in real-time:

```bash
# Watch current metrics (updates every 2 seconds)
watch -n 2 curl -s http://localhost:5000/api/metrics/current | jq
```

### 4. Collect Results After Load Test

```bash
# Get final statistics and recommendations
curl http://localhost:5000/api/metrics/statistics | jq
```

## Understanding Azure Recommendations

The system automatically calculates ACI requirements with safety buffers:

### Memory Recommendations
- **Peak Memory**: Maximum working set observed during testing
- **Buffer**: 50% added to peak for safety margin
- **Calculation**: `ceil((PeakMemoryMB / 1024) * 1.5)`
- **Minimum**: 0.5 GB

### CPU Recommendations
- **Peak CPU**: Maximum CPU percentage observed
- **Buffer**: 30% added to peak for burst capacity
- **Calculation**: `ceil((MaxCpuPercent / 100) * 1.3)`
- **Minimum**: 0.5 cores

### Example Calculation

If your testing shows:
- Peak Memory: 648 MB
- Peak CPU: 78%

Recommendations:
- Memory: `ceil((648 / 1024) * 1.5) = 1.0 GB`
- CPU: `ceil(0.78 * 1.3) = 1.0 cores`

**Result**: Use ACI with **1 vCPU and 1 GB RAM**

## Azure Container Instance SKUs

Common ACI configurations (as of 2025):

| vCPU | Memory | Use Case |
|------|--------|----------|
| 0.5  | 0.5 GB | Very light workloads |
| 1.0  | 1.0 GB | Light workloads, small models |
| 1.0  | 2.0 GB | Medium workloads |
| 2.0  | 4.0 GB | Heavy workloads, large models |
| 4.0  | 8.0 GB | Very heavy workloads |

**Pricing Note**: ACI pricing is per-second based on vCPU and memory allocated.

## Monitoring in Production

### Log Analysis

The application logs performance metrics every 10 seconds:

```
[INFO] Performance Metrics - CPU: 45.2%, Memory: 512.50MB (Peak: 648.20MB), 
GC Heap: 256.70MB, Threads: 24, GC Collections: [G0:12, G1:5, G2:2]
```

Monitor these logs in Azure Container Instances:

```bash
# Azure CLI
az container logs --resource-group myResourceGroup --name myContainer --follow
```

### Application Insights Integration

For production monitoring, integrate with Application Insights:

```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

Add to `Program.cs`:
```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

### Health Checks

The application includes health checks:

```bash
curl http://localhost:5000/health
```

## Performance Optimization Tips

### 1. Model Loading
- Model loads once at startup (singleton service)
- Initial memory spike is normal and expected
- Peak memory during model load is typically highest

### 2. Garbage Collection
- Monitor Gen2 collections (expensive)
- Frequent Gen2 = potential memory pressure
- Force GC to test memory after cleanup:
  ```bash
  curl -X POST http://localhost:5000/api/metrics/gc
  ```

### 3. Request Batching
- Single large request vs multiple small requests
- Test both scenarios to determine optimal configuration

### 4. Concurrency
- ONNX inference uses `SemaphoreSlim` for thread safety
- One inference at a time (by design)
- High concurrency = request queuing

## Troubleshooting

### High Memory Usage
- Check model size: larger models require more memory
- Monitor GC pressure via Gen2 collections
- Consider increasing memory allocation

### High CPU Usage
- CPU usage depends on model complexity and input length
- ONNX Runtime uses CPU optimizations automatically
- Consider multi-core allocation for better performance

### Request Latency
- Check embedding duration in metrics
- Monitor queue time if concurrent requests are high
- Consider horizontal scaling (multiple instances) for high throughput

## Sample Deployment Configuration

### Azure Container Instance (ARM Template)

```json
{
  "type": "Microsoft.ContainerInstance/containerGroups",
  "apiVersion": "2023-05-01",
  "properties": {
    "containers": [{
  "name": "llm-embedding-api",
      "properties": {
        "image": "your-registry/llm-embedding-api:latest",
        "resources": {
    "requests": {
            "cpu": 1.0,
            "memoryInGb": 1.0
          }
        },
    "ports": [{ "port": 8080 }]
      }
    }],
    "osType": "Linux",
    "restartPolicy": "Always"
  }
}
```

### Docker Compose

```yaml
version: '3.8'
services:
  embedding-api:
    image: llm-embedding-api:latest
  build: .
    ports:
  - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Embedding__ModelPath=/models/model.onnx
      - Embedding__AutoDownload=true
    deploy:
   resources:
        limits:
          cpus: '1.0'
 memory: 1G
        reservations:
   cpus: '0.5'
     memory: 512M
```

## Summary Checklist

- [ ] Run application with representative workload
- [ ] Collect metrics for at least 15-30 minutes
- [ ] Perform load testing with concurrent requests
- [ ] Review `/api/metrics/statistics` for recommendations
- [ ] Add 50% memory buffer for safety
- [ ] Add 30% CPU buffer for burst capacity
- [ ] Test deployment in Azure with recommended SKU
- [ ] Monitor production metrics and adjust as needed

## Additional Resources

- [Azure Container Instances Pricing](https://azure.microsoft.com/pricing/details/container-instances/)
- [ONNX Runtime Performance Tuning](https://onnxruntime.ai/docs/performance/)
- [.NET Performance Best Practices](https://learn.microsoft.com/dotnet/core/diagnostics/performance-tips)
