using System.Diagnostics;
using System.Runtime;
using LlmEmbeddingApi.Services;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Background service that periodically collects and logs performance metrics
/// </summary>
public class PerformanceCollectorService : BackgroundService
{
    private readonly ILogger<PerformanceCollectorService> _logger;
    private readonly PerformanceMetricsService _metricsService;
    private readonly TimeSpan _collectionInterval;
    private readonly List<PerformanceSnapshot> _snapshots = new();
  private readonly int _maxSnapshots = 1000; // Keep last 1000 snapshots

    public PerformanceCollectorService(
     ILogger<PerformanceCollectorService> logger,
        PerformanceMetricsService metricsService)
    {
     _logger = logger;
        _metricsService = metricsService;
        _collectionInterval = TimeSpan.FromSeconds(10); // Collect every 10 seconds
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Performance collector started. Collection interval: {Interval}s", 
 _collectionInterval.TotalSeconds);

        // Log initial system info
        LogSystemInfo();

     while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
        var snapshot = _metricsService.GetSnapshot();
      
     // Store snapshot
          lock (_snapshots)
     {
              _snapshots.Add(snapshot);
     if (_snapshots.Count > _maxSnapshots)
    {
    _snapshots.RemoveAt(0);
         }
      }

           // Log current metrics
     _logger.LogInformation(
         "Performance Metrics - CPU: {Cpu}%, Memory: {Memory}MB (Peak: {PeakMemory}MB), " +
"GC Heap: {GCHeap}MB, Threads: {Threads}, GC Collections: [G0:{G0}, G1:{G1}, G2:{G2}]",
       snapshot.CpuUsagePercent,
     snapshot.WorkingSetMB.ToString("F2"),
             snapshot.PeakWorkingSetMB.ToString("F2"),
         snapshot.GCHeapSizeMB.ToString("F2"),
    snapshot.ThreadCount,
     snapshot.Gen0Collections,
        snapshot.Gen1Collections,
   snapshot.Gen2Collections);

  await Task.Delay(_collectionInterval, stoppingToken);
            }
      catch (Exception ex) when (ex is not OperationCanceledException)
     {
       _logger.LogError(ex, "Error collecting performance metrics");
          await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
  }

        // Log final summary
        LogFinalSummary();
    }

    private void LogSystemInfo()
    {
  var process = Process.GetCurrentProcess();
        
        _logger.LogInformation(
        "System Information - " +
            "Processor Count: {ProcessorCount}, " +
            "OS: {OS}, " +
         "64-bit Process: {Is64Bit}, " +
    "GC Server Mode: {GCServerMode}, " +
          "GC Concurrent: {GCConcurrent}",
            Environment.ProcessorCount,
            Environment.OSVersion,
       Environment.Is64BitProcess,
            GCSettings.IsServerGC,
            GCSettings.LatencyMode);
    }

    private void LogFinalSummary()
    {
        if (_snapshots.Count == 0)
 {
            _logger.LogInformation("No performance data collected");
     return;
    }

        lock (_snapshots)
        {
 var avgCpu = _snapshots.Average(s => s.CpuUsagePercent);
  var maxCpu = _snapshots.Max(s => s.CpuUsagePercent);
      var avgMemory = _snapshots.Average(s => s.WorkingSetMB);
    var maxMemory = _snapshots.Max(s => s.WorkingSetMB);
            var peakMemory = _snapshots.Max(s => s.PeakWorkingSetMB);
            
    _logger.LogInformation(
       "Performance Summary - " +
  "Avg CPU: {AvgCpu}%, Max CPU: {MaxCpu}%, " +
            "Avg Memory: {AvgMemory}MB, Max Memory: {MaxMemory}MB, Peak Memory: {PeakMemory}MB, " +
  "Total Snapshots: {Snapshots}",
      avgCpu.ToString("F2"),
      maxCpu.ToString("F2"),
             avgMemory.ToString("F2"),
        maxMemory.ToString("F2"),
    peakMemory.ToString("F2"),
    _snapshots.Count);

 // Azure Container Instance recommendations
  var recommendedCpu = Math.Ceiling(maxCpu / 100.0); // CPU cores
var recommendedMemoryGB = Math.Ceiling(peakMemory / 1024.0 * 1.5); // Add 50% buffer
            
            _logger.LogInformation(
     "Azure Container Instance Recommendations - " +
                "Minimum CPU Cores: {Cpu}, " +
    "Minimum Memory: {Memory}GB " +
        "(Based on peak usage with 50% buffer)",
           recommendedCpu,
       recommendedMemoryGB);
   }
    }

    /// <summary>
    /// Gets the collected performance snapshots
    /// </summary>
  public IReadOnlyList<PerformanceSnapshot> GetSnapshots()
    {
     lock (_snapshots)
      {
         return _snapshots.ToList();
        }
    }

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    public PerformanceStatistics GetStatistics()
    {
        lock (_snapshots)
        {
      if (_snapshots.Count == 0)
            {
 return new PerformanceStatistics();
            }

     return new PerformanceStatistics
     {
    SampleCount = _snapshots.Count,
       CollectionDurationMinutes = (_snapshots[^1].Timestamp - _snapshots[0].Timestamp).TotalMinutes,
        
        CpuUsagePercent = new MetricStats
    {
         Average = _snapshots.Average(s => s.CpuUsagePercent),
                Min = _snapshots.Min(s => s.CpuUsagePercent),
          Max = _snapshots.Max(s => s.CpuUsagePercent),
        Current = _snapshots[^1].CpuUsagePercent,
          Peak = _snapshots.Max(s => s.PeakCpuPercent)
    },
       
                WorkingSetMB = new MetricStats
     {
       Average = _snapshots.Average(s => s.WorkingSetMB),
         Min = _snapshots.Min(s => s.WorkingSetMB),
     Max = _snapshots.Max(s => s.WorkingSetMB),
        Current = _snapshots[^1].WorkingSetMB,
                Peak = _snapshots.Max(s => s.PeakWorkingSetMB)
       },
      
             PrivateMemoryMB = new MetricStats
     {
         Average = _snapshots.Average(s => s.PrivateMemoryMB),
 Min = _snapshots.Min(s => s.PrivateMemoryMB),
              Max = _snapshots.Max(s => s.PrivateMemoryMB),
    Current = _snapshots[^1].PrivateMemoryMB,
     Peak = _snapshots.Max(s => s.PeakPrivateMemoryMB)
       },
     
       GCHeapSizeMB = new MetricStats
         {
        Average = _snapshots.Average(s => s.GCHeapSizeMB),
    Min = _snapshots.Min(s => s.GCHeapSizeMB),
         Max = _snapshots.Max(s => s.GCHeapSizeMB),
  Current = _snapshots[^1].GCHeapSizeMB
    },
    
       ThreadCount = new MetricStats
            {
   Average = _snapshots.Average(s => s.ThreadCount),
        Min = _snapshots.Min(s => s.ThreadCount),
       Max = _snapshots.Max(s => s.ThreadCount),
   Current = _snapshots[^1].ThreadCount
  },
        
    TotalGen0Collections = _snapshots[^1].Gen0Collections,
        TotalGen1Collections = _snapshots[^1].Gen1Collections,
       TotalGen2Collections = _snapshots[^1].Gen2Collections,
          
        AzureRecommendations = CalculateAzureRecommendations()
   };
        }
    }

  private AzureContainerRecommendations CalculateAzureRecommendations()
    {
        if (_snapshots.Count == 0)
        {
return new AzureContainerRecommendations();
        }

      var peakMemoryMB = _snapshots.Max(s => s.PeakWorkingSetMB);
        var maxCpuPercent = _snapshots.Max(s => s.CpuUsagePercent);
        var avgCpuPercent = _snapshots.Average(s => s.CpuUsagePercent);

        // Calculate recommendations with buffers
        var memoryBufferMultiplier = 1.5; // 50% buffer
        var cpuBufferMultiplier = 1.3; // 30% buffer

    var recommendedMemoryGB = Math.Ceiling(peakMemoryMB / 1024.0 * memoryBufferMultiplier * 2) / 2; // Round to nearest 0.5GB
     var recommendedCpuCores = Math.Ceiling((maxCpuPercent / 100.0) * cpuBufferMultiplier * 4) / 4; // Round to nearest 0.25 cores

        // Ensure minimums
    recommendedMemoryGB = Math.Max(0.5, recommendedMemoryGB);
        recommendedCpuCores = Math.Max(0.5, recommendedCpuCores);

        return new AzureContainerRecommendations
    {
            MinimumMemoryGB = recommendedMemoryGB,
            MinimumCpuCores = recommendedCpuCores,
       PeakMemoryMB = peakMemoryMB,
 PeakCpuPercent = maxCpuPercent,
      AverageCpuPercent = avgCpuPercent,
            MemoryBuffer = $"{(memoryBufferMultiplier - 1) * 100}%",
            CpuBuffer = $"{(cpuBufferMultiplier - 1) * 100}%",
       Recommendation = $"Recommended ACI SKU: {recommendedCpuCores} vCPU, {recommendedMemoryGB}GB RAM"
        };
 }
}

/// <summary>
/// Performance statistics summary
/// </summary>
public class PerformanceStatistics
{
    public int SampleCount { get; set; }
    public double CollectionDurationMinutes { get; set; }
    public MetricStats CpuUsagePercent { get; set; } = new();
    public MetricStats WorkingSetMB { get; set; } = new();
    public MetricStats PrivateMemoryMB { get; set; } = new();
    public MetricStats GCHeapSizeMB { get; set; } = new();
    public MetricStats ThreadCount { get; set; } = new();
    public int TotalGen0Collections { get; set; }
    public int TotalGen1Collections { get; set; }
    public int TotalGen2Collections { get; set; }
 public AzureContainerRecommendations AzureRecommendations { get; set; } = new();
}

/// <summary>
/// Statistics for a single metric
/// </summary>
public class MetricStats
{
    public double Average { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Current { get; set; }
public double Peak { get; set; }
}

/// <summary>
/// Azure Container Instance recommendations
/// </summary>
public class AzureContainerRecommendations
{
    public double MinimumMemoryGB { get; set; }
    public double MinimumCpuCores { get; set; }
    public double PeakMemoryMB { get; set; }
    public double PeakCpuPercent { get; set; }
    public double AverageCpuPercent { get; set; }
    public string MemoryBuffer { get; set; } = string.Empty;
    public string CpuBuffer { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
