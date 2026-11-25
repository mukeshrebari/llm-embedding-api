# EmbeddingGemma-300m Setup Guide

## Important Notes

⚠️ **Model Format**: The `google/embeddinggemma-300m` model is in **SafeTensors format**, NOT ONNX.  
⚠️ **Gated Model**: You MUST accept the license before downloading.  
⚠️ **Current Limitation**: This API currently expects ONNX models. You have two options:

### Option 1: Convert to ONNX (Recommended for Production)
Convert the SafeTensors model to ONNX format before use. See [conversion guide](#converting-to-onnx) below.

### Option 2: Use Alternative ONNX Model
Use a different model that already has ONNX export (see [alternatives](#onnx-alternatives) below).

---

## Quick Start with Docker Compose

### 1. Accept the Model License
1. Visit https://huggingface.co/google/embeddinggemma-300m
2. Log in to your HuggingFace account
3. Click "Access repository" and accept the terms

### 2. Get Your HuggingFace Token
1. Go to https://huggingface.co/settings/tokens
2. Create a new token (read access is sufficient)
3. Copy the token (starts with `hf_`)

### 3. Create .env File
```bash
# Copy the example
cp .env.example .env

# Edit and add your token
# .env file content:
HUGGINGFACE_TOKEN=hf_your_actual_token_here
```

### 4. Run with Docker Compose
```powershell
# Build and start
docker-compose up -d

# View logs
docker-compose logs -f

# Check model status
Invoke-RestMethod http://localhost:8080/api/model/status

# Test embedding
$body = @{ text = "Hello world"; model = "embeddinggemma" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/embed -Body $body -ContentType "application/json"

# Stop
docker-compose down
```

---

## Manual Docker Run (PowerShell)

```powershell
# Create volume or host directory
docker volume create llm-models
# OR for host bind mount:
# New-Item -ItemType Directory -Force -Path "D:\llm-models"

# Run container
docker run -d `
  --name llm-embedding-api `
  -p 8080:8080 `
  -v llm-models:/app/models `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__ModelPath=/app/models/embeddinggemma-300m.safetensors' `
  -e 'Embedding__HuggingFaceRepoId=google/embeddinggemma-300m' `
  -e 'Embedding__HuggingFaceFilename=model.safetensors' `
  -e 'Embedding__HuggingFaceToken=hf_YOUR_TOKEN_HERE' `
  -e 'Embedding__MaxSequenceLength=2048' `
  -e 'Embedding__EmbeddingDimension=768' `
  -e 'Embedding__DownloadTimeoutMinutes=60' `
  --restart unless-stopped `
  llm-embedding-api
```

---

## Converting to ONNX

The EmbeddingGemma model needs to be converted from SafeTensors to ONNX format. Here's how:

### Using Optimum (Recommended)

```bash
# Install required packages
pip install optimum[exporters] transformers sentence-transformers

# Convert model
optimum-cli export onnx \
  --model google/embeddinggemma-300m \
  --task feature-extraction \
  --optimize O3 \
  --batch_size 1 \
  ./models/embeddinggemma-300m-onnx/

# The ONNX model will be in ./models/embeddinggemma-300m-onnx/model.onnx
```

### Using Python Script

```python
from optimum.onnxruntime import ORTModelForFeatureExtraction
from transformers import AutoTokenizer

# Load and convert
model_id = "google/embeddinggemma-300m"
model = ORTModelForFeatureExtraction.from_pretrained(
    model_id,
    export=True,  # Convert to ONNX
    provider="CPUExecutionProvider"
)
tokenizer = AutoTokenizer.from_pretrained(model_id)

# Save to local directory
output_dir = "./models/embeddinggemma-300m-onnx"
model.save_pretrained(output_dir)
tokenizer.save_pretrained(output_dir)

print(f"ONNX model saved to: {output_dir}/model.onnx")
```

After conversion, update your configuration:
```json
{
  "Embedding": {
    "ModelPath": "/app/models/embeddinggemma-300m-onnx/model.onnx",
    "AutoDownload": false
  }
}
```

---

## ONNX Alternatives

If you prefer not to convert, these models have ONNX exports available:

### 1. all-MiniLM-L6-v2 (Small, Fast)
```json
{
  "HuggingFaceRepoId": "sentence-transformers/all-MiniLM-L6-v2",
  "HuggingFaceFilename": "onnx/model.onnx",
  "MaxSequenceLength": 256,
  "EmbeddingDimension": 384
}
```

### 2. all-mpnet-base-v2 (High Quality)
```json
{
  "HuggingFaceRepoId": "sentence-transformers/all-mpnet-base-v2",
  "HuggingFaceFilename": "onnx/model.onnx",
  "MaxSequenceLength": 384,
  "EmbeddingDimension": 768
}
```

### 3. bge-small-en-v1.5 (Balanced)
```json
{
  "HuggingFaceRepoId": "BAAI/bge-small-en-v1.5",
  "HuggingFaceFilename": "onnx/model.onnx",
  "MaxSequenceLength": 512,
  "EmbeddingDimension": 384
}
```

---

## Model Specifications

| Property | Value |
|----------|-------|
| **Size** | 300M parameters (~1.21 GB) |
| **Max Sequence Length** | 2048 tokens |
| **Embedding Dimension** | 768 (supports 128, 256, 512 via truncation) |
| **Languages** | 100+ |
| **Format** | SafeTensors (conversion to ONNX needed) |
| **License** | Gemma License (requires acceptance) |
| **Use Cases** | Semantic search, classification, clustering, retrieval, Q&A |

---

## Matryoshka Representation Learning (MRL)

EmbeddingGemma supports MRL, allowing you to use smaller embedding dimensions:

- **768d**: Full quality (default)
- **512d**: Slight quality trade-off, smaller size
- **256d**: Balanced speed/quality
- **128d**: Fastest, lower quality

To use smaller dimensions, truncate the output vector and re-normalize:

```csharp
// Truncate to 256 dimensions
var fullEmbedding = new float[768]; // From model
var truncated = fullEmbedding.Take(256).ToArray();

// Re-normalize
var magnitude = Math.Sqrt(truncated.Sum(x => x * x));
var normalized = truncated.Select(x => x / (float)magnitude).ToArray();
```

---

## Troubleshooting

### Download Fails with 401/403
- **Cause**: Token missing or license not accepted
- **Fix**: Ensure you accepted the license at https://huggingface.co/google/embeddinggemma-300m and token is from that account

### Model is SafeTensors, Not ONNX
- **Cause**: EmbeddingGemma is distributed as SafeTensors
- **Fix**: Convert to ONNX (see above) or use an ONNX-ready alternative model

### OnnxRuntimeException When Loading
- **Cause**: Trying to load SafeTensors with ONNX Runtime
- **Fix**: Convert model first or use ONNX alternative

### Download Times Out
- **Cause**: Large model (1.21 GB) + slow connection
- **Fix**: Increase `DownloadTimeoutMinutes` to 120 or pre-download locally

---

## Performance Tips

1. **Use Smaller Dimensions**: Truncate embeddings to 256 or 512 for faster inference
2. **Batch Requests**: Process multiple texts together when possible
3. **Cache Results**: Store frequently used embeddings
4. **CPU Optimization**: Use ONNX Runtime with optimized execution providers
5. **Resource Limits**: Set appropriate Docker memory/CPU limits (8GB+ recommended)

---

## Next Steps

1. ✅ Accept license on HuggingFace
2. ✅ Get HF token
3. ⚠️ Convert model to ONNX OR use alternative ONNX model
4. ✅ Configure app with token
5. ✅ Run and test

For more details, see:
- [HUGGINGFACE_TOKEN_GUIDE.md](HUGGINGFACE_TOKEN_GUIDE.md) - Token setup
- [CONFIGURATION.md](CONFIGURATION.md) - All config options
- [README.md](README.md) - API endpoints and usage
