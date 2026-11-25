# Configuration Quick Reference

## Files Overview

| File | Purpose | When to Use |
|------|---------|-------------|
| `appsettings.json` | Production defaults | Base configuration with empty/safe values |
| `appsettings.Development.json` | Local dev overrides | Local development with auto-download enabled |
| `appsettings.Example.json` | Documentation & examples | Reference for all options and common scenarios |
| `HUGGINGFACE_TOKEN_GUIDE.md` | Token setup guide | When using private Hugging Face models |
| `SECURITY.md` | Auth & rate limiting reference | `Auth:*` / `RateLimiting:*` config keys, API key hashing, remote validation |

---

## Configuration Methods (Priority Order)

1. **Command-line arguments** (highest priority)
2. **Environment variables** (recommended for Docker/Azure)
3. **User secrets** (local development only)
4. **appsettings.{Environment}.json**
5. **appsettings.json** (lowest priority)

---

## Common Scenarios

### 1. Local Development (Public Model)
**File:** `appsettings.Development.json`
```json
{
  "Embedding": {
    "ModelPath": "./models/model.onnx",
    "AutoDownload": true,
    "HuggingFaceRepoId": "sentence-transformers/all-MiniLM-L6-v2",
    "HuggingFaceFilename": "onnx/model.onnx"
  }
}
```

### 2. Docker (Google EmbeddingGemma-300m - Gated Model)
```powershell
# NOTE: This model requires accepting the license at https://huggingface.co/google/embeddinggemma-300m
docker run -d -p 8080:8080 `
  -v 'D:\llm-models:/app/models' `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__HuggingFaceRepoId=google/embeddinggemma-300m' `
  -e 'Embedding__HuggingFaceFilename=model.safetensors' `
  -e 'Embedding__HuggingFaceToken=hf_your_token_here' `
  -e 'Embedding__MaxSequenceLength=2048' `
  -e 'Embedding__EmbeddingDimension=768' `
  llm-embedding-api
```

### 2b. Docker (Public Model - ONNX)
```powershell
docker run -d -p 8080:8080 `
  -v 'D:\llm-models:/app/models' `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__HuggingFaceRepoId=sentence-transformers/all-MiniLM-L6-v2' `
  -e 'Embedding__HuggingFaceFilename=onnx/model.onnx' `
  llm-embedding-api
```

### 3. Docker (Private Model with Token)
```powershell
docker run -d -p 8080:8080 `
  -v 'D:\llm-models:/app/models' `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__HuggingFaceRepoId=your-org/private-model' `
  -e 'Embedding__HuggingFaceFilename=model.onnx' `
  -e 'Embedding__HuggingFaceToken=hf_your_token_here' `
  llm-embedding-api
```

### 4. Azure Container Apps (with Key Vault)
```bash
# Store token in Key Vault
az keyvault secret set --vault-name my-vault --name HFToken --value "hf_..."

# Reference in Container App
az containerapp create \
  --name llm-api \
  --resource-group my-rg \
  --environment my-env \
  --image llm-embedding-api:latest \
  --env-vars \
    "Embedding__AutoDownload=true" \
    "Embedding__HuggingFaceRepoId=your-org/model" \
    "Embedding__HuggingFaceFilename=model.onnx" \
    "Embedding__HuggingFaceToken=secretref:hf-token"
```

### 5. Pre-existing Model (No Download)
```json
{
  "Embedding": {
    "ModelPath": "/app/models/existing-model.onnx",
    "AutoDownload": false
  }
}
```

---

## Environment Variable Examples

| Setting | Environment Variable | Example |
|---------|---------------------|---------|
| Model Path | `Embedding__ModelPath` | `/app/models/model.onnx` |
| Auto Download | `Embedding__AutoDownload` | `true` or `false` |
| HF Repo | `Embedding__HuggingFaceRepoId` | `sentence-transformers/all-MiniLM-L6-v2` |
| HF File | `Embedding__HuggingFaceFilename` | `onnx/model.onnx` |
| HF Token | `Embedding__HuggingFaceToken` | `hf_abc123...` (keep secret!) |
| Download URL | `Embedding__DownloadUrl` | `https://example.com/model.onnx` |
| Max Length | `Embedding__MaxSequenceLength` | `512` |
| Dimensions | `Embedding__EmbeddingDimension` | `768` |
| Timeout | `Embedding__DownloadTimeoutMinutes` | `30` |

---

## Recommended Embedding Models

| Model | Repo ID | Filename | Dimensions | Use Case |
|-------|---------|----------|------------|----------|
| **all-MiniLM-L6-v2** | `sentence-transformers/all-MiniLM-L6-v2` | `onnx/model.onnx` | 384 | Fast, general purpose |
| **all-mpnet-base-v2** | `sentence-transformers/all-mpnet-base-v2` | `onnx/model.onnx` | 768 | Higher quality |
| **bge-small-en-v1.5** | `BAAI/bge-small-en-v1.5` | `onnx/model.onnx` | 384 | Balanced speed/quality |
| **bge-base-en-v1.5** | `BAAI/bge-base-en-v1.5` | `onnx/model.onnx` | 768 | High quality |

> All models above are public and require no token

---

## Validation Checklist

Before deploying, verify:

- ✅ `ModelPath` is writable by the container/app
- ✅ If using HF: both `HuggingFaceRepoId` AND `HuggingFaceFilename` are set
- ✅ If private repo: `HuggingFaceToken` is set via env var or Key Vault
- ✅ `AutoDownload=true` if model doesn't exist yet
- ✅ `MaxSequenceLength` and `EmbeddingDimension` match your chosen model
- ✅ For Docker: volume is mounted to persist models (`-v host:container`)
- ✅ Tokens are NEVER in appsettings.json (use env vars/secrets)

---

## Troubleshooting

| Problem | Check | Solution |
|---------|-------|----------|
| "Failed to download model" | Logs for HTTP errors | Verify repo/filename, add token if private |
| 401 Unauthorized | HuggingFace token | Set `Embedding__HuggingFaceToken` correctly |
| Model downloads every restart | Volume mount | Ensure `-v` flag mounts persistent storage |
| "Model not found" | `ModelPath` value | Check path is correct and readable |
| Slow inference | Model size | Use smaller model (e.g., MiniLM instead of large) |

---

## Security Best Practices

1. **Never commit tokens** - Add `appsettings.Development.json` to `.gitignore`
2. **Use environment variables** for tokens in Docker/containers
3. **Use Key Vault** for production Azure deployments
4. **Read-only tokens** - Only grant read access on Hugging Face
5. **Rotate tokens** - Change tokens periodically
6. **Log sanitization** - Tokens are automatically hidden in logs

---

## Quick Commands

```powershell
# Build
dotnet build

# Run locally
dotnet run --project src/LlmEmbeddingApi

# Build Docker image
docker build -f Dockerfile -t llm-embedding-api .

# Check model status
Invoke-RestMethod http://localhost:8080/api/model/status

# Test embeddings
$body = @{ text = "Hello"; model = "embeddinggemma" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/embed -Body $body -ContentType "application/json"

# View logs
docker logs -f llm-embedding-api

# Exec into container
docker exec -it llm-embedding-api /bin/sh
```

---

## Need Help?

- **Hugging Face tokens**: See [HUGGINGFACE_TOKEN_GUIDE.md](HUGGINGFACE_TOKEN_GUIDE.md)
- **All config options**: See [appsettings.Example.json](appsettings.Example.json)
- **API endpoints**: See [README.md](README.md#api-endpoints)
- **Docker setup**: See [README.md](README.md#docker-deployment)
