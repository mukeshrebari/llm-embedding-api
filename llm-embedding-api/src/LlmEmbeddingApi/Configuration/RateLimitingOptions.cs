namespace LlmEmbeddingApi.Configuration;

/// <summary>
/// Configuration for API rate limiting, partitioned per API key (falling back to
/// per-client-IP for unauthenticated/anonymous callers).
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>
    /// Requests permitted per rolling one-minute window for general <c>/api/*</c>
    /// endpoints, per partition (API key or IP).
    /// </summary>
    public int PermitPerMinute { get; set; } = 120;

    /// <summary>
    /// Requests permitted per rolling one-minute window for the batch embedding
    /// endpoint (<c>POST /api/embed/batch</c>), per partition. Stricter than the
    /// general limit because batch requests are more expensive.
    /// </summary>
    public int BatchPermitPerMinute { get; set; } = 30;

    /// <summary>
    /// Number of requests allowed to queue once the limit is reached, before requests
    /// are rejected with 429.
    /// </summary>
    public int QueueLimit { get; set; }
}
