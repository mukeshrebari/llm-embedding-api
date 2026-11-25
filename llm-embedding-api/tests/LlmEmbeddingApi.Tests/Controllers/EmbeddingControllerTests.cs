using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using LlmEmbeddingApi.Middleware;
using LlmEmbeddingApi.Models;
using LlmEmbeddingApi.Services;
using LlmEmbeddingApi.Tests.Mocks;
using Xunit;

namespace LlmEmbeddingApi.Tests;

public class EmbeddingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Test-only plaintext key. Only its SHA-256 hash is ever configured/compared —
    // this mirrors how real deployments must only store Auth:ApiKeys:*:KeyHash.
    private const string TestApiKey = "test-plaintext-key-for-unit-tests-only";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public EmbeddingApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:ApiKeys:0:KeyHash"] = ComputeHash(TestApiKey),
                    ["Auth:ApiKeys:0:Name"] = "test-suite",
                    // Keep rate limits generous so the test suite itself never gets throttled.
                    ["RateLimiting:PermitPerMinute"] = "1000",
                    ["RateLimiting:BatchPermitPerMinute"] = "1000",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace the real service with mock for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmbeddingService));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IEmbeddingService, MockEmbeddingService>();
            });
        });
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(ApiKeyAuthMiddleware.ApiKeyHeaderName, TestApiKey);
    }

    private static string ComputeHash(string plaintextKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintextKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    [Fact]
    public async Task GetModels_ReturnsAvailableModels()
    {
        // Act
        var response = await _client.GetAsync("/api/models");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("embeddinggemma", content);
    }

    [Fact]
    public async Task PostEmbed_WithValidRequest_ReturnsEmbedding()
    {
        // Arrange
        var request = new EmbeddingRequest("Hello world");

        // Act
        var response = await _client.PostAsJsonAsync("/api/embed", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var embeddingResponse = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        Assert.NotNull(embeddingResponse);
        Assert.NotEmpty(embeddingResponse.Embeddings);
        Assert.Equal(768, embeddingResponse.Embeddings.Length);
    }

    [Fact]
    public async Task PostEmbed_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new EmbeddingRequest("");

        // Act
        var response = await _client.PostAsJsonAsync("/api/embed", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostEmbed_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange — a client with no x-api-key/Authorization header at all
        using var anonymousClient = _factory.CreateClient();
        var request = new EmbeddingRequest("Hello world");

        // Act
        var response = await anonymousClient.PostAsJsonAsync("/api/embed", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEmbed_WithWrongApiKey_ReturnsForbidden()
    {
        // Arrange
        using var wrongKeyClient = _factory.CreateClient();
        wrongKeyClient.DefaultRequestHeaders.Add(ApiKeyAuthMiddleware.ApiKeyHeaderName, "definitely-not-the-right-key");
        var request = new EmbeddingRequest("Hello world");

        // Act
        var response = await wrongKeyClient.PostAsJsonAsync("/api/embed", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostEmbed_WithBearerToken_Authenticates()
    {
        // Arrange — Authorization: Bearer fallback instead of x-api-key
        using var bearerClient = _factory.CreateClient();
        bearerClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestApiKey}");
        var request = new EmbeddingRequest("Hello world");

        // Act
        var response = await bearerClient.PostAsJsonAsync("/api/embed", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetModelStatus_WithoutApiKey_IsAnonymous()
    {
        // Arrange — model/status must remain accessible without a key, like /health
        using var anonymousClient = _factory.CreateClient();

        // Act
        var response = await anonymousClient.GetAsync("/api/model/status");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostEmbedBatch_WithValidRequest_ReturnsEmbeddings()
    {
        // Arrange
        var request = new BatchEmbeddingRequest(new List<string> { "Hello", "World" });

        // Act
        var response = await _client.PostAsJsonAsync("/api/embed/batch", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var batchResponse = await response.Content.ReadFromJsonAsync<BatchEmbeddingResponse>();
        Assert.NotNull(batchResponse);
        Assert.Equal(2, batchResponse.Embeddings.Count);
    }
}
