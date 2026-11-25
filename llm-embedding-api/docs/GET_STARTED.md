# ? Performance Monitoring - Ready to Use!

## ?? What's Been Implemented

Your LLM Embedding API now has comprehensive performance monitoring to determine Azure Container Instance (ACI) requirements!

### Features Added:
- ? Real-time .NET performance counters (CPU, memory, GC)
- ? Automated Azure ACI recommendations
- ? Load testing scripts with detailed reporting
- ? Live performance dashboard
- ? Historical metrics tracking
- ? API endpoints for metrics access

## ?? Quick Start (3 Steps)

### Step 1: Start the API
```powershell
cd llm-embedding-api
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj
```

Wait for: `Now listening on: http://localhost:5000`

### Step 2: Verify Installation (New Terminal)
```powershell
.\scripts\Test-PerformanceMonitoring.ps1
```

This will verify all endpoints are working correctly.

### Step 3: Run Load Test (Get Azure Recommendations)
```powershell
.\scripts\Run-LoadTest.ps1
```

**You'll see output like:**
```
====================================
Azure Container Instance Recommendations
====================================

Recommended Configuration:
  CPU Cores: 1 vCPU
  Memory:     1 GB

Recommendation: Recommended ACI SKU: 1 vCPU, 1GB RAM
```

## ?? Available Tools

### 1. Performance Test Script
```powershell
.\scripts\Test-PerformanceMonitoring.ps1
```
Verifies all monitoring endpoints are working.

### 2. Load Test Script
```powershell
# Basic test (100 requests, 5 concurrent)
.\scripts\Run-LoadTest.ps1

# Heavy test (500 requests, 10 concurrent)
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10

# Custom text
.\scripts\Run-LoadTest.ps1 -TestText "Your production text here"
```

### 3. Live Dashboard
```powershell
.\scripts\Watch-Performance.ps1
```
Real-time monitoring with visual progress bars.

### 4. API Endpoints
```powershell
# Current metrics
Invoke-RestMethod http://localhost:5000/api/metrics/current

# Statistics with Azure recommendations
Invoke-RestMethod http://localhost:5000/api/metrics/statistics

# Historical snapshots
Invoke-RestMethod http://localhost:5000/api/metrics/snapshots?limit=50

# Force garbage collection
Invoke-RestMethod -Method Post http://localhost:5000/api/metrics/gc
```

## ?? What Gets Measured

### System Metrics
- **CPU Usage**: Current, peak, average percentage
- **Memory Usage**: Working set, private memory, GC heap
- **Garbage Collection**: Gen 0/1/2 collection counts
- **Thread Count**: Active threads
- **Handle Count**: OS handles

### Request Metrics
- **Duration**: Time to generate embeddings
- **Input Length**: Characters in input text
- **Throughput**: Requests per second
- **Error Rate**: Failed requests

### Azure Recommendations
- **Minimum CPU Cores**: Based on peak with 30% buffer
- **Minimum Memory**: Based on peak with 50% buffer
- **Recommended SKU**: Complete ACI configuration

## ?? Example Workflow

### For Testing (Without Model)
```powershell
# Terminal 1: Start API
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj

# Terminal 2: Verify installation
.\scripts\Test-PerformanceMonitoring.ps1

# Terminal 3: Watch metrics
.\scripts\Watch-Performance.ps1
```

### For Production Sizing (With Model)
```powershell
# Terminal 1: Start API with model
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj

# Terminal 2: Run comprehensive load test
.\scripts\Run-LoadTest.ps1 -TotalRequests 500 -ConcurrentRequests 10

# Review Azure recommendations in output
# Results saved to: performance-test-results-YYYYMMDD-HHMMSS.json
```

## ?? Files Created

### Services (C#)
- `src/LlmEmbeddingApi/Services/PerformanceMetricsService.cs` - Core metrics collection
- `src/LlmEmbeddingApi/Services/PerformanceCollectorService.cs` - Background collector
- `src/LlmEmbeddingApi/Middleware/PerformanceMonitoringMiddleware.cs` - Request tracking

### Scripts (PowerShell)
- `scripts/Test-PerformanceMonitoring.ps1` - Verify installation
- `scripts/Run-LoadTest.ps1` - Automated load testing
- `scripts/Watch-Performance.ps1` - Live dashboard

### Documentation
- `docs/QUICK_REFERENCE.md` - One-page reference
- `docs/QUICK_START_PERFORMANCE.md` - 5-minute guide
- `docs/PERFORMANCE_MONITORING.md` - Complete reference
- `docs/README_PERFORMANCE.md` - Overview and examples
- `docs/IMPLEMENTATION_SUMMARY.md` - Technical details
- `docs/GET_STARTED.md` - This file

## ?? Understanding Results

### Memory Calculation
```
Peak Memory: 648 MB
Buffer: 50% (safety margin)
Calculation: CEIL((648 / 1024) * 1.5) = 1.0 GB
Result: Allocate 1 GB for ACI
```

### CPU Calculation
```
Peak CPU: 78%
Buffer: 30% (burst capacity)
Calculation: CEIL(0.78 * 1.3) = 1.0 cores
Result: Allocate 1 vCPU for ACI
```

## ?? Troubleshooting

### "API is not accessible"
**Solution**: Ensure API is running on http://localhost:5000
```powershell
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj
```

### "Model not loaded" (Optional)
**Note**: Performance monitoring works WITHOUT a model loaded!
- Tests 1-4, 6-7 will pass
- Test 5 (embedding generation) will be skipped

**To load a model:**
1. Update `appsettings.json` with model path
2. Set `AutoDownload: true` for automatic download
3. Or manually place ONNX model at specified path

### High Memory Usage
**Note**: This is normal for ML models!
- Small models: ~500 MB
- Medium models: ~1 GB
- Large models: ~2 GB
- Follow Azure recommendations for sizing

### Script Execution Policy Error
```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

## ?? Deploying to Azure

### Using Recommended Configuration
```bash
# Get recommendations first
curl http://localhost:5000/api/metrics/statistics

# Deploy with recommended specs (example: 1 vCPU, 1 GB)
az container create \
  --resource-group myResourceGroup \
  --name llm-embedding-api \
  --image myregistry.azurecr.io/llm-embedding-api:latest \
  --cpu 1 \
  --memory 1 \
  --ports 8080 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production
```

### Monitor Production
```bash
# View logs
az container logs --resource-group myResourceGroup \
  --name llm-embedding-api --follow

# Access metrics endpoint
curl https://your-container.azurecontainer.io/api/metrics/current
```

## ?? Documentation

| Document | Purpose |
|----------|---------|
| **QUICK_REFERENCE.md** | One-page cheat sheet |
| **QUICK_START_PERFORMANCE.md** | Get started in 5 minutes |
| **PERFORMANCE_MONITORING.md** | Complete reference (4000+ words) |
| **README_PERFORMANCE.md** | Overview with examples |
| **IMPLEMENTATION_SUMMARY.md** | Technical implementation details |
| **GET_STARTED.md** | This file - start here! |

## ? Key Benefits

1. **No Guesswork**: Data-driven Azure sizing recommendations
2. **Cost Optimization**: Right-size containers, avoid over-provisioning
3. **Easy Testing**: Automated scripts for consistent results
4. **Real-Time Monitoring**: Live dashboard shows current performance
5. **Production Ready**: Follows .NET best practices
6. **Export Results**: JSON files for historical comparison

## ?? Next Actions

1. ? **Verify Installation**: Run `.\scripts\Test-PerformanceMonitoring.ps1`
2. ?? **Watch Metrics**: Run `.\scripts\Watch-Performance.ps1`
3. ?? **Run Load Test**: Run `.\scripts\Run-LoadTest.ps1`
4. ?? **Review Docs**: Read `docs/QUICK_START_PERFORMANCE.md`
5. ?? **Deploy**: Use Azure recommendations for ACI configuration

## ?? Pro Tips

- Run tests for 15-30 minutes for accurate results
- Test with your actual production text
- Try multiple load scenarios (light, medium, heavy)
- Monitor production using `/api/metrics/statistics`
- Results include safety buffers (50% memory, 30% CPU)

## ?? Need Help?

1. Check API logs in the terminal
2. Review `docs/QUICK_START_PERFORMANCE.md`
3. Run `.\scripts\Test-PerformanceMonitoring.ps1` to verify setup
4. Check endpoint: `http://localhost:5000/api/metrics/current`

---

## ? Status: Ready to Use!

**Everything is set up and ready to go!**

Start with:
```powershell
# Terminal 1
dotnet run --project src\LlmEmbeddingApi\LlmEmbeddingApi.csproj

# Terminal 2
.\scripts\Test-PerformanceMonitoring.ps1
```

?? **You'll have Azure Container Instance recommendations in minutes!**
