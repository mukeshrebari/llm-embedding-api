using System.Security.Cryptography;
using System.Text;
using LlmEmbeddingApi.Services.Auth;

namespace LlmEmbeddingApi.Middleware;

/// <summary>
/// Helpers for deriving rate-limit partition keys: per API key when one is presented,
/// falling back to per-client-IP otherwise (e.g. anonymous endpoints, or requests with
/// no/invalid key that still need throttling to blunt brute-force attempts).
/// </summary>
public static class RateLimitPartitioning
{
    /// <summary>
    /// Derives a stable partition key for the current request. The raw API key is
    /// hashed before use so it is never retained/logged in plaintext.
    /// </summary>
    public static string GetPartitionKey(HttpContext context)
    {
        var key = ExtractRawKey(context);

        if (!string.IsNullOrWhiteSpace(key))
        {
            return $"key:{HashKey(key)}";
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    private static string? ExtractRawKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(ApiKeyAuthMiddleware.ApiKeyHeaderName, out var apiKeyValue)
            && !string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return apiKeyValue.ToString();
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.ToString();
            const string bearerPrefix = "Bearer ";
            if (value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[bearerPrefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(token) ? null : token;
            }
        }

        return null;
    }

    private static string HashKey(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
