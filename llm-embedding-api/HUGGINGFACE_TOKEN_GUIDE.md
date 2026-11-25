# Hugging Face Token Configuration Guide

## Overview
The application now supports Hugging Face authentication tokens for downloading private models. This guide shows you exactly how to configure and use the token.

## How It Works
1. Token is stored in `EmbeddingOptions.HuggingFaceToken`
2. `ModelDownloader` reads it from the injected options
3. When downloading from `huggingface.co`, the token is automatically added as `Authorization: Bearer <token>` header
4. URLs are sanitized in logs to prevent token leakage

---

## Option 1: Environment Variable (Recommended for Docker/Azure)

### Local Docker Run
```powershell
docker run -d `
  --name llm-embedding-api `
  -p 8080:8080 `
  -v 'D:\llm-models:/app/models' `
  -e 'Embedding__HuggingFaceToken=hf_YOUR_TOKEN_HERE' `
  -e 'Embedding__HuggingFaceRepoId=google/gemma-2b' `
  -e 'Embedding__HuggingFaceFilename=model.onnx' `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__ModelPath=/app/models/embeddinggemma.onnx' `
  llm-embedding-api
```

### Azure Container Apps
Use secrets to store the token securely:

```bash
# Add the token as a secret
az containerapp secret set \
  --name my-containerapp \
  --resource-group my-rg \
  --secrets HF_TOKEN=hf_YOUR_TOKEN_HERE

# Set it as an environment variable
az containerapp update \
  --name my-containerapp \
  --resource-group my-rg \
  --set-env-vars "Embedding__HuggingFaceToken=secretref:HF_TOKEN"
```

Or in the Container App manifest:
```json
{
  "properties": {
    "configuration": {
      "secrets": [
        {
          "name": "hf-token",
          "value": "hf_YOUR_TOKEN_HERE"
        }
      ]
    },
    "template": {
      "containers": [
        {
          "name": "llm",
          "env": [
            {
              "name": "Embedding__HuggingFaceToken",
              "secretRef": "hf-token"
            },
            {
              "name": "Embedding__HuggingFaceRepoId",
              "value": "google/gemma-2b"
            },
            {
              "name": "Embedding__HuggingFaceFilename",
              "value": "model.onnx"
            }
          ]
        }
      ]
    }
  }
}
```

---

## Option 2: appsettings.json (Local Development Only)

**⚠️ WARNING: Never commit tokens to source control!**

For local development, add to `appsettings.Development.json` (which should be in `.gitignore`):

```json
{
  "Embedding": {
    "HuggingFaceToken": "hf_YOUR_TOKEN_HERE",
    "HuggingFaceRepoId": "google/gemma-2b",
    "HuggingFaceFilename": "model.onnx",
    "ModelPath": "/app/models/embeddinggemma.onnx",
    "AutoDownload": true
  }
}
```

---

## Option 3: Azure Key Vault (Most Secure for Production)

1. Store token in Azure Key Vault:
```bash
az keyvault secret set \
  --vault-name my-keyvault \
  --name HuggingFaceToken \
  --value "hf_YOUR_TOKEN_HERE"
```

2. Reference in Container App:
```json
{
  "env": [
    {
      "name": "Embedding__HuggingFaceToken",
      "value": "@Microsoft.KeyVault(SecretUri=https://my-keyvault.vault.azure.net/secrets/HuggingFaceToken/)"
    }
  ]
}
```

3. Ensure Container App has Managed Identity with Key Vault access.

---

## How to Get a Hugging Face Token

1. Go to https://huggingface.co/settings/tokens
2. Click "New token"
3. Give it a name (e.g., "production-model-download")
4. Select type: "Read" (sufficient for downloading models)
5. Click "Generate token"
6. Copy the token (starts with `hf_...`)

**Important:** Store this token securely. Treat it like a password.

---

## Configuration Priority

ASP.NET Core config follows this priority (higher overrides lower):
1. Command-line arguments
2. Environment variables (e.g., `Embedding__HuggingFaceToken`)
3. User secrets (Development only)
4. appsettings.{Environment}.json
5. appsettings.json

**Best Practice:** Use environment variables or Azure Key Vault in production.

---

## Testing Without a Token (Public Models)

If the model is public, you don't need a token. Just leave `HuggingFaceToken` empty or unset:

```powershell
docker run -d `
  --name llm-embedding-api `
  -p 8080:8080 `
  -e 'Embedding__HuggingFaceRepoId=sentence-transformers/all-MiniLM-L6-v2' `
  -e 'Embedding__HuggingFaceFilename=onnx/model.onnx' `
  -e 'Embedding__AutoDownload=true' `
  llm-embedding-api
```

---

## Troubleshooting

### 401 Unauthorized Error
- **Cause:** Token is missing or invalid for a private repo
- **Fix:** Ensure `HuggingFaceToken` is set correctly and the token has read access

### Token in Logs
- URLs are sanitized automatically to prevent leaking tokens
- Only the base URL is logged (before `?`)
- Authorization headers are never logged

### Direct URL Downloads (Non-HF)
- Token is only added for URLs containing `huggingface.co`
- For other sources, use `DownloadUrl` without authentication or implement custom auth

### Model Still Not Downloading
Check the logs:
```powershell
docker logs llm-embedding-api 2>&1 | Select-String "ModelDownloader"
```

---

## Security Checklist

- ✅ Use environment variables or secrets for tokens
- ✅ Never commit tokens to git
- ✅ Add `appsettings.Development.json` to `.gitignore`
- ✅ Use Azure Key Vault for production
- ✅ Rotate tokens periodically
- ✅ Use read-only tokens (not write/admin)
- ✅ Monitor token usage on Hugging Face dashboard

---

## Complete Example (Docker with All Options)

```powershell
# Create host directory
New-Item -ItemType Directory -Force -Path "D:\llm-models"

# Run with HF token for private model
docker run -d `
  --name llm-embedding-api `
  -p 8080:8080 `
  --restart unless-stopped `
  -v 'D:\llm-models:/app/models' `
  -e 'ASPNETCORE_ENVIRONMENT=Production' `
  -e 'Embedding__ModelPath=/app/models/embeddinggemma.onnx' `
  -e 'Embedding__MaxSequenceLength=512' `
  -e 'Embedding__EmbeddingDimension=768' `
  -e 'Embedding__AutoDownload=true' `
  -e 'Embedding__DownloadTimeoutMinutes=30' `
  -e 'Embedding__HuggingFaceRepoId=google/gemma-2b' `
  -e 'Embedding__HuggingFaceFilename=model.onnx' `
  -e 'Embedding__HuggingFaceToken=hf_YOUR_TOKEN_HERE' `
  llm-embedding-api

# Check logs
docker logs -f llm-embedding-api

# Verify model downloaded
docker exec llm-embedding-api ls -lh /app/models

# Test endpoint
$body = @{ text = "Hello world"; model = "embeddinggemma" } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8080/api/embed -Body $body -ContentType "application/json"
```
