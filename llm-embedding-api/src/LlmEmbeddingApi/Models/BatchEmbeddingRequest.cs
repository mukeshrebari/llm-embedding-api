using System.ComponentModel.DataAnnotations;

namespace LlmEmbeddingApi.Models;

/// <summary>
/// Request model for generating embeddings for multiple inputs in one call.
/// </summary>
/// <param name="Texts">Collection of texts to embed.</param>
/// <param name="Model">Model name (optional, defaults to embeddinggemma).</param>
public sealed record BatchEmbeddingRequest(
    [Required, MinLength(1), MaxLength(16)] List<string> Texts,
    string Model = "embeddinggemma"
);
