using System.ComponentModel.DataAnnotations;

namespace LlmEmbeddingApi.Configuration;

/// <summary>
/// Authentication configuration for the API. Supports statically configured API key
/// hashes and/or delegating validation to the organization service.
/// </summary>
public sealed class AuthOptions
{
    /// <summary>
    /// Statically configured API keys. Only SHA-256 hashes are stored here — never
    /// plaintext keys. See <c>tools/hash-key</c> in the repo README for how to
    /// compute a hash from a plaintext key.
    /// </summary>
    public List<ApiKeyEntry> ApiKeys { get; set; } = new();

    /// <summary>
    /// Optional remote validation against the organization service's API key endpoint.
    /// </summary>
    public RemoteValidationOptions RemoteValidation { get; set; } = new();
}

/// <summary>
/// A single statically configured API key, identified by its SHA-256 hash.
/// </summary>
public sealed class ApiKeyEntry
{
    /// <summary>
    /// Lowercase hex-encoded SHA-256 hash of the plaintext API key. Never store the
    /// plaintext key in configuration.
    /// </summary>
    [Required]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label for the key (e.g. the consuming service or team), used for
    /// logging/auditing only.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional tenant this key is scoped to. When null, the key is not tenant-scoped.
    /// </summary>
    public string? TenantId { get; set; }
}

/// <summary>
/// Configuration for delegating API key validation to the organization service.
/// </summary>
public sealed class RemoteValidationOptions
{
    /// <summary>
    /// Whether remote validation is enabled. When disabled, only statically configured
    /// keys (<see cref="AuthOptions.ApiKeys"/>) are accepted.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Full URL of the organization service's key validation endpoint, e.g.
    /// <c>https://organization.internal/api/org/apikeys/validate</c>.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// HTTP timeout (seconds) for the remote validation call.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// How long (minutes) a successful ("Active") validation result is cached in-memory.
    /// </summary>
    public int PositiveCacheMinutes { get; set; } = 5;

    /// <summary>
    /// How long (minutes) a failed/invalid validation result is cached in-memory. Kept
    /// short so a key that is later activated is not locked out for long, and so a
    /// transient issue on the org service doesn't cause an extended false-negative.
    /// </summary>
    public int NegativeCacheMinutes { get; set; } = 1;
}
