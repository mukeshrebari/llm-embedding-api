using LlmEmbeddingApi.Configuration;
using LlmEmbeddingApi.Models;
using LlmEmbeddingApi.Services;
using LlmEmbeddingApi.Services.Auth;
using LlmEmbeddingApi.Middleware;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
 c.SwaggerDoc("v1", new() { Title = "LLM Embedding API", Version = "v1" });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "LlmEmbeddingApi.xml"));
});

builder.Services.AddHealthChecks()
    .AddCheck<EmbeddingHealthCheck>("embedding_service");

builder.Services.Configure<EmbeddingOptions>(
    builder.Configuration.GetSection("Embedding"));

builder.Services.AddHttpClient<IModelDownloader, ModelDownloader>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(30); // Default timeout for downloads
    client.DefaultRequestHeaders.Add("User-Agent", "LLM-Embedding-API/1.0");
    // Authorization header is added per-request in ModelDownloader when needed
});

// === Authentication (static API key hashes + optional remote validation) ===
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddHttpClient(ApiKeyValidator.HttpClientRegistrationName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "LLM-Embedding-API/1.0");
});

// === Rate limiting (partitioned per API key, fallback per client IP) ===
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));
var rateLimitingOptions = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitioning.GetPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, rateLimitingOptions.PermitPerMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = rateLimitingOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));

    options.AddPolicy("batch", context => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPartitioning.GetPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, rateLimitingOptions.BatchPermitPerMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = rateLimitingOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        }));
});

// Performance monitoring services
builder.Services.AddSingleton<PerformanceMetricsService>();
builder.Services.AddHostedService<PerformanceCollectorService>();

builder.Services.AddHostedService<ModelInitializationService>();
builder.Services.AddSingleton<IEmbeddingService, GemmaEmbeddingService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Limit cache size
});
var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseExceptionHandler();

// === Rate limiting ===
// Applied before authentication so brute-force/invalid-key traffic is throttled too.
// Endpoints opt in via .RequireRateLimiting("api"|"batch"); anonymous endpoints
// (/health, /api/model/status) don't set a policy and are left unlimited.
app.UseRateLimiter();

// === API key authentication ===
// Static key hashes (Auth:ApiKeys) and/or remote validation via the organization
// service (Auth:RemoteValidation), see Configuration/AuthOptions.cs and
// Services/Auth/ApiKeyValidator.cs. Enforced on /api/* except the anonymous
// allow-list defined in ApiKeyAuthMiddleware.
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseMiddleware<PerformanceMonitoringMiddleware>();
app.MapGet("/", () => "LLM Embedding API is running. Visit /swagger for API documentation.");
app.UseHealthChecks("/health");

// API Endpoints
var apiGroup = app.MapGroup("/api")
    .WithOpenApi()
    .WithTags("Embedding");

apiGroup.MapPost("/embed", async Task<Results<Ok<EmbeddingResponse>, BadRequest<string>, StatusCodeHttpResult>> (
    [FromBody] EmbeddingRequest request,
    IEmbeddingService embeddingService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Text))
    {
        return TypedResults.BadRequest("Text cannot be empty");
    }

    try
    {
        var embeddings = await embeddingService.GenerateEmbeddingAsync(request.Text, cancellationToken);
        return TypedResults.Ok(new EmbeddingResponse(embeddings, request.Text.Length));
    }
    catch (OperationCanceledException)
    {
        return TypedResults.StatusCode(499); // Client closed request
    }
    catch (Exception)
    {
        return TypedResults.StatusCode(500);
    }
})
.WithName("GenerateEmbedding")
.WithSummary("Generate embeddings for text")
.WithDescription("Generates vector embeddings for the provided text using the Gemma embedding model")
.Produces<EmbeddingResponse>(200)
.Produces<string>(400)
.Produces(500)
.RequireRateLimiting("api");

apiGroup.MapPost("/embed/batch", async Task<Results<Ok<BatchEmbeddingResponse>, BadRequest<string>, StatusCodeHttpResult>> (
    [FromBody] BatchEmbeddingRequest request,
    IEmbeddingService embeddingService,
    CancellationToken cancellationToken) =>
{
    if (request?.Texts is null || request.Texts.Count == 0)
    {
        return TypedResults.BadRequest("Texts cannot be empty");
    }

    if (request.Texts.Any(string.IsNullOrWhiteSpace))
    {
        return TypedResults.BadRequest("Texts cannot contain empty items");
    }

    try
    {
        var batchResult = await embeddingService.GenerateEmbeddingsBatchAsync(request.Texts, cancellationToken);
        return TypedResults.Ok(new BatchEmbeddingResponse(
            batchResult.Embeddings.ToList(),
            batchResult.TokenCounts.ToArray(),
            request.Model));
    }
    catch (ArgumentException ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
    catch (OperationCanceledException)
    {
        return TypedResults.StatusCode(499); // Client closed request
    }
    catch (Exception)
    {
        return TypedResults.StatusCode(500);
    }
})
.WithName("GenerateBatchEmbeddings")
.WithSummary("Generate embeddings for multiple texts")
.WithDescription("Generates vector embeddings for multiple texts using a single ONNX call")
.Produces<BatchEmbeddingResponse>(200)
.Produces<string>(400)
.Produces(500)
.RequireRateLimiting("batch");

apiGroup.MapGet("/models", () => TypedResults.Ok(new { models = new[] { "embeddinggemma" } }))
    .WithName("GetModels")
    .WithSummary("Get available models")
    .RequireRateLimiting("api");

apiGroup.MapGet("/model/status", (HttpContext context) =>
{
    var options = context.RequestServices.GetRequiredService<IOptions<EmbeddingOptions>>();
    var downloader = context.RequestServices.GetRequiredService<IModelDownloader>();
    
    var opts = options.Value;
    var exists = File.Exists(opts.ModelPath);
    var isValid = exists && downloader.ValidateModel(opts.ModelPath);
    var fileSize = exists ? new FileInfo(opts.ModelPath).Length : 0;

    return TypedResults.Ok(new
    {
        modelPath = opts.ModelPath,
        exists,
        isValid,
        fileSizeBytes = fileSize,
        autoDownload = opts.AutoDownload,
        downloadSources = new
        {
            huggingFaceRepoId = opts.HuggingFaceRepoId,
            huggingFaceFilename = opts.HuggingFaceFilename,
            downloadUrl = opts.DownloadUrl
        }
    });
})
.WithName("GetModelStatus")
.WithSummary("Get model download status")
.WithDescription("Anonymous — no API key required (see ApiKeyAuthMiddleware allow-list). Used as a readiness signal alongside /health.");

apiGroup.MapPost("/model/download", async Task<IResult> (HttpContext context) =>
{
    var downloader = context.RequestServices.GetRequiredService<IModelDownloader>();
    var options = context.RequestServices.GetRequiredService<IOptions<EmbeddingOptions>>();
    var cancellationToken = context.RequestAborted;
    
    var opts = options.Value;
    
    if (!opts.AutoDownload)
    {
        return TypedResults.BadRequest("Auto-download is disabled");
    }

    try
    {
        bool success = false;
        
        if (!string.IsNullOrEmpty(opts.HuggingFaceRepoId) && !string.IsNullOrEmpty(opts.HuggingFaceFilename))
        {
            success = await downloader.DownloadFromHuggingFaceAsync(
                opts.ModelPath, opts.HuggingFaceRepoId, opts.HuggingFaceFilename, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(opts.DownloadUrl))
        {
            success = await downloader.EnsureModelExistsAsync(opts.ModelPath, opts.DownloadUrl, cancellationToken);
        }
        else
        {
            return TypedResults.BadRequest("No download source configured");
        }

        if (success)
        {
            return TypedResults.Ok(new { message = "Model downloaded successfully", modelPath = opts.ModelPath });
        }
        else
        {
            return TypedResults.StatusCode(500);
        }
    }
    catch (OperationCanceledException)
    {
        return TypedResults.StatusCode(408); // Request timeout
    }
    catch (Exception)
    {
        return TypedResults.StatusCode(500);
    }
})
.WithName("DownloadModel")
.WithSummary("Trigger model download")
.RequireRateLimiting("api");

// Performance monitoring endpoints
var metricsGroup = app.MapGroup("/api/metrics")
    .WithOpenApi()
    .WithTags("Performance");

metricsGroup.MapGet("/current", async Task<IResult> (PerformanceMetricsService metricsService) =>
{
    var snapshot = metricsService.GetSnapshot();
    await Task.CompletedTask;
    return TypedResults.Ok(snapshot);
})
.WithName("GetCurrentMetrics")
.WithSummary("Get current performance metrics")
.WithDescription("Returns current CPU, memory, and GC metrics")
.RequireRateLimiting("api");

metricsGroup.MapGet("/statistics", async Task<IResult> (HttpContext context) =>
{
    var collectorService = context.RequestServices
        .GetServices<IHostedService>()
        .OfType<PerformanceCollectorService>()
        .FirstOrDefault();
    
    if (collectorService == null)
    {
        return await Task.FromResult(TypedResults.Problem("Performance collector service not found"));
    }

    var stats = collectorService.GetStatistics();
    await Task.CompletedTask;
    return TypedResults.Ok(stats);
})
.WithName("GetPerformanceStatistics")
.WithSummary("Get performance statistics")
.WithDescription("Returns aggregated performance statistics and Azure Container Instance recommendations")
.RequireRateLimiting("api");

metricsGroup.MapGet("/snapshots", async Task<IResult> (HttpContext context, [FromQuery] int? limit) =>
{
    var collectorService = context.RequestServices
        .GetServices<IHostedService>()
        .OfType<PerformanceCollectorService>()
        .FirstOrDefault();
  
    if (collectorService == null)
    {
        return await Task.FromResult(TypedResults.Problem("Performance collector service not found"));
    }

    var snapshots = collectorService.GetSnapshots();
    
    if (limit.HasValue && limit.Value > 0)
    {
        snapshots = snapshots.TakeLast(limit.Value).ToList();
    }

    await Task.CompletedTask;
    return TypedResults.Ok(new
    {
        count = snapshots.Count,
        snapshots
    });
})
.WithName("GetPerformanceSnapshots")
.WithSummary("Get performance snapshots")
.WithDescription("Returns historical performance snapshots. Optional limit parameter to get last N snapshots.")
.RequireRateLimiting("api");

metricsGroup.MapPost("/gc", async Task<IResult> () =>
{
    var before = GC.GetTotalMemory(false);
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var after = GC.GetTotalMemory(false);
    var freed = before - after;

    await Task.CompletedTask;
    return TypedResults.Ok(new
    {
    message = "Garbage collection completed",
        memoryBeforeMB = before / 1024.0 / 1024.0,
        memoryAfterMB = after / 1024.0 / 1024.0,
        freedMB = freed / 1024.0 / 1024.0
    });
})
.WithName("ForceGarbageCollection")
.WithSummary("Force garbage collection")
.WithDescription("Triggers a full garbage collection and reports memory freed")
.RequireRateLimiting("api");

app.Run();

/// <summary>
/// Entry point class for the application
/// </summary>
public partial class Program { }

/// <summary>
/// Global exception handler for the application
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the GlobalExceptionHandler class
    /// </summary>
    /// <param name="logger">Logger instance</param>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Tries to handle the exception
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="exception">Exception to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if handled, false otherwise</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = "An internal server error occurred. Please try again later."
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
