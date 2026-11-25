namespace LlmEmbeddingApi.Models;

/// <summary>
/// Response model containing generated embeddings
/// </summary>
/// <param name="Embeddings">The generated embedding vector</param>
/// <param name="TokenCount">Number of tokens processed</param>
/// <param name="Model">Model used for generation</param>
public sealed record EmbeddingResponse(
    float[] Embeddings,
    int TokenCount,
    string Model = "embeddinggemma"
);