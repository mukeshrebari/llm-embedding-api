using System.ComponentModel.DataAnnotations;

namespace LlmEmbeddingApi.Models;

/// <summary>
/// Configuration options for the embedding service
/// </summary>
public sealed class EmbeddingOptions
{
    /// <summary>
    /// Path to the ONNX model file
    /// </summary>
    [Required]
    public string ModelPath { get; set; } = "/app/models/embeddinggemma.onnx";

    /// <summary>
    /// Maximum sequence length for tokenization
    /// </summary>
    public int MaxSequenceLength { get; set; } = 512;

    /// <summary>
    /// Embedding dimension
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;

    /// <summary>
    /// URL to download the model from if it doesn't exist locally
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Hugging Face repository ID for model download (e.g., "google/gemma-2b")
    /// </summary>
    public string? HuggingFaceRepoId { get; set; }

    /// <summary>
    /// Model filename in Hugging Face repository
    /// </summary>
    public string? HuggingFaceFilename { get; set; }

    /// <summary>
    /// Hugging Face API token for accessing private repositories
    /// (Optional: only needed for private models)
    /// </summary>
    public string? HuggingFaceToken { get; set; }

    /// <summary>
    /// Whether to automatically download the model if it doesn't exist
    /// </summary>
    public bool AutoDownload { get; set; } = true;

    /// <summary>
    /// Timeout for model download operations in minutes
    /// </summary>
    public int DownloadTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Optional path to a native custom-op library that registers operators
    /// used by quantized models (for example: com.microsoft::GatherBlockQuantized).
    /// Provide the platform-specific shared library (.dll on Windows, .so on Linux)
    /// when using models that rely on custom-domain operators.
    /// </summary>
    public string? CustomOpLibraryPath { get; set; }
}