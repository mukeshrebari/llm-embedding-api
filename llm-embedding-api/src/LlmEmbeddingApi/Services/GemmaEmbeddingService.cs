using Google.Protobuf; // Required for parsing protobuf models
using LlmEmbeddingApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LlmEmbeddingApi.Services;

/// <summary>
/// Gemma-based embedding service using ONNX Runtime
/// </summary>
public sealed class GemmaEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly InferenceSession _session;
    private readonly ILogger<GemmaEmbeddingService> _logger;
    private readonly EmbeddingOptions _options;
    private readonly PerformanceMetricsService? _metricsService;
    private readonly SemaphoreSlim _inferenceLock;
    private readonly int _maxConcurrency;
    private const int MaxBatchSize = 16;
    private bool _isModelLoaded;
    private string? _modelLoadError;
    private readonly Tokenizer? _tokenizer;
    //private readonly IMemoryCache? _cache;
    /// <summary>
    /// Initializes a new instance of the GemmaEmbeddingService class
    /// </summary>
    /// <param name="options">Embedding configuration options</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="modelDownloader">Model downloader service</param>
    /// <param name="metricsService">Performance metrics service (optional)</param>
    public GemmaEmbeddingService(
        IOptions<EmbeddingOptions> options,
        ILogger<GemmaEmbeddingService> logger,
        IModelDownloader modelDownloader,
    PerformanceMetricsService? metricsService = null)
    {
        _options = options.Value;
        _logger = logger;
        _metricsService = metricsService;
        _maxConcurrency = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        _inferenceLock = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        _logger.LogInformation("ONNX inference concurrency set to {Concurrency} based on hardware", _maxConcurrency);
        
        try
        {
            //_tokenizer = Microsoft.ML.Tokenizers.Tokenizer
            // Ensure model exists, download if necessary
            if (_options.AutoDownload && !File.Exists(_options.ModelPath))
            {
                var downloadSuccessful = false;
                var timeout = TimeSpan.FromMinutes(_options.DownloadTimeoutMinutes);
                using var cts = new CancellationTokenSource(timeout);

                if (!string.IsNullOrEmpty(_options.HuggingFaceRepoId) && !string.IsNullOrEmpty(_options.HuggingFaceFilename))
                {
                    _logger.LogInformation("Attempting to download model from Hugging Face: {RepoId}/{Filename}", 
                        _options.HuggingFaceRepoId, _options.HuggingFaceFilename);
                    
                    downloadSuccessful = modelDownloader.DownloadFromHuggingFaceAsync(
                        _options.ModelPath, 
                        _options.HuggingFaceRepoId, 
                        _options.HuggingFaceFilename, 
                        cts.Token).GetAwaiter().GetResult();
                }
                else if (!string.IsNullOrEmpty(_options.DownloadUrl))
                {
                    _logger.LogInformation("Attempting to download model from URL: {DownloadUrl}", _options.DownloadUrl);
                    
                    downloadSuccessful = modelDownloader.EnsureModelExistsAsync(
                        _options.ModelPath, 
                        _options.DownloadUrl, 
                        cts.Token).GetAwaiter().GetResult();
                }

                if (!downloadSuccessful)
                {
                    throw new InvalidOperationException(
                        $"Failed to download model. Please ensure the model exists at {_options.ModelPath} or configure valid download options.");
                }
            }
            // Load from tokenizer.json
            // Load tokenizer.model file from same directory as ONNX model
            var tokenizerModelPath = Path.Combine(
                Path.GetDirectoryName(_options.ModelPath)!,
                "tokenizer.model"
            );
            if (File.Exists(tokenizerModelPath))
            {
                using var stream = File.OpenRead(tokenizerModelPath);
                _tokenizer = Microsoft.ML.Tokenizers.SentencePieceTokenizer.Create(
                    stream,
                    addBeginningOfSentence: false,
                    addEndOfSentence: false);
                logger.LogInformation("SentencePiece tokenizer loaded from {Path}", tokenizerModelPath);
            }
            else
            {
                _logger.LogWarning("Tokenizer model not found at {Path}, using fallback tokenization",
                    tokenizerModelPath);
                _tokenizer = null; // Will use byte-level fallback
            }
            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = true,
                
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,

                // Use sequential execution to avoid native-level parallelism issues with some CPU providers
                // and custom operator libraries. If you need parallel execution, validate it thoroughly.
                ExecutionMode = ExecutionMode.ORT_PARALLEL,
                //InterOpNumThreads = 1,
                IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
                                       // Enable profiling to identify bottlenecks (dev only)
                EnableProfiling = false,

                // Optimize graph before first run
                OptimizedModelFilePath = Path.Combine(
                    Path.GetDirectoryName(_options.ModelPath)!,
                    "optimized_model.onnx"
                ),
                LogSeverityLevel= OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
            };

            // If a custom operator native library is provided, register it before creating the session.
            // This is required for quantized models that rely on custom-domain ops (e.g. com.microsoft::GatherBlockQuantized).
            if (!string.IsNullOrEmpty(_options.CustomOpLibraryPath))
            {
                try
                {
                    var customLibPath = _options.CustomOpLibraryPath;
                    if (File.Exists(customLibPath))
                    {
                        sessionOptions.RegisterCustomOpLibrary(customLibPath);
                        _logger.LogInformation("Registered ONNX custom-op library from {Path}", customLibPath);
                    }
                    else
                    {
                        _logger.LogWarning("Custom op library specified but not found at path: {Path}", customLibPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register custom op library from {Path}", _options.CustomOpLibraryPath);
                    throw;
                }
            }

            // Use CPU provider for non-GPU containers
            sessionOptions.AppendExecutionProvider_CPU(1);
            sessionOptions.EnableMemoryPattern = true;

            _session = new InferenceSession(_options.ModelPath, sessionOptions);
            _logger.LogInformation("Embedding model loaded successfully from {ModelPath}", _options.ModelPath);
            _isModelLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load embedding model from {ModelPath}", _options.ModelPath);
            _isModelLoaded = false;
            _modelLoadError = ex.Message;
            throw;
        }
    }
        
    private static string ComputeHash(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hashBytes);
    }
    /// <summary>
    /// Indicates whether the model was successfully loaded and is ready for inference.
    /// </summary>
    public bool IsModelLoaded => _isModelLoaded;

    /// <summary>
    /// If the model failed to load, this contains the error message; otherwise null.
    /// </summary>
    public string? ModelLoadError => _modelLoadError;

    /// <summary>
    /// Generate embeddings for the provided text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var result = await GenerateEmbeddingsBatchAsync(new[] { text }, cancellationToken);
        return result.Embeddings[0];
    }

    /// <summary>
    /// Generate embeddings for a collection of texts in one batch.
    /// </summary>
    public async Task<EmbeddingBatchResult> GenerateEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
        {
            throw new ArgumentException("At least one text is required", nameof(texts));
        }

        if (texts.Count > MaxBatchSize)
        {
            throw new ArgumentException($"Batch size {texts.Count} exceeds limit of {MaxBatchSize}", nameof(texts));
        }

        var stopwatch = Stopwatch.StartNew();
        var isError = false;
        long[]? inputIds = null;
        long[]? attentionMask = null;

        try
        {
            var sanitizedTexts = new List<string>(texts.Count);
            foreach (var text in texts)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(text);
                sanitizedTexts.Add(SanitizeInput(text));
            }

            var tokenized = new List<long[]>(sanitizedTexts.Count);
            var tokenCounts = new int[sanitizedTexts.Count];
            for (int i = 0; i < sanitizedTexts.Count; i++)
            {
                var tokens = TokenizeText(sanitizedTexts[i]);
                tokenized.Add(tokens);
                tokenCounts[i] = tokens.Length;
            }

            var sequenceLength = tokenized.Max(t => t.Length);
            if (sequenceLength == 0)
            {
                sequenceLength = 1;
            }

            var batchSize = tokenized.Count;
            var totalElements = batchSize * sequenceLength;

            // Rent pooled buffers instead of allocating. Rented arrays may contain stale
            // data so we zero them before use — padding positions must be 0 for both
            // input_ids (pad token) and attention_mask (ignore position).
            inputIds = ArrayPool<long>.Shared.Rent(totalElements);
            attentionMask = ArrayPool<long>.Shared.Rent(totalElements);
            Array.Clear(inputIds, 0, totalElements);
            Array.Clear(attentionMask, 0, totalElements);

            for (int b = 0; b < batchSize; b++)
            {
                var tokens = tokenized[b];
                var offset = b * sequenceLength;

                Array.Copy(tokens, 0, inputIds, offset, tokens.Length);
                for (int i = 0; i < tokens.Length; i++)
                {
                    attentionMask[offset + i] = 1;
                }
            }

            // Wrap pooled arrays in Memory<T> so DenseTensor references the same buffer
            // without copying. The arrays must remain live until after _session.Run().
            var inputTensor = new DenseTensor<long>(inputIds.AsMemory(0, totalElements), new[] { batchSize, sequenceLength });
            var attentionMaskTensor = new DenseTensor<long>(attentionMask.AsMemory(0, totalElements), new[] { batchSize, sequenceLength });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
            };

            _logger.LogDebug("Waiting to enter ONNX inference lock (max concurrency: {MaxConcurrency})", _maxConcurrency);
            await _inferenceLock.WaitAsync(cancellationToken);
            _logger.LogDebug("Entered ONNX inference lock (current slots available: {Available})", _inferenceLock.CurrentCount);

            try
            {
                _logger.LogDebug("Starting ONNX session Run for batch size {BatchSize}", batchSize);
                using var results = await Task.Run(() => _session.Run(inputs), cancellationToken);
                _logger.LogDebug("Completed ONNX session Run for batch size {BatchSize}", batchSize);

                var outputTensor = (DenseTensor<float>)results.First().AsTensor<float>();
                var outputShape = outputTensor.Dimensions.ToArray();
                var outputSpan = outputTensor.Buffer.Span;

                _logger.LogDebug("Model output shape: [{Shape}]", string.Join(", ", outputShape));

                var embeddings = new List<float[]>(batchSize);

                if (outputShape.Length == 2)
                {
                    var embeddingDim = outputShape[1];
                    for (int b = 0; b < batchSize; b++)
                    {
                        // Slice directly into the tensor's contiguous memory — no per-element
                        // indexer overhead.
                        var rowSpan = outputSpan.Slice(b * embeddingDim, embeddingDim);
                        embeddings.Add(PrepareEmbedding(rowSpan));
                    }
                }
                else if (outputShape.Length == 3)
                {
                    var seqLen = outputShape[1];
                    var hiddenDim = outputShape[2];
                    var temp = ArrayPool<float>.Shared.Rent(hiddenDim);

                    try
                    {
                        for (int b = 0; b < batchSize; b++)
                        {
                            var validSteps = Math.Min(seqLen, Math.Max(1, tokenCounts[b]));
                            Array.Clear(temp, 0, hiddenDim);

                            // Outer loop over sequence positions keeps reads of outputSpan
                            // sequential (row-major), which is cache-friendly.
                            for (int j = 0; j < validSteps; j++)
                            {
                                var rowSpan = outputSpan.Slice((b * seqLen + j) * hiddenDim, hiddenDim);
                                for (int i = 0; i < hiddenDim; i++)
                                {
                                    temp[i] += rowSpan[i];
                                }
                            }

                            for (int i = 0; i < hiddenDim; i++)
                            {
                                temp[i] /= validSteps;
                            }

                            _logger.LogDebug("Applied mean pooling over sequence length {SeqLen} for batch item {Index}", validSteps, b);
                            embeddings.Add(PrepareEmbedding(temp.AsSpan(0, hiddenDim)));
                        }
                    }
                    finally
                    {
                        ArrayPool<float>.Shared.Return(temp);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unexpected output tensor shape: [{string.Join(", ", outputShape)}]. Expected 2D or 3D tensor.");
                }

                return new EmbeddingBatchResult(embeddings, tokenCounts);
            }
            finally
            {
                _inferenceLock.Release();
                _logger.LogDebug("Released ONNX inference lock");
            }
        }
        catch (Exception ex)
        {
            isError = true;
            _logger.LogError(ex, "Error generating embeddings for batch size {BatchSize}", texts.Count);
            throw;
        }
        finally
        {
            // Return pooled buffers after inference is complete and results have been
            // extracted. Clearing is skipped here because we clear on Rent instead.
            if (inputIds is not null) ArrayPool<long>.Shared.Return(inputIds);
            if (attentionMask is not null) ArrayPool<long>.Shared.Return(attentionMask);

            stopwatch.Stop();
            var totalLength = texts.Sum(t => t?.Length ?? 0);
            _metricsService?.RecordEmbeddingRequest(
                stopwatch.Elapsed.TotalMilliseconds,
                totalLength,
                isError);
        }
    }

    private float[] PrepareEmbedding(ReadOnlySpan<float> rawEmbeddings)
    {
        var outputDimensions = rawEmbeddings.Length;
        var requestedDimensions = _options.EmbeddingDimension;

        if (requestedDimensions < outputDimensions)
        {
            var result = new float[requestedDimensions];
            rawEmbeddings.Slice(0, requestedDimensions).CopyTo(result);
            NormalizeEmbedding(result);
            _logger.LogDebug("Truncated embeddings from {From}d to {To}d", outputDimensions, requestedDimensions);
            return result;
        }

        return rawEmbeddings.ToArray();
    }

    private static void NormalizeEmbedding(float[] embedding)
    {
        // L2 normalization for truncated embeddings
        var sumSquares = 0.0;
        for (int i = 0; i < embedding.Length; i++)
        {
            sumSquares += embedding[i] * embedding[i];
        }
        
        var magnitude = Math.Sqrt(sumSquares);
        if (magnitude > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)(embedding[i] / magnitude);
            }
        }
    }

    /// <summary>
    /// Sanitizes input text by normalizing whitespace and removing control characters.
    /// Newlines and tabs are preserved but normalized.
    /// </summary>
    private static string SanitizeInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var sb = new StringBuilder(text.Length);
        
        foreach (char c in text)
        {
            // Allow printable characters, newlines, tabs, and spaces
            if (c >= 32 || c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(c);
            }
            // Replace other control characters with space
            else if (char.IsControl(c))
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }
        
        // Normalize consecutive whitespace (but preserve single newlines/tabs)
        var result = sb.ToString();
        
        // Optional: Trim excessive whitespace at start/end
        result = result.Trim();
        
        return result;
    }
    private long[] TokenizeText(string text)
    {
        if (_tokenizer != null)
        {
            // Use proper SentencePiece tokenizer
            var ids = _tokenizer.EncodeToIds(text);
            var maxLength = Math.Min(ids.Count, _options.MaxSequenceLength);
            var tokens = new long[maxLength];

            for (int i = 0; i < maxLength; i++)
            {
                tokens[i] = ids[i];
            }

            if (ids.Count > _options.MaxSequenceLength)
            {
                _logger.LogWarning("Text truncated from {Original} to {Max} tokens",
                    ids.Count, _options.MaxSequenceLength);
            }

            return tokens;
        }
        else
        {
            // Fallback to byte-level tokenization (POC only)
            _logger.LogDebug("Using fallback byte-level tokenization (POC mode)");
            //var bytes = Encoding.UTF8.GetBytes(text);
            //var maxLength = Math.Min(bytes.Length, _options.MaxSequenceLength);
            //var tokens = new long[maxLength];

            //for (int i = 0; i < maxLength; i++)
            //{
            //    tokens[i] = i < bytes.Length ? bytes[i] : 0;
            //}

            //return tokens;
            return TokenizeTextFallback(text);
        }
    }
    private long[] TokenizeTextFallback(string text)
    {
        // WARNING: Simplified byte-level tokenization for POC
        // TODO: Replace with proper tokenizer (e.g., SentencePiece/Tokenizers library)
        // This is NOT production-ready and will affect embedding quality!
        
        var bytes = Encoding.UTF8.GetBytes(text);
        var maxLength = Math.Min(bytes.Length, _options.MaxSequenceLength);
        var tokens = new long[maxLength];
        
        for (int i = 0; i < maxLength; i++)
        {
            tokens[i] = i < bytes.Length ? bytes[i] : 0;
        }
        
        if (bytes.Length > _options.MaxSequenceLength)
        {
            _logger.LogWarning("Text truncated from {Original} to {Max} bytes", 
                bytes.Length, _options.MaxSequenceLength);
        }
        
        return tokens;
    }

    /// <summary>
    /// Disposes the ONNX Runtime session
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
        _inferenceLock?.Dispose();
    }
}
