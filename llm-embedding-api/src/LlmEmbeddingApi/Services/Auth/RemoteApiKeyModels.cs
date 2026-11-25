namespace LlmEmbeddingApi.Services.Auth;

/// <summary>
/// Request body posted to the organization service's API key validation endpoint
/// (<c>POST /api/org/apikeys/validate</c>).
/// </summary>
/// <param name="ApiKey">The plaintext API key to validate.</param>
public sealed record RemoteApiKeyValidationRequest(string ApiKey);

/// <summary>
/// Response body from the organization service's API key validation endpoint.
/// A 200 response with <see cref="Status"/> equal to <c>"Active"</c> is the only
/// combination treated as valid; any other status, or any non-200 response, is invalid.
/// </summary>
/// <param name="TenantId">Tenant the key belongs to.</param>
/// <param name="Scopes">Scopes granted to the key.</param>
/// <param name="Status">Key status, e.g. <c>"Active"</c>, <c>"Revoked"</c>, <c>"Suspended"</c>.</param>
public sealed record RemoteApiKeyValidationResponse(
    string? TenantId,
    List<string>? Scopes,
    string? Status);
