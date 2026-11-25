namespace LlmEmbeddingApi.Models;

/// <summary>
/// Response model containing generated embeddings for a batch request.
/// </summary>
/// <param name="Embeddings">Embedding vectors for each input text.</param>
/// <param name="TokenCounts">Number of tokens processed per input text.</param>
/// <param name="Model">Model used for generation.</param>
public sealed record BatchEmbeddingResponse(
    IReadOnlyList<float[]> Embeddings,
    IReadOnlyList<int> TokenCounts,
    string Model = "embeddinggemma"
);
