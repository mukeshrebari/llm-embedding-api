# ?? Quick Reference Card - Performance Monitoring

## Start Testing (3 Commands)

```powershell
# 1. Start the API
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj

# 2. Run load test (new terminal)
.\scripts\Run-LoadTest.ps1

# 3. View results (automatic)
# Azure recommendations will be displayed!
```

## Key Endpoints

```bash
GET  /api/metrics/current      # Real-time metrics
GET  /api/metrics/statistics   # Azure recommendations
GET  /api/metrics/snapshots    # Historical data
POST /api/metrics/gc        # Force garbage collection
```

## PowerShell Scripts

```powershell
# Basic load test
.\scripts\Run-LoadTest.ps1

# Heavy load test
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10

# Real-time dashboard
.\scripts\Watch-Performance.ps1

# Custom API URL
.\scripts\Run-LoadTest.ps1 -BaseUrl "http://your-api:5000"
```

## Quick API Tests

```powershell
# Current metrics
Invoke-RestMethod http://localhost:5000/api/metrics/current | ConvertTo-Json

# Get Azure recommendations
Invoke-RestMethod http://localhost:5000/api/metrics/statistics | ConvertTo-Json

# Test embedding
$body = @{ text = "Test text" } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5000/api/embed -Method Post -Body $body -ContentType "application/json"
```

## Interpreting Results

### Memory Recommendation
```
Peak Memory: 648 MB
Calculation: CEIL((648 / 1024) * 1.5) = 1 GB
Result: Use 1 GB memory
```

### CPU Recommendation
```
Peak CPU: 78%
Calculation: CEIL(0.78 * 1.3) = 1.0 cores
Result: Use 1 vCPU
```

## Common ACI Configurations

| Workload | vCPU | Memory | Use Case |
|----------|------|--------|----------|
| Light    | 0.5  | 1 GB   | Dev/Testing |
| Medium   | 1.0  | 1-2 GB | Production |
| Heavy    | 2.0  | 4 GB   | High Load |

## Deploy to Azure

```bash
# Using Azure CLI
az container create \
  --resource-group myResourceGroup \
  --name llm-embedding-api \
  --image myregistry.azurecr.io/llm-embedding-api:latest \
  --cpu 1 \
  --memory 1 \
  --ports 8080

# View logs
az container logs --resource-group myResourceGroup --name llm-embedding-api --follow
```

## Metrics Collected

- ? CPU Usage (%, peak, average)
- ? Memory (working set, private, GC heap)
- ? Garbage Collection (Gen 0/1/2)
- ? Request Performance (duration, input length)
- ? Thread/Handle Count
- ? Error Rate

## Troubleshooting

| Issue | Solution |
|-------|----------|
| API not accessible | Check if running on localhost:5000 |
| High memory usage | Normal for ML models, use recommendations |
| Slow responses | Check CPU usage, consider more cores |
| Tests fail | Ensure model is loaded, check logs |

## Documentation

- ?? **Full Guide**: `docs/PERFORMANCE_MONITORING.md`
- ?? **Quick Start**: `docs/QUICK_START_PERFORMANCE.md`
- ?? **Overview**: `docs/README_PERFORMANCE.md`
- ?? **Implementation**: `docs/IMPLEMENTATION_SUMMARY.md`

## Example Output

```
====================================
Azure Container Instance Recommendations
====================================

Recommended Configuration:
  CPU Cores:      1 vCPU
  Memory: 1 GB

Recommendation: Recommended ACI SKU: 1 vCPU, 1GB RAM
```

## Best Practices

1. ? Run tests for 15-30 minutes
2. ? Test with realistic data
3. ? Test multiple load scenarios
4. ? Add safety buffers (included in recommendations)
5. ? Monitor production metrics
6. ? Adjust based on real usage

---

**Need Help?** 
- Check logs in the API terminal
- Review `docs/QUICK_START_PERFORMANCE.md`
- Use `.\scripts\Watch-Performance.ps1` for live monitoring
