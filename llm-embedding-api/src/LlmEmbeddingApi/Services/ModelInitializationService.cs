using Microsoft.Extensions.Options;
using LlmEmbeddingApi.Models;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Background service to ensure models are downloaded during application startup
/// </summary>
public sealed class ModelInitializationService : BackgroundService
{
    private readonly IModelDownloader _modelDownloader;
    private readonly IOptions<EmbeddingOptions> _options;
    private readonly ILogger<ModelInitializationService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// Initializes a new instance of the ModelInitializationService class
    /// </summary>
    /// <param name="modelDownloader">Model downloader service</param>
    /// <param name="options">Embedding options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="lifetime">Application lifetime service</param>
    public ModelInitializationService(
        IModelDownloader modelDownloader,
        IOptions<EmbeddingOptions> options,
        ILogger<ModelInitializationService> logger,
        IHostApplicationLifetime lifetime)
    {
        _modelDownloader = modelDownloader;
        _options = options;
        _logger = logger;
        _lifetime = lifetime;
    }

    /// <summary>
    /// Executes the model initialization process
    /// </summary>
    /// <param name="stoppingToken">Cancellation token</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var embeddingOptions = _options.Value;
            
            if (!embeddingOptions.AutoDownload)
            {
                _logger.LogInformation("Auto-download is disabled, skipping model initialization");
                return;
            }

            if (File.Exists(embeddingOptions.ModelPath) && _modelDownloader.ValidateModel(embeddingOptions.ModelPath))
            {
                _logger.LogInformation("Valid model already exists at {ModelPath}", embeddingOptions.ModelPath);
                return;
            }

            _logger.LogInformation("Initializing model download...");

            var timeout = TimeSpan.FromMinutes(embeddingOptions.DownloadTimeoutMinutes);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(timeout);

            bool downloadSuccessful = false;

            // Try Hugging Face download first
            if (!string.IsNullOrEmpty(embeddingOptions.HuggingFaceRepoId) && 
                !string.IsNullOrEmpty(embeddingOptions.HuggingFaceFilename))
            {
                _logger.LogInformation("Downloading model from Hugging Face: {RepoId}/{Filename}", 
                    embeddingOptions.HuggingFaceRepoId, embeddingOptions.HuggingFaceFilename);

                downloadSuccessful = await _modelDownloader.DownloadFromHuggingFaceAsync(
                    embeddingOptions.ModelPath,
                    embeddingOptions.HuggingFaceRepoId,
                    embeddingOptions.HuggingFaceFilename,
                    cts.Token);
            }

            // Fallback to direct URL download
            if (!downloadSuccessful && !string.IsNullOrEmpty(embeddingOptions.DownloadUrl))
            {
                _logger.LogInformation("Downloading model from URL: {DownloadUrl}", embeddingOptions.DownloadUrl);

                downloadSuccessful = await _modelDownloader.EnsureModelExistsAsync(
                    embeddingOptions.ModelPath,
                    embeddingOptions.DownloadUrl,
                    cts.Token);
            }

            if (downloadSuccessful)
            {
                _logger.LogInformation("Model initialization completed successfully");
            }
            else
            {
                _logger.LogError("Failed to download model. The application may not function properly without a valid model.");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Model download was cancelled due to timeout or application shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model initialization");
        }
    }
}