# Azure Container Instances Deployment Guide

## Overview

This guide walks through deploying the LLM Embedding API to Azure Container Instances (ACI) with a production-ready configuration featuring 2 CPU cores and 4GB RAM.

## Prerequisites

- **Azure CLI**: Version 2.0.55 or later ([Install](https://aka.ms/azure-cli))
- **Docker**: For building container images ([Install](https://docs.docker.com/get-docker/))
- **Azure Subscription**: Active subscription with sufficient quota
- **Azure Container Registry**: For hosting Docker images

## Architecture

```
┌─────────────────────────────────────────────┐
│   Azure Container Instance                  │
│                                             │
│  ┌──────────────────────────────────────┐  │
│  │  LLM Embedding API Container         │  │
│  │                                       │  │
│  │  - 2 CPU Cores                        │  │
│  │  - 4 GB RAM                           │  │
│  │  - Public IP with DNS                 │  │
│  │  - Health Probes (Liveness/Readiness)│  │
│  │  - Auto-restart on failure            │  │
│  └──────────────────────────────────────┘  │
│                                             │
│  Connected to:                              │
│  - Azure Container Registry (ACR)           │
│  - Log Analytics (optional)                 │
└─────────────────────────────────────────────┘
```

## Quick Start

### Option 1: Using PowerShell Script (Recommended)

```powershell
# Navigate to the infra directory
cd infra

# Deploy with auto-build
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry"

# Deploy with Hugging Face token (for private models)
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -HuggingFaceToken "hf_xxxxx"

# Preview deployment without executing (WhatIf)
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -WhatIf
```

If your Azure Container Registry is hosted in a different resource group than the deployment resource group, append `-ContainerRegistryResourceGroup "<rg-name>"` to the command above.

### Option 2: Manual Deployment Steps

#### Step 1: Create Azure Container Registry

```bash
# Set variables
RESOURCE_GROUP="llm-embedding-rg"
LOCATION="eastus"
ACR_NAME="myregistry"  # Must be globally unique

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create container registry
az acr create \
    --resource-group $RESOURCE_GROUP \
    --name $ACR_NAME \
    --sku Basic \
    --admin-enabled true
```

#### Step 2: Build and Push Docker Image

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Build and push image
docker build -t ${ACR_NAME}.azurecr.io/llm-embedding-api:latest -f Dockerfile .
docker push ${ACR_NAME}.azurecr.io/llm-embedding-api:latest
```

#### Step 3: Deploy to ACI

```bash
# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

# Deploy with Bicep (preview first)
az deployment group what-if \
    --name llm-embedding-deployment \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters infra/main.parameters.json \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:latest" \
    --parameters registryServer="${ACR_NAME}.azurecr.io" \
    --parameters registryUsername="$ACR_USERNAME" \
    --parameters registryPassword="$ACR_PASSWORD"

# Execute deployment
az deployment group create \
    --name llm-embedding-deployment \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters infra/main.parameters.json \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:latest" \
    --parameters registryServer="${ACR_NAME}.azurecr.io" \
    --parameters registryUsername="$ACR_USERNAME" \
    --parameters registryPassword="$ACR_PASSWORD"
```

## Configuration

### Environment Variables

The deployment automatically configures the following environment variables:

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | Production | Runtime environment |
| `ASPNETCORE_URLS` | http://+:8080 | Listening URLs |
| `Embedding__AutoDownload` | true | Auto-download ONNX model |
| `Embedding__MaxSequenceLength` | 2048 | Max token sequence length |
| `Embedding__EmbeddingDimension` | 768 | Output embedding dimensions |
| `Embedding__HuggingFaceRepoId` | onnx-community/embeddinggemma-300m-ONNX | Model repository |
| `Embedding__HuggingFaceFilename` | onnx/model_fp16.onnx | Model file path |
| `Embedding__HuggingFaceToken` | (secure) | HF API token (optional) |

### Resource Specifications

- **CPU**: 2 cores
- **Memory**: 4 GB
- **Storage**: 50 GB (ACI default)
- **Restart Policy**: Always
- **OS**: Linux

### Health Probes

**Liveness Probe**:
- Path: `/health`
- Initial Delay: 30 seconds
- Period: 30 seconds
- Failure Threshold: 3

**Readiness Probe**:
- Path: `/health`
- Initial Delay: 60 seconds (allows model download)
- Period: 10 seconds
- Failure Threshold: 3

## Monitoring and Logging

### View Container Logs

```bash
# View live logs
az container logs \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod \
    --follow

# View last 100 lines
az container logs \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod \
    --tail 100
```

### Check Container Status

```bash
az container show \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod \
    --query "{Status:instanceView.state,IP:ipAddress.ip,FQDN:ipAddress.fqdn}" \
    --output table
```

### Log Analytics Integration (Optional)

To enable Log Analytics for advanced monitoring:

```bash
# Create Log Analytics workspace
WORKSPACE_ID=$(az monitor log-analytics workspace create \
    --resource-group $RESOURCE_GROUP \
    --workspace-name llm-embedding-logs \
    --query customerId -o tsv)

WORKSPACE_KEY=$(az monitor log-analytics workspace get-shared-keys \
    --resource-group $RESOURCE_GROUP \
    --workspace-name llm-embedding-logs \
    --query primarySharedKey -o tsv)

# Redeploy with Log Analytics parameters
az deployment group create \
    --name llm-embedding-deployment-logs \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters infra/main.parameters.json \
    --parameters logAnalyticsWorkspaceId="$WORKSPACE_ID" \
    --parameters logAnalyticsWorkspaceKey="$WORKSPACE_KEY"
```

## Testing the Deployment

### Health Check

```bash
# Get the FQDN
FQDN=$(az container show \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod \
    --query ipAddress.fqdn -o tsv)

# Test health endpoint
curl http://${FQDN}:8080/health
```

### Generate Embeddings

```bash
# Test embedding endpoint
curl -X POST http://${FQDN}:8080/api/embed \
    -H "Content-Type: application/json" \
    -d '{"text":"Hello, this is a test message for embedding generation"}'
```

**PowerShell**:

```powershell
$fqdn = az container show `
    --resource-group "llm-embedding-rg" `
    --name "llm-embedding-api-prod" `
    --query "ipAddress.fqdn" -o tsv

$body = @{ text = "Hello, this is a test message" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://${fqdn}:8080/api/embed" -Method Post -Body $body -ContentType "application/json"
```

### Performance Metrics

```bash
# Get current metrics
curl http://${FQDN}:8080/api/metrics/current
```

## Cost Estimation

### Azure Container Instances Pricing (2 CPU / 4GB RAM)

- **CPU**: 2 cores × $0.0000125/second × 86,400 seconds/day = $2.16/day
- **Memory**: 4 GB × $0.0000014/second × 86,400 seconds/day = $0.48/day
- **Total**: ~$2.64/day or ~$79/month

**Note**: Actual costs may vary by region. Check [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/) for accurate estimates.

### Cost Optimization Tips

1. **Use scheduled start/stop**: Stop the container during off-hours
2. **Scale resources**: Reduce to 1 CPU / 2GB RAM for dev/test
3. **Review logs frequency**: Disable Log Analytics for non-production
4. **Use committed use discounts**: For long-running workloads

## Scaling Considerations

### Current Configuration Limits

Based on performance test results with 2 CPU / 4GB RAM:
- **Throughput**: ~1 request/second
- **Average Latency**: 1-2 seconds
- **P95 Latency**: 3-5 seconds
- **Memory Usage**: 600-700 MB peak
- **CPU Usage**: 20-30% average

### Scale-Up Options

For higher throughput requirements:

1. **Increase to 4 CPU / 8GB RAM**:
   ```bash
   --parameters cpuCores=4 memoryInGB=8
   ```
   - Expected throughput: ~2-3 req/sec
   - Cost: ~$5-6/day

2. **Deploy multiple instances with load balancer**:
   - Use Azure Application Gateway or Azure Front Door
   - Deploy 3-5 ACI instances
   - Horizontal scaling for high availability

3. **Migrate to Azure Container Apps or AKS**:
   - For production workloads >10 req/sec
   - Auto-scaling capabilities
   - Built-in load balancing

## Troubleshooting

### Container Fails to Start

```bash
# Check container events
az container show \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod \
    --query "instanceView.events" -o table

# View detailed logs
az container logs \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod
```

**Common Issues**:
- Model download timeout → Increase `initialDelaySeconds` in readiness probe
- Out of memory → Increase `memoryInGB` parameter
- Registry authentication → Verify ACR credentials

### Slow Response Times

1. Check if model auto-download is enabled
2. Verify model is cached in container
3. Review CPU/memory metrics
4. Test with smaller input texts

### Restart Container

```bash
az container restart \
    --resource-group $RESOURCE_GROUP \
    --name llm-embedding-api-prod
```

## Security Best Practices

### Secrets Management

**Option 1: Azure Key Vault (Recommended)**

```bash
# Store HF token in Key Vault
az keyvault create \
    --name llm-embedding-kv \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION

az keyvault secret set \
    --vault-name llm-embedding-kv \
    --name hf-token \
    --value "hf_xxxxx"

# Grant ACI access to Key Vault
# (Requires managed identity - see advanced configuration)
```

**Option 2: Deployment Parameters (Current)**

- Secrets passed as secure parameters
- Not visible in Azure Portal or logs
- Stored encrypted in ARM deployment history

### Network Security

Current deployment uses public IP. For production:

1. **Deploy in VNet**:
   - Modify Bicep template to use `ipAddress.type: 'Private'`
   - Add subnet delegation for ACI
   - Use Application Gateway for public access

2. **Enable HTTPS**:
   - Add TLS termination at Application Gateway
   - Use Azure Front Door for CDN and WAF

3. **Restrict Access**:
   - Use Network Security Groups
   - Configure IP allowlists
   - Enable Azure AD authentication

## Maintenance

### Update Deployment

```bash
# Build new image version
docker build -t ${ACR_NAME}.azurecr.io/llm-embedding-api:v2 -f Dockerfile .
docker push ${ACR_NAME}.azurecr.io/llm-embedding-api:v2

# Update deployment
az deployment group create \
    --name llm-embedding-update \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:v2"
```

### Backup and Disaster Recovery

ACI containers are stateless. To ensure reliability:

1. **Store models in Azure Storage**: Mount blob storage for model persistence
2. **Use ACR geo-replication**: Replicate images across regions
3. **Document deployment process**: Keep deployment scripts in source control
4. **Test recovery procedures**: Regularly validate redeploy process

## Advanced Configuration

### Custom Domain with HTTPS

```bash
# Use Azure Front Door or Application Gateway
az afd profile create --profile-name llm-embedding-cdn --resource-group $RESOURCE_GROUP
# Configure origin with ACI FQDN
# Add custom domain and SSL certificate
```

### Managed Identity

Update Bicep template to add:

```bicep
identity: {
  type: 'SystemAssigned'
}
```

Then grant access to Key Vault, Storage, etc.

## Support and Resources

- **Azure Documentation**: [Azure Container Instances Docs](https://learn.microsoft.com/azure/container-instances/)
- **Pricing**: [ACI Pricing](https://azure.microsoft.com/pricing/details/container-instances/)
- **Regional Availability**: [ACI Regions](https://azure.microsoft.com/global-infrastructure/services/?products=container-instances)
- **Quota Limits**: [ACI Resource Limits](https://learn.microsoft.com/azure/container-instances/container-instances-resource-and-quota-limits)

## Next Steps

1. ✅ Deploy to dev/test environment
2. ✅ Validate performance under load
3. ⬜ Configure Log Analytics for monitoring
4. ⬜ Set up alerts for failures and high latency
5. ⬜ Implement CI/CD pipeline (GitHub Actions / Azure DevOps)
6. ⬜ Add custom domain and HTTPS
7. ⬜ Deploy to production with auto-scaling (Container Apps/AKS)
