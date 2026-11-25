# EmbeddingGemma-300m Quick Commands

## ✅ No Prerequisites Needed!
Using `onnx-community/embeddinggemma-300m-ONNX` - a public ONNX model (no token required)

## Docker Compose (Easiest)

```powershell
# Just run - no token needed!
docker-compose up -d

# Logs
docker-compose logs -f

# Status
Invoke-RestMethod http://localhost:8080/api/model/status

# Test
$body = @{ text = "semantic search test"; model = "embeddinggemma" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/embed -Body $body -ContentType "application/json"

# Stop
docker-compose down
```

## Manual Docker Run

```powershell
docker run -d `
  --name llm-embedding-api `
  -p 8080:8080 `
  -v llm-models:/app/models `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__HuggingFaceRepoId=onnx-community/embeddinggemma-300m-ONNX' `
  -e 'Embedding__HuggingFaceFilename=onnx/model_q4.onnx' `
  -e 'Embedding__MaxSequenceLength=2048' `
  -e 'Embedding__EmbeddingDimension=768' `
  llm-embedding-api
```

## Local Development

```powershell
# Edit appsettings.Development.json with your token
# Then run:
cd src/LlmEmbeddingApi
dotnet run
```

## Model Information

✅ **Using**: `onnx-community/embeddinggemma-300m-ONNX` (quantized 4-bit)  
✅ **Format**: ONNX (ready to use)  
✅ **Size**: ~197 MB (model_q4.onnx)  
✅ **No Token Required**: Public model  
✅ **Embedding Dimension**: 768 (supports 128, 256, 512 via truncation)

## Using ONNX Alternative

```powershell
# Use a model that already has ONNX export
docker run -d `
  -p 8080:8080 `
  -v llm-models:/app/models `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__HuggingFaceRepoId=sentence-transformers/all-MiniLM-L6-v2' `
  -e 'Embedding__HuggingFaceFilename=onnx/model.onnx' `
  -e 'Embedding__MaxSequenceLength=256' `
  -e 'Embedding__EmbeddingDimension=384' `
  llm-embedding-api
```

## Model Options Available

The ONNX-community repo provides multiple quantized models:

| Model File | Size | Quality | Use Case |
|------------|------|---------|----------|
| `onnx/model_q4.onnx` | 197 MB | Good | **Default** - Best balance (current config) |
| `onnx/model_q4f16.onnx` | 175 MB | Good | Faster, slightly smaller |
| `onnx/model_quantized.onnx` | 309 MB | Better | More accurate |
| `onnx/model_fp16.onnx` | 617 MB | Best | Highest quality (FP16) |
| `onnx/model.onnx` | 1.23 GB | Best | Full precision (FP32) |

To use a different model, change `HuggingFaceFilename` in config:
```json
"HuggingFaceFilename": "onnx/model_fp16.onnx"  // for higher quality
```

## Files Updated
- ✅ `appsettings.json` - Production defaults (ONNX-community model_q4)
- ✅ `appsettings.Development.json` - Dev config (ONNX-community model_q4)
- ✅ `docker-compose.yml` - Complete Docker Compose setup (no token needed)
- ✅ `.env.example` - Environment variable template (token now optional)
- ✅ Updated `CONFIGURATION.md` - Quick reference
- ✅ Updated `README.md` - Added quick start

## Next Steps
1. Run `docker-compose up -d` (that's it!)
2. Test endpoints
3. Optional: Switch to different quantization level if needed

For complete details: [EMBEDDINGGEMMA_SETUP.md](EMBEDDINGGEMMA_SETUP.md)
