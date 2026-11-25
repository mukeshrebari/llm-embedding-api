namespace LlmEmbeddingApi.Services;

/// <summary>
/// Service for downloading ML models
/// </summary>
public interface IModelDownloader
{
    /// <summary>
    /// Downloads a model if it doesn't exist locally
    /// </summary>
    /// <param name="modelPath">Local path where the model should be stored</param>
    /// <param name="downloadUrl">URL to download the model from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if download was successful or model already exists</returns>
    Task<bool> EnsureModelExistsAsync(string modelPath, string downloadUrl, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a model from Hugging Face Hub
    /// </summary>
    /// <param name="modelPath">Local path where the model should be stored</param>
    /// <param name="repoId">Hugging Face repository ID (e.g., "google/gemma-2b")</param>
    /// <param name="filename">Model filename in the repository</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if download was successful or model already exists</returns>
    Task<bool> DownloadFromHuggingFaceAsync(string modelPath, string repoId, string filename, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates that a downloaded model file is valid
    /// </summary>
    /// <param name="modelPath">Path to the model file</param>
    /// <returns>True if the model file is valid</returns>
    bool ValidateModel(string modelPath);
}