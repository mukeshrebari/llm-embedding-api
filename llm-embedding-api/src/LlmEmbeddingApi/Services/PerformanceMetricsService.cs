using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Service for tracking application performance metrics
/// </summary>
public sealed class PerformanceMetricsService : IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _embeddingRequestCounter;
  private readonly Histogram<double> _embeddingDurationHistogram;
    private readonly Histogram<long> _embeddingInputLengthHistogram;
  private readonly Histogram<long> _memoryUsageHistogram;
    private readonly Counter<long> _errorCounter;
    private readonly ILogger<PerformanceMetricsService> _logger;

    // Process-level metrics
    private readonly Process _currentProcess;
    private DateTime _lastMetricsUpdate = DateTime.UtcNow;
    
    // Peak tracking
    private long _peakWorkingSetBytes;
    private long _peakPrivateMemoryBytes;
 private double _peakCpuPercent;

    public PerformanceMetricsService(ILogger<PerformanceMetricsService> logger)
    {
    _logger = logger;
     _currentProcess = Process.GetCurrentProcess();
        
        _meter = new Meter("LlmEmbeddingApi.Performance", "1.0");
 
        // Counters
        _embeddingRequestCounter = _meter.CreateCounter<long>(
     "embedding.requests",
     description: "Total number of embedding requests");
        
        _errorCounter = _meter.CreateCounter<long>(
      "embedding.errors",
       description: "Total number of errors");

        // Histograms
   _embeddingDurationHistogram = _meter.CreateHistogram<double>(
     "embedding.duration",
  unit: "ms",
          description: "Duration of embedding generation in milliseconds");
    
  _embeddingInputLengthHistogram = _meter.CreateHistogram<long>(
            "embedding.input_length",
      unit: "characters",
            description: "Length of input text in characters");
        
        _memoryUsageHistogram = _meter.CreateHistogram<long>(
    "process.memory_usage",
       unit: "MB",
        description: "Process memory usage in megabytes");

        // Observable gauges for system metrics
    _meter.CreateObservableGauge(
         "process.cpu_usage",
            () => GetCpuUsagePercent(),
       unit: "%",
            description: "Current CPU usage percentage");

        _meter.CreateObservableGauge(
            "process.working_set",
  () => GetWorkingSetMB(),
 unit: "MB",
            description: "Current working set memory in MB");

    _meter.CreateObservableGauge(
          "process.private_memory",
      () => GetPrivateMemoryMB(),
   unit: "MB",
            description: "Current private memory in MB");

      _meter.CreateObservableGauge(
        "process.gc_heap_size",
          () => GC.GetTotalMemory(false) / 1024.0 / 1024.0,
 unit: "MB",
         description: "Current GC heap size in MB");

        _meter.CreateObservableCounter(
   "process.gc_gen0_collections",
       () => GC.CollectionCount(0),
      description: "Total Gen 0 GC collections");

      _meter.CreateObservableCounter(
          "process.gc_gen1_collections",
   () => GC.CollectionCount(1),
      description: "Total Gen 1 GC collections");

        _meter.CreateObservableCounter(
   "process.gc_gen2_collections",
            () => GC.CollectionCount(2),
     description: "Total Gen 2 GC collections");

      _meter.CreateObservableGauge(
            "process.thread_count",
         () => _currentProcess.Threads.Count,
        description: "Current thread count");

        _meter.CreateObservableGauge(
            "process.handle_count",
     () => _currentProcess.HandleCount,
   description: "Current handle count");
    }

    /// <summary>
    /// Records an embedding request with its metrics
    /// </summary>
    public void RecordEmbeddingRequest(double durationMs, int inputLength, bool isError = false)
    {
        _embeddingRequestCounter.Add(1);
        _embeddingDurationHistogram.Record(durationMs);
        _embeddingInputLengthHistogram.Record(inputLength);
        
        // Record memory usage at request time
        var memoryMB = GetWorkingSetMB();
        _memoryUsageHistogram.Record((long)memoryMB);

 if (isError)
  {
   _errorCounter.Add(1);
        }

        _logger.LogInformation(
            "Embedding request completed: Duration={Duration}ms, InputLength={Length}, Memory={Memory}MB, Error={Error}",
 durationMs, inputLength, memoryMB, isError);
    }

    /// <summary>
    /// Records an error
    /// </summary>
    public void RecordError()
    {
_errorCounter.Add(1);
    }

    /// <summary>
    /// Gets current performance snapshot
    /// </summary>
    public PerformanceSnapshot GetSnapshot()
    {
        _currentProcess.Refresh();
        
    var workingSetMB = GetWorkingSetMB();
        var privateMemoryMB = GetPrivateMemoryMB();
  var cpuPercent = GetCpuUsagePercent();

 // Track peaks
     var workingSetBytes = _currentProcess.WorkingSet64;
        var privateMemoryBytes = _currentProcess.PrivateMemorySize64;
     
        if (workingSetBytes > _peakWorkingSetBytes)
     _peakWorkingSetBytes = workingSetBytes;
    
        if (privateMemoryBytes > _peakPrivateMemoryBytes)
        _peakPrivateMemoryBytes = privateMemoryBytes;
        
        if (cpuPercent > _peakCpuPercent)
       _peakCpuPercent = cpuPercent;

        return new PerformanceSnapshot
{
            Timestamp = DateTime.UtcNow,
     CpuUsagePercent = cpuPercent,
    WorkingSetMB = workingSetMB,
          PrivateMemoryMB = privateMemoryMB,
    GCHeapSizeMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
   Gen0Collections = GC.CollectionCount(0),
    Gen1Collections = GC.CollectionCount(1),
      Gen2Collections = GC.CollectionCount(2),
            ThreadCount = _currentProcess.Threads.Count,
        HandleCount = _currentProcess.HandleCount,
            PeakWorkingSetMB = _peakWorkingSetBytes / 1024.0 / 1024.0,
    PeakPrivateMemoryMB = _peakPrivateMemoryBytes / 1024.0 / 1024.0,
        PeakCpuPercent = _peakCpuPercent
        };
    }

    private double GetWorkingSetMB()
    {
        _currentProcess.Refresh();
  return _currentProcess.WorkingSet64 / 1024.0 / 1024.0;
    }

    private double GetPrivateMemoryMB()
    {
     _currentProcess.Refresh();
return _currentProcess.PrivateMemorySize64 / 1024.0 / 1024.0;
    }

    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.MinValue;

    private double GetCpuUsagePercent()
    {
    _currentProcess.Refresh();
        var now = DateTime.UtcNow;
        var currentCpuTime = _currentProcess.TotalProcessorTime;

      if (_lastCpuCheck == DateTime.MinValue)
        {
    _lastCpuCheck = now;
        _lastCpuTime = currentCpuTime;
     return 0;
        }

        var timeDiff = (now - _lastCpuCheck).TotalMilliseconds;
     var cpuDiff = (currentCpuTime - _lastCpuTime).TotalMilliseconds;

        if (timeDiff > 0)
        {
      var cpuPercent = (cpuDiff / (timeDiff * Environment.ProcessorCount)) * 100.0;
        
            _lastCpuCheck = now;
      _lastCpuTime = currentCpuTime;
         
        return Math.Round(cpuPercent, 2);
        }

        return 0;
    }

    public void Dispose()
    {
     _meter?.Dispose();
    }
}

/// <summary>
/// Performance snapshot at a point in time
/// </summary>
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public double CpuUsagePercent { get; set; }
    public double WorkingSetMB { get; set; }
  public double PrivateMemoryMB { get; set; }
    public double GCHeapSizeMB { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
    public double PeakWorkingSetMB { get; set; }
    public double PeakPrivateMemoryMB { get; set; }
    public double PeakCpuPercent { get; set; }
}
