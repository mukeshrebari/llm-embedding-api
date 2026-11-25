using System.Diagnostics;
using LlmEmbeddingApi.Services;

namespace LlmEmbeddingApi.Middleware;

/// <summary>
/// Middleware to monitor request performance
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
private readonly ILogger<PerformanceMonitoringMiddleware> _logger;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, PerformanceMetricsService metricsService)
    {
      var stopwatch = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            await _next(context);
          stopwatch.Stop();

   // Only log for API endpoints
       if (context.Request.Path.StartsWithSegments("/api"))
 {
         var duration = stopwatch.Elapsed.TotalMilliseconds;
           var statusCode = context.Response.StatusCode;
         
     _logger.LogInformation(
         "Request {Method} {Path} completed in {Duration}ms with status {StatusCode}",
        context.Request.Method,
        context.Request.Path,
         duration,
             statusCode);
  }
        }
catch (Exception ex)
        {
  stopwatch.Stop();
            metricsService.RecordError();
            
       _logger.LogError(ex,
  "Request {Method} {Path} failed after {Duration}ms",
    context.Request.Method,
       context.Request.Path,
   stopwatch.Elapsed.TotalMilliseconds);
       
          throw;
        }
    }
}
