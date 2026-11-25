using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Health check for the embedding service
/// </summary>
public sealed class EmbeddingHealthCheck : IHealthCheck
{
    private readonly IEmbeddingService _embeddingService;
    private DateTime _lastHealthyCheck = DateTime.MinValue;
    private readonly TimeSpan _cacheValidDuration = TimeSpan.FromSeconds(60);
    private HealthCheckResult? _cachedResult;

    /// <summary>
    /// Initializes a new instance of the EmbeddingHealthCheck class
    /// </summary>
    /// <param name="embeddingService">The embedding service to check</param>
    public EmbeddingHealthCheck(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Checks the health of the embedding service
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Cache health check results to avoid running inference every 30 seconds
        if (_cachedResult != null && DateTime.UtcNow - _lastHealthyCheck < _cacheValidDuration)
        {
            return Task.FromResult(_cachedResult.Value);
        }

        try
        {
            // Prefer checking model load status exposed by the embedding service to avoid
            // performing a live inference on every health check.
            if (_embeddingService is { IsModelLoaded: true })
            {
                _cachedResult = HealthCheckResult.Healthy("Embedding model loaded and ready");
                _lastHealthyCheck = DateTime.UtcNow;
                return Task.FromResult(_cachedResult.Value);
            }

            // If model not loaded, return unhealthy with available error information
            var error = (_embeddingService as dynamic)?.ModelLoadError as string;
            _cachedResult = HealthCheckResult.Unhealthy("Embedding model not loaded", error == null ? null : new Exception(error));
            return Task.FromResult(_cachedResult.Value);
        }
        catch (Exception ex)
        {
            _cachedResult = HealthCheckResult.Unhealthy("Embedding service failed", ex);
            return Task.FromResult(_cachedResult.Value);
        }
    }
}