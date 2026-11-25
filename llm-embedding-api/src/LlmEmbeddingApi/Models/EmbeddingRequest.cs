using System.ComponentModel.DataAnnotations;

namespace LlmEmbeddingApi.Models;

/// <summary>
/// Request model for generating embeddings
/// </summary>
/// <param name="Text">The text to generate embeddings for</param>
/// <param name="Model">The model to use (optional, defaults to embeddinggemma)</param>
public sealed record EmbeddingRequest(
    [Required, StringLength(8192, MinimumLength = 1)] string Text,
    string Model = "embeddinggemma"
);