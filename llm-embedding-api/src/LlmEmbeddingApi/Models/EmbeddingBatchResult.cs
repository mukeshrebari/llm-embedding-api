namespace LlmEmbeddingApi.Models;

/// <summary>
/// Internal result for batch embedding generation.
/// </summary>
/// <param name="Embeddings">Embedding vectors per input.</param>
/// <param name="TokenCounts">Token counts per input.</param>
public sealed record EmbeddingBatchResult(
    IReadOnlyList<float[]> Embeddings,
    IReadOnlyList<int> TokenCounts
);
