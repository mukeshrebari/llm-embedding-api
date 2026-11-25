# LLM Embedding API

A .NET 9 minimal API for generating text embeddings using embedding models like Google EmbeddingGemma-300m, optimized for CPU execution in containerized environments.

## 💰 Cost-Effective POC Setup

**Perfect for resource-constrained projects!**

- **Cost:** $0/month (self-hosted, free model)
- **Hardware:** 4 CPU cores, 8GB RAM (no GPU needed)
- **Model:** EmbeddingGemma-300m (197 MB, 4-bit quantized)
- **Optimization:** 256d embeddings (3x faster than full 768d)

📊 See **`POC_OPTIMIZATION.md`** for complete POC strategy and **`POC_TO_PRODUCTION.md`** for migration path to fine-tuned models when you get funding.

## Quick Start

### Local Development

```powershell
# 1. Build the image
docker build -f Dockerfile -t llm-embedding-api .

# 2. Run with docker-compose (recommended)
# First: Copy .env.example to .env and add your HuggingFace token
docker-compose up -d

# 3. Test the API
Invoke-RestMethod http://localhost:8080/api/model/status
```

### Azure Deployment

Deploy to Azure Container Instances with 2 CPU cores and 4GB RAM:

```powershell
# Option 1: Public IP only (simple deployment)
cd infra
.\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry"

# Option 2: VNet integration (dual networking - private + public)
.\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" -DeployVnet

# Option 3: Private only (maximum security)
.\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId -EnablePublicIp $false
```

If your Azure Container Registry is located in a different resource group than your container group deployment, append `-ContainerRegistryResourceGroup "<rg-name>"` to the `Deploy-ACI.ps1` command.

📖 **Deployment Guides:**
- [Azure Deployment Guide](docs/AZURE_DEPLOYMENT_GUIDE.md) - Complete deployment documentation
- [VNet Integration Guide](docs/VNET_INTEGRATION_GUIDE.md) - Private networking and dual-mode configuration
- [ACI Quick Reference](docs/ACI_QUICK_REFERENCE.md) - Quick commands and troubleshooting
- [EmbeddingGemma Setup](EMBEDDINGGEMMA_SETUP.md) - Model-specific configuration

## Features

- **Modern .NET 9 & C# 13**: Uses latest language features including record types, primary constructors, and minimal APIs
- **CPU-Optimized**: Runs efficiently on CPU without GPU requirements using ONNX Runtime
- **Container-Ready**: Docker and docker-compose support for easy deployment
- **Auto-Download**: Automatically downloads models from Hugging Face
- **Gated Model Support**: Handles HuggingFace authentication for private/gated models
- **Health Checks**: Built-in health monitoring
- **OpenAPI/Swagger**: Full API documentation
- **Global Exception Handling**: Structured error handling
- **Authentication & Rate Limiting**: API key auth (static hashes and/or remote org-service validation) plus per-key rate limiting — see [SECURITY.md](SECURITY.md)

## Authentication

All `/api/*` endpoints require an API key, **except** `GET /api/model/status` (kept
anonymous alongside `/health` as a readiness signal). Provide the key via either header:

```bash
curl -H "x-api-key: <your-key>" http://localhost:8080/api/models
# or
curl -H "Authorization: Bearer <your-key>" http://localhost:8080/api/models
```

Keys are configured as SHA-256 hashes only (never plaintext) under `Auth:ApiKeys` — see
[SECURITY.md](SECURITY.md) for the full authentication flow, the optional remote
validation mode against the organization service, rate limiting, and the complete config
key reference. Compute a key's hash with [`tools/hash-key`](tools/hash-key).

## API Endpoints

### `POST /api/embed`
Generate embeddings for text input.

**Request:**
```json
{
  "text": "Your text to embed",
  "model": "embeddinggemma"
}
```

**Response:**
```json
{
  "embeddings": [0.1, 0.2, ...],
  "tokenCount": 5,
  "model": "embeddinggemma"
}
```

### `GET /api/models`
Get list of available models.

**Response:**
```json
{
  "models": ["embeddinggemma"]
}
```

### `GET /api/model/status`
*(Anonymous — no API key required)*

Get model download status and configuration.

**Response:**
```json
{
  "modelPath": "/app/models/embeddinggemma.onnx",
  "exists": true,
  "isValid": true,
  "fileSizeBytes": 245760000,
  "autoDownload": true,
  "downloadSources": {
    "huggingFaceRepoId": "sentence-transformers/all-MiniLM-L6-v2",
    "huggingFaceFilename": "onnx/model.onnx",
    "downloadUrl": "https://example.com/models/embeddinggemma.onnx"
  }
}
```

### `POST /api/model/download`
Trigger manual model download.

**Response:**
```json
{
  "message": "Model downloaded successfully",
  "modelPath": "/app/models/embeddinggemma.onnx"
}
```

### `GET /health`
*(Anonymous — no API key required)*

Health check endpoint.

## Setup

### Prerequisites
- .NET 9 SDK
- Docker (optional)
- Gemma ONNX model file

### Local Development
```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run --project src/LlmEmbeddingApi

# The API will be available at https://localhost:5001
```

### Docker Deployment
```bash
# Build the Docker image
docker build -f Dockerfile -t llm-embedding-api .

# Run the container
docker run -p 8080:8080 \
  -v /path/to/models:/app/models \
  llm-embedding-api
```

## Configuration

The application uses `appsettings.json` for default configuration. See `appsettings.Example.json` for comprehensive examples.

### Default Configuration (`appsettings.json`)
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma.onnx",
    "MaxSequenceLength": 512,
    "EmbeddingDimension": 768,
    "AutoDownload": false,
    "DownloadTimeoutMinutes": 30,
    "HuggingFaceRepoId": "",
    "HuggingFaceFilename": "",
    "HuggingFaceToken": "",
    "DownloadUrl": "",
    "CustomOpLibraryPath": ""
  }
}
```

### Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| **ModelPath** | string | `/app/models/embeddinggemma.onnx` | Local path where the ONNX model is stored or will be downloaded |
| **MaxSequenceLength** | int | 512 | Maximum number of tokens to process (longer sequences are truncated) |
| **EmbeddingDimension** | int | 768 | Output embedding vector dimension (must match your model) |
| **AutoDownload** | bool | false | Automatically download the model if it doesn't exist locally |
| **DownloadTimeoutMinutes** | int | 30 | Maximum time to wait for model download before timing out |
| **HuggingFaceRepoId** | string | empty | Hugging Face repository ID (e.g., "sentence-transformers/all-MiniLM-L6-v2") |
| **HuggingFaceFilename** | string | empty | Model filename in the Hugging Face repository (e.g., "onnx/model.onnx") |
| **HuggingFaceToken** | string | empty | API token for private Hugging Face repos (starts with "hf_"). See [Token Guide](HUGGINGFACE_TOKEN_GUIDE.md) |
| **CustomOpLibraryPath** | string | empty | Path to native custom-op library (.dll/.so) for models that require custom-domain operators (e.g., com.microsoft::GatherBlockQuantized) |
| **DownloadUrl** | string | empty | Direct HTTPS URL to download the model from (fallback option) |

### Using Environment Variables

Override any setting using environment variables with double underscore notation:

```bash
# PowerShell
$env:Embedding__ModelPath = "/app/models/mymodel.onnx"
$env:Embedding__HuggingFaceToken = "hf_your_token_here"
$env:Embedding__AutoDownload = "true"

# Docker
docker run -e 'Embedding__AutoDownload=true' \
  -e 'Embedding__HuggingFaceRepoId=sentence-transformers/all-MiniLM-L6-v2' \
  -e 'Embedding__HuggingFaceFilename=onnx/model.onnx' \
  llm-embedding-api
```

### Quick Start Configurations

**Public Model (No Token Required)**
```json
{
  "Embedding": {
    "AutoDownload": true,
    "HuggingFaceRepoId": "sentence-transformers/all-MiniLM-L6-v2",
    "HuggingFaceFilename": "onnx/model.onnx",
    "MaxSequenceLength": 256,
    "EmbeddingDimension": 384
  }
}
```

**Private Model (Token Required)**
```bash
# Use environment variable for security
export Embedding__HuggingFaceToken="hf_your_token_here"
export Embedding__AutoDownload="true"
export Embedding__HuggingFaceRepoId="your-org/private-model"
export Embedding__HuggingFaceFilename="model.onnx"
```

**Pre-existing Local Model (No Download)**
```json
{
  "Embedding": {
    "ModelPath": "./models/my-local-model.onnx",
    "AutoDownload": false,
    "MaxSequenceLength": 512,
    "EmbeddingDimension": 768
  }
}
```

> **Security Note:** Never commit tokens to source control! Use environment variables or Azure Key Vault. See [HUGGINGFACE_TOKEN_GUIDE.md](HUGGINGFACE_TOKEN_GUIDE.md) for details

### Download Priority
1. Check if model exists locally and is valid
2. If not found and AutoDownload is enabled:
   - Try downloading from Hugging Face (if configured)
   - Fallback to direct URL download (if configured)
3. Validate downloaded model

### Environment Variables
- `ASPNETCORE_URLS`: Set to `http://+:8080` for container deployment
- `ASPNETCORE_ENVIRONMENT`: Set to `Production` or `Development`

## Model Requirements

The service supports automatic model downloading from multiple sources:

### Option 1: Hugging Face Hub
Configure Hugging Face repository details in your settings:
```json
{
  "Embedding": {
    "HuggingFaceRepoId": "sentence-transformers/all-MiniLM-L6-v2",
    "HuggingFaceFilename": "onnx/model.onnx"
  }
}
```

### Option 2: Direct URL
Provide a direct download URL:
```json
{
  "Embedding": {
    "DownloadUrl": "https://your-server.com/models/embeddinggemma.onnx"
  }
}
```

### Option 3: Manual Placement
Place your `embeddinggemma.onnx` model file manually in the configured model path.

### Model Requirements
- ONNX-compatible embedding model
- Input: `input_ids` tensor of shape `[batch_size, sequence_length]`
- Output: embedding vector of configured dimension
- Supported models: Sentence Transformers, Gemma embeddings, custom ONNX models

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Performance Features

- **CPU-optimized ONNX Runtime**: Efficient inference without GPU requirements
- **Response caching**: Cached responses for repeated requests (5-minute default)
- **Minimal memory footprint**: Optimized for container environments
- **Efficient tokenization**: Simple byte-level tokenization with configurable limits

## API Usage Examples

### Using curl
```bash
# Generate embeddings
curl -X POST "http://localhost:8080/api/embed" \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello world", "model": "embeddinggemma"}'

# Get available models
curl -X GET "http://localhost:8080/api/models"

# Health check
curl -X GET "http://localhost:8080/health"
```

### Using C#
```csharp
using var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:8080");

var request = new { text = "Hello world", model = "embeddinggemma" };
var response = await client.PostAsJsonAsync("/api/embed", request);
var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
```

## Architecture

The application follows modern .NET practices:
- **Minimal APIs**: Lightweight endpoint definitions
- **Dependency Injection**: Built-in DI container
- **Configuration**: Options pattern with validation
- **Logging**: Structured logging with Microsoft.Extensions.Logging
- **Health Checks**: ASP.NET Core health check framework
- **Exception Handling**: Global exception handler

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Run tests and ensure they pass
6. Submit a pull request

## License

This project is licensed under the Apache License 2.0. See the [LICENSE](../LICENSE) file for details.

The embedding model (EmbeddingGemma) is distributed separately by Google under its own terms; review them before use.
