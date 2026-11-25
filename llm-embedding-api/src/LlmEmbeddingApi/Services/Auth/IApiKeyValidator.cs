namespace LlmEmbeddingApi.Services.Auth;

/// <summary>
/// Validates API keys presented by callers, against statically configured key hashes
/// and/or the organization service's remote validation endpoint.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates the given plaintext API key.
    /// </summary>
    /// <param name="plaintextKey">The raw API key as presented by the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ApiKeyValidationResult> ValidateAsync(string plaintextKey, CancellationToken cancellationToken = default);
}
