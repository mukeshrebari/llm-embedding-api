using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using LlmEmbeddingApi.Models;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Service for downloading ML models from various sources
/// </summary>
public sealed class ModelDownloader : IModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ModelDownloader> _logger;
    private readonly string? _huggingFaceToken;
    
    /// <summary>
    /// Initializes a new instance of the ModelDownloader class
    /// </summary>
    /// <param name="httpClient">HTTP client for downloading models</param>
    /// <param name="options">Embedding options containing HuggingFace token</param>
    /// <param name="logger">Logger instance</param>
    public ModelDownloader(HttpClient httpClient, IOptions<EmbeddingOptions> options, ILogger<ModelDownloader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _huggingFaceToken = options.Value.HuggingFaceToken;
    }

    /// <summary>
    /// Downloads a model if it doesn't exist locally
    /// </summary>
    public async Task<bool> EnsureModelExistsAsync(string modelPath, string downloadUrl, CancellationToken cancellationToken = default)
    {
        if (File.Exists(modelPath))
        {
            _logger.LogInformation("Model already exists at {ModelPath}", modelPath);
            
            if (ValidateModel(modelPath))
            {
                return true;
            }
            
            _logger.LogWarning("Existing model at {ModelPath} is invalid, re-downloading", modelPath);
            File.Delete(modelPath);
        }

        return await DownloadModelAsync(modelPath, downloadUrl, cancellationToken);
    }

    /// <summary>
    /// Downloads a model from Hugging Face Hub
    /// </summary>
    public async Task<bool> DownloadFromHuggingFaceAsync(string modelPath, string repoId, string filename, CancellationToken cancellationToken = default)
    {
        var downloadUrl = $"https://huggingface.co/{repoId}/resolve/main/{filename}";
        var success = await EnsureModelExistsAsync(modelPath, downloadUrl, cancellationToken);
        
        if (!success)
        {
            return false;
        }
        
        // ONNX models often come with a separate .onnx_data file containing weights
        // The .onnx_data file MUST have the same base name as the .onnx file
        // Example: if local file is "embeddinggemma-300m-fp16.onnx", 
        //          ONNX Runtime expects "embeddinggemma-300m-fp16.onnx_data"
        if (filename.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
        {
            // Get the base name from HuggingFace filename (e.g., "model_fp16" from "onnx/model_fp16.onnx")
            var hfBaseFilename = Path.GetFileNameWithoutExtension(filename); // "model_fp16"
            var dataFilename = filename + "_data"; // "onnx/model_fp16.onnx_data"
            var dataUrl = $"https://huggingface.co/{repoId}/resolve/main/{dataFilename}";
            
            // Get the local model directory and base filename
            var modelDirectory = Path.GetDirectoryName(modelPath);
            // Save companion data file using the remote data filename (base name only).
            // Some ONNX models reference an external data filename that differs from
            // the local ONNX filename. Use the remote data filename's basename so
            // the runtime can locate the companion file (e.g. "model_fp16.onnx_data").
            var localDataPath = Path.Combine(modelDirectory ?? ".", Path.GetFileName(dataFilename));
            
            _logger.LogInformation("Checking for companion data file: {DataFilename} -> {LocalPath}", dataFilename, localDataPath);
            
            // Try to download data file (might not exist for all models)
            try
            {
                var dataSuccess = await DownloadModelAsync(localDataPath, dataUrl, cancellationToken);
                if (dataSuccess)
                {
                    _logger.LogInformation("Successfully downloaded companion data file to {LocalPath}", localDataPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No companion data file found (this is normal for some models)");
            }
        }
        
        return true;
    }

    /// <summary>
    /// Validates that a downloaded model file is valid
    /// </summary>
    public bool ValidateModel(string modelPath)
    {
        try
        {
            if (!File.Exists(modelPath))
                return false;

            var fileInfo = new FileInfo(modelPath);
            
            // Check if file is not empty
            if (fileInfo.Length == 0)
                return false;

            // Check if file has ONNX magic number (basic validation)
            using var fileStream = File.OpenRead(modelPath);
            var buffer = new byte[8];
            var bytesRead = fileStream.Read(buffer, 0, 8);
            
            if (bytesRead < 8)
                return false;

            // ONNX files start with specific bytes
            // This is a basic check - could be enhanced with proper ONNX format validation
            return buffer[0] == 0x08 || // ONNX IR version
                   (buffer[0] == 0x50 && buffer[1] == 0x4B); // ZIP format (ONNX models can be zipped)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating model at {ModelPath}", modelPath);
            return false;
        }
    }

    private async Task<bool> DownloadModelAsync(string modelPath, string downloadUrl, CancellationToken cancellationToken)
    {
        try
        {
            // Sanitize URL for logging (hide tokens if present)
            var sanitizedUrl = downloadUrl.Contains("?") ? downloadUrl.Split('?')[0] + "?..." : downloadUrl;
            _logger.LogInformation("Downloading model from {DownloadUrl} to {ModelPath}", sanitizedUrl, modelPath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create request with Authorization header for Hugging Face if token is present
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            
            if (!string.IsNullOrEmpty(_huggingFaceToken) && downloadUrl.Contains("huggingface.co"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _huggingFaceToken);
                _logger.LogDebug("Added HuggingFace authorization header");
            }

            // Download with progress reporting
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var tempPath = modelPath + ".tmp";

            try
            {
                var totalBytesRead = 0L;
                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                {
                    using var fileStream = File.Create(tempPath);

                    var buffer = new byte[8192];
                    
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progress = (double)totalBytesRead / totalBytes * 100;
                            _logger.LogInformation("Download progress: {Progress:F1}% ({Downloaded}/{Total} bytes)",
                                progress, totalBytesRead, totalBytes);
                        }
                    }
                }
                // Move temp file to final location
                File.Move(tempPath, modelPath);
                
                _logger.LogInformation("Successfully downloaded model to {ModelPath} ({Size} bytes)", 
                    modelPath, totalBytesRead);

                return ValidateModel(modelPath);
            }
            finally
            {
                // Clean up temp file if it exists
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download model from {DownloadUrl}", downloadUrl);
            return false;
        }
    }
}