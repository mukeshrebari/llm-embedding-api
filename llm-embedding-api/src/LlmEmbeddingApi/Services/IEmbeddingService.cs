using LlmEmbeddingApi.Models;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Service for generating text embeddings
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embeddings for the provided text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for a collection of texts in a single batch.
    /// </summary>
    /// <param name="texts">Texts to embed.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch embedding vectors and token counts.</returns>
    Task<EmbeddingBatchResult> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether the underlying model was loaded successfully and is ready for inference.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// If model loading failed, contains the error message; otherwise null.
    /// </summary>
    string? ModelLoadError { get; }
}
