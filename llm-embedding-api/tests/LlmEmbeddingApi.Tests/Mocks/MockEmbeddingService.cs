using LlmEmbeddingApi.Models;
using LlmEmbeddingApi.Services;

namespace LlmEmbeddingApi.Tests.Mocks;

/// <summary>
/// Mock embedding service for testing
/// </summary>
public class MockEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        // Return a mock embedding
        var embedding = new float[768];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(i * 0.001);
        }

        return Task.FromResult(embedding);
    }

    public Task<EmbeddingBatchResult> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var embeddings = new List<float[]>(texts.Count);
        var tokenCounts = new List<int>(texts.Count);

        foreach (var text in texts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            var embedding = new float[768];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(i * 0.001);
            }

            embeddings.Add(embedding);
            tokenCounts.Add(text.Length);
        }

        return Task.FromResult(new EmbeddingBatchResult(embeddings, tokenCounts));
    }

    public bool IsModelLoaded => true;

    public string? ModelLoadError => null;
}