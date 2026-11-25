using LlmEmbeddingApi.Services.Auth;
using Microsoft.Extensions.Primitives;

namespace LlmEmbeddingApi.Middleware;

/// <summary>
/// Enforces API key authentication on <c>/api/*</c> routes (except an explicit
/// anonymous allow-list). Accepts the key via the <c>x-api-key</c> header (kept for
/// client compatibility) or an <c>Authorization: Bearer &lt;key&gt;</c> header as a
/// fallback. Delegates the actual validation to <see cref="IApiKeyValidator"/>, which
/// checks statically configured key hashes and/or the organization service.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    /// <summary>
    /// Header used for API keys, kept for backwards compatibility with existing clients.
    /// </summary>
    public const string ApiKeyHeaderName = "x-api-key";

    private const string BearerPrefix = "Bearer ";

    /// <summary>
    /// Route prefix that requires authentication.
    /// </summary>
    private const string ProtectedPathPrefix = "/api";

    /// <summary>
    /// Paths under <see cref="ProtectedPathPrefix"/> that remain anonymous — currently
    /// just the model download-status check, which exposes no sensitive data and is
    /// used as a readiness signal alongside <c>/health</c>.
    /// </summary>
    private static readonly string[] AnonymousPaths = ["/api/model/status"];

    /// <summary>
    /// Key used to stash the validated auth context on <see cref="HttpContext.Items"/>
    /// for downstream consumers (e.g. logging, rate-limit partitioning).
    /// </summary>
    public const string AuthResultItemKey = "ApiKeyAuthResult";

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthMiddleware"/> class.
    /// </summary>
    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Validates the API key (if required for the request path) and either continues
    /// the pipeline or short-circuits with 401/403/503.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IApiKeyValidator apiKeyValidator)
    {
        var path = context.Request.Path;

        if (!path.HasValue || !path.StartsWithSegments(ProtectedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (IsAnonymousPath(path.Value))
        {
            await _next(context);
            return;
        }

        var providedKey = ExtractKey(context.Request.Headers);

        if (string.IsNullOrWhiteSpace(providedKey))
        {
            await WriteUnauthorized(context, "Missing API key (header: x-api-key, or Authorization: Bearer <key>)");
            return;
        }

        var result = await apiKeyValidator.ValidateAsync(providedKey, context.RequestAborted);

        if (result.Transient)
        {
            _logger.LogWarning("API key validation temporarily unavailable for request {Path}", path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("API key validation temporarily unavailable, please retry");
            return;
        }

        if (!result.IsValid)
        {
            await WriteForbidden(context, "Invalid API key");
            return;
        }

        context.Items[AuthResultItemKey] = result;
        await _next(context);
    }

    private static bool IsAnonymousPath(string path)
    {
        foreach (var anonymousPath in AnonymousPaths)
        {
            if (string.Equals(path, anonymousPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractKey(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(ApiKeyHeaderName, out StringValues apiKeyValue) && !string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return apiKeyValue.ToString();
        }

        if (headers.TryGetValue("Authorization", out StringValues authHeader))
        {
            var value = authHeader.ToString();
            if (value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[BearerPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
        }

        return null;
    }

    private static Task WriteUnauthorized(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsync(message);
    }

    private static Task WriteForbidden(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsync(message);
    }
}
