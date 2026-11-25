namespace LlmEmbeddingApi.Services.Auth;

/// <summary>
/// Where an API key validation decision came from.
/// </summary>
public enum ApiKeyValidationSource
{
    /// <summary>No source — key was never evaluated.</summary>
    None,

    /// <summary>Matched a statically configured key hash (<c>Auth:ApiKeys</c>).</summary>
    Static,

    /// <summary>Validated by calling the organization service.</summary>
    Remote,

    /// <summary>Served from the remote-validation in-memory cache.</summary>
    RemoteCache,
}

/// <summary>
/// Outcome of validating a provided API key.
/// </summary>
/// <param name="IsValid">Whether the key is valid and the caller should be authorized.</param>
/// <param name="TenantId">Tenant the key is scoped to, if any.</param>
/// <param name="Scopes">Scopes granted to the key, if any (remote validation only).</param>
/// <param name="KeyName">Human-readable key name/label, for logging (static keys only).</param>
/// <param name="Source">Where this decision came from.</param>
/// <param name="Transient">
/// True when validation could not be completed due to a transient error (e.g. the
/// remote validation endpoint timed out or errored) rather than the key genuinely
/// being invalid. Callers may want to respond with 503 instead of 401/403 in this case.
/// </param>
public sealed record ApiKeyValidationResult(
    bool IsValid,
    string? TenantId,
    IReadOnlyList<string>? Scopes,
    string? KeyName,
    ApiKeyValidationSource Source,
    bool Transient = false)
{
    /// <summary>Creates an invalid result attributed to the given source.</summary>
    public static ApiKeyValidationResult Invalid(ApiKeyValidationSource source = ApiKeyValidationSource.None) =>
        new(false, null, null, null, source);

    /// <summary>Creates a result representing a transient validation failure (e.g. remote endpoint unreachable).</summary>
    public static ApiKeyValidationResult TransientFailure() =>
        new(false, null, null, null, ApiKeyValidationSource.None, Transient: true);
}
