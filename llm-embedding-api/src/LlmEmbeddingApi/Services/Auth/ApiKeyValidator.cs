using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using LlmEmbeddingApi.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LlmEmbeddingApi.Services.Auth;

/// <summary>
/// Validates API keys against statically configured key hashes first, then — if none
/// match and remote validation is enabled — against the organization service, with an
/// in-memory positive/negative cache to avoid a round-trip per request.
/// </summary>
public sealed class ApiKeyValidator : IApiKeyValidator
{
    private const string CacheKeyPrefix = "apikey:";
    private static readonly string HttpClientName = "OrgApiKeyValidation";

    private readonly IOptionsMonitor<AuthOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ApiKeyValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyValidator"/> class.
    /// </summary>
    public ApiKeyValidator(
        IOptionsMonitor<AuthOptions> options,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<ApiKeyValidator> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Name of the named <see cref="HttpClient"/> registered for organization service calls.
    /// </summary>
    public static string HttpClientRegistrationName => HttpClientName;

    /// <inheritdoc />
    public async Task<ApiKeyValidationResult> ValidateAsync(string plaintextKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextKey))
        {
            return ApiKeyValidationResult.Invalid();
        }

        var options = _options.CurrentValue;
        var hash = ComputeHash(plaintextKey);

        var staticMatch = options.ApiKeys.Find(k =>
            !string.IsNullOrWhiteSpace(k.KeyHash) &&
            string.Equals(k.KeyHash, hash, StringComparison.OrdinalIgnoreCase));

        if (staticMatch is not null)
        {
            return new ApiKeyValidationResult(
                IsValid: true,
                TenantId: staticMatch.TenantId,
                Scopes: null,
                KeyName: staticMatch.Name,
                Source: ApiKeyValidationSource.Static);
        }

        if (!options.RemoteValidation.Enabled)
        {
            return ApiKeyValidationResult.Invalid();
        }

        return await ValidateRemoteAsync(hash, plaintextKey, options.RemoteValidation, cancellationToken);
    }

    private async Task<ApiKeyValidationResult> ValidateRemoteAsync(
        string hash,
        string plaintextKey,
        RemoteValidationOptions remoteOptions,
        CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeyPrefix + hash;

        if (_cache.TryGetValue(cacheKey, out ApiKeyValidationResult? cached) && cached is not null)
        {
            return cached with { Source = ApiKeyValidationSource.RemoteCache };
        }

        if (string.IsNullOrWhiteSpace(remoteOptions.Endpoint))
        {
            _logger.LogWarning("Remote API key validation is enabled but no endpoint is configured (Auth:RemoteValidation:Endpoint)");
            return ApiKeyValidationResult.Invalid();
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, remoteOptions.TimeoutSeconds)));

            using var response = await client.PostAsJsonAsync(
                remoteOptions.Endpoint,
                new RemoteApiKeyValidationRequest(plaintextKey),
                cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return CacheAndReturn(
                    cacheKey,
                    ApiKeyValidationResult.Invalid(ApiKeyValidationSource.Remote),
                    remoteOptions.NegativeCacheMinutes);
            }

            var body = await response.Content.ReadFromJsonAsync<RemoteApiKeyValidationResponse>(cts.Token);

            if (body is null || !string.Equals(body.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return CacheAndReturn(
                    cacheKey,
                    ApiKeyValidationResult.Invalid(ApiKeyValidationSource.Remote),
                    remoteOptions.NegativeCacheMinutes);
            }

            var result = new ApiKeyValidationResult(
                IsValid: true,
                TenantId: body.TenantId,
                Scopes: body.Scopes,
                KeyName: null,
                Source: ApiKeyValidationSource.Remote);

            return CacheAndReturn(cacheKey, result, remoteOptions.PositiveCacheMinutes);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Remote API key validation timed out calling {Endpoint}", remoteOptions.Endpoint);
            return ApiKeyValidationResult.TransientFailure();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Remote API key validation failed calling {Endpoint}", remoteOptions.Endpoint);
            return ApiKeyValidationResult.TransientFailure();
        }
    }

    private ApiKeyValidationResult CacheAndReturn(string cacheKey, ApiKeyValidationResult result, int minutes)
    {
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, minutes)),
            Size = 1,
        });
        return result;
    }

    private static string ComputeHash(string plaintextKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintextKey);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
