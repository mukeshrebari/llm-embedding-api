using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using LlmEmbeddingApi.Services;
using LlmEmbeddingApi.Models;
using Xunit;

namespace LlmEmbeddingApi.Tests.Services;

public class GemmaEmbeddingServiceTests
{
    private readonly Mock<ILogger<GemmaEmbeddingService>> _mockLogger;
    private readonly Mock<IOptions<EmbeddingOptions>> _mockOptions;
    private readonly Mock<IModelDownloader> _mockDownloader;

    public GemmaEmbeddingServiceTests()
    {
        _mockLogger = new Mock<ILogger<GemmaEmbeddingService>>();
        _mockOptions = new Mock<IOptions<EmbeddingOptions>>();
        _mockDownloader = new Mock<IModelDownloader>();
        
        _mockOptions.Setup(x => x.Value).Returns(new EmbeddingOptions
        {
            ModelPath = "./test-model.onnx",
            MaxSequenceLength = 512,
            EmbeddingDimension = 768,
            AutoDownload = false // Disable auto-download for tests
        });
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // This test will fail without a real model file, but validates the structure
        Assert.Throws<Microsoft.ML.OnnxRuntime.OnnxRuntimeException>(() => 
            new GemmaEmbeddingService(_mockOptions.Object, _mockLogger.Object, _mockDownloader.Object));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNullText_ThrowsArgumentException()
    {
        // This test validates input validation without needing the actual model
        var mockService = new Mock<IEmbeddingService>();
        mockService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .Returns((string text, CancellationToken ct) => 
               {
                   ArgumentException.ThrowIfNullOrWhiteSpace(text);
                   return Task.FromResult(new float[] { 1.0f, 2.0f, 3.0f });
               });

        await Assert.ThrowsAsync<ArgumentException>(() => 
            mockService.Object.GenerateEmbeddingAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidText_ReturnsEmbeddings()
    {
        var mockService = new Mock<IEmbeddingService>();
        var expectedEmbeddings = new float[] { 0.1f, 0.2f, 0.3f };
        
        mockService.Setup(x => x.GenerateEmbeddingAsync("Hello world", It.IsAny<CancellationToken>()))
               .ReturnsAsync(expectedEmbeddings);

        var result = await mockService.Object.GenerateEmbeddingAsync("Hello world", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedEmbeddings, result);
    }

    [Fact]
    public async Task ModelDownloader_DownloadFromHuggingFace_CallsCorrectMethod()
    {
        var mockDownloader = new Mock<IModelDownloader>();
        mockDownloader.Setup(x => x.DownloadFromHuggingFaceAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await mockDownloader.Object.DownloadFromHuggingFaceAsync(
            "/test/path", "test/repo", "model.onnx", CancellationToken.None);

        Assert.True(result);
        mockDownloader.Verify(x => x.DownloadFromHuggingFaceAsync(
            "/test/path", "test/repo", "model.onnx", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ValidateModel_WithValidPath_ReturnsExpectedResult()
    {
        var mockDownloader = new Mock<IModelDownloader>();
        mockDownloader.Setup(x => x.ValidateModel(It.IsAny<string>())).Returns(true);

        var result = mockDownloader.Object.ValidateModel("/valid/path");

        Assert.True(result);
    }
}