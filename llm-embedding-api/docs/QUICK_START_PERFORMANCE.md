# Quick Start: Performance Testing

This guide will help you quickly determine CPU and RAM requirements for Azure Container Instance.

## Step 1: Start the Application

```powershell
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj
```

Wait for the message: `Application started. Press Ctrl+C to shut down.`

## Step 2: Run Load Test

Open a new terminal and run:

```powershell
# Basic test (100 requests, 5 concurrent)
.\scripts\Run-LoadTest.ps1

# Intensive test (500 requests, 10 concurrent)
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10

# Custom text test
.\scripts\Run-LoadTest.ps1 -TestText "Your custom text here that represents your typical workload"
```

## Step 3: View Results

The script will automatically display:
- ? Request statistics (success rate, throughput)
- ? Response times (average, percentiles)
- ? CPU and memory usage (current, peak, average)
- ? **Azure Container Instance recommendations**

### Example Output:

```
====================================
Azure Container Instance Recommendations
====================================

Based on Peak Usage:
  Peak Memory:  648.25 MB
  Peak CPU:    78.50%

Recommended Configuration:
  CPU Cores:         1 vCPU
  Memory:     1 GB
  Buffers Applied:   CPU 30%, Memory 50%

Recommendation: Recommended ACI SKU: 1 vCPU, 1GB RAM
```

## Alternative: Manual Testing

### Get Real-Time Metrics

```powershell
# Watch metrics update every 2 seconds
$url = "http://localhost:5000/api/metrics/current"
while ($true) { 
    Clear-Host
    Invoke-RestMethod $url | ConvertTo-Json
    Start-Sleep 2
}
```

### Get Statistics and Recommendations

```powershell
Invoke-RestMethod http://localhost:5000/api/metrics/statistics | ConvertTo-Json -Depth 10
```

### Make Test Requests

```powershell
$body = @{ text = "Test text for embedding" } | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5000/api/embed -Method Post -Body $body -ContentType "application/json"
```

## Understanding the Results

### Memory Recommendation
- **Peak Memory**: Highest memory usage during test
- **50% Buffer**: Added for safety (model loading, GC, spikes)
- **Example**: 648 MB peak ? recommend 1 GB (648 * 1.5 / 1024 ? 0.95, rounded to 1)

### CPU Recommendation
- **Peak CPU**: Highest CPU usage during test
- **30% Buffer**: Added for burst capacity
- **Example**: 78% peak ? recommend 1 core (0.78 * 1.3 ? 1.01, rounded to 1)

### Typical Requirements

| Model Size | Memory | CPU | Recommended ACI |
|------------|--------|-----|-----------------|
| Small   | ~0.5GB | 0.5 | 0.5 vCPU, 1GB   |
| Medium   | ~1GB   | 1.0 | 1 vCPU, 2GB     |
| Large      | ~2GB   | 2.0 | 2 vCPU, 4GB     |

## Next Steps

1. **Run multiple test scenarios**:
   - Different text lengths
   - Different concurrency levels
   - Different request rates

2. **Document your findings**:
   - Results are auto-saved as JSON files
   - Compare different test runs

3. **Deploy to Azure**:
   ```bash
   az container create \
     --resource-group myResourceGroup \
     --name llm-embedding-api \
     --image your-registry/llm-embedding-api:latest \
     --cpu 1 \
     --memory 1 \
     --ports 8080
   ```

4. **Monitor production**:
   - Use `/api/metrics/current` endpoint
   - Set up Azure Monitor alerts
- Adjust resources based on real usage

## Troubleshooting

**Issue**: Script fails with "API is not accessible"
- **Solution**: Ensure the API is running on http://localhost:5000

**Issue**: All requests fail
- **Solution**: Check model is loaded correctly, view logs

**Issue**: Very high memory usage
- **Solution**: This is normal for ML models, ensure recommendations are followed

**Issue**: Load test is too slow
- **Solution**: Reduce TotalRequests or increase ConcurrentRequests

## Support

For issues or questions:
1. Check logs in the terminal running the API
2. View detailed metrics: `GET /api/metrics/statistics`
3. Review the full documentation: `docs/PERFORMANCE_MONITORING.md`
