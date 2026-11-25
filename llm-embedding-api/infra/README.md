# Infrastructure as Code - Azure Deployment

This directory contains Azure deployment infrastructure for the LLM Embedding API.

## Contents

- **`main.bicep`**: Main Bicep template for Azure Container Instances deployment
- **`vnet.bicep`**: VNet infrastructure template (optional, for private networking)
- **`main.parameters.json`**: Parameter file with deployment configuration
- **`vnet.parameters.json`**: VNet parameter file
- **`Deploy-ACI.ps1`**: PowerShell deployment script with automated provisioning

## Documentation

- **[VNet Integration Guide](../docs/VNET_INTEGRATION_GUIDE.md)**: Complete guide for VNet deployment and configuration
- **[Azure Deployment Guide](../docs/AZURE_DEPLOYMENT_GUIDE.md)**: Comprehensive deployment documentation
- **[Quick Reference](../docs/ACI_QUICK_REFERENCE.md)**: Common commands and operations

## Quick Start

### Prerequisites

1. Azure CLI installed and configured
2. Docker installed (for building images)
3. Active Azure subscription
4. Azure Container Registry (or create during deployment)

### Deploy to Azure

**Option 1: Public IP Only (Default)**

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry"
```

If your Azure Container Registry lives in a different resource group than the deployment resource group, append `-ContainerRegistryResourceGroup "<rg-name>"` to the command above.

**Option 2: With VNet Integration (Dual Networking)**

Deploy new VNet infrastructure and container together:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -DeployVnet
```

Or use existing VNet:

```powershell
$vnetId = "/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>"

.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId `
    -EnablePublicIp $true
```

**Option 3: Private Only (Maximum Security)**

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId `
    -EnablePublicIp $false
```

> **📖 See the [VNet Integration Guide](../docs/VNET_INTEGRATION_GUIDE.md) for detailed networking configuration.**

**Option 2: Manual Deployment**

```bash
# Set variables
RESOURCE_GROUP="llm-embedding-rg"
ACR_NAME="myregistry"

# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

# Preview deployment
az deployment group what-if \
    --name llm-embedding-deployment \
    --resource-group $RESOURCE_GROUP \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:latest" \
    --parameters registryServer="${ACR_NAME}.azurecr.io" \
    --parameters registryUsername="$ACR_USERNAME" \
    --parameters registryPassword="$ACR_PASSWORD"

# Execute deployment
az deployment group create \
    --name llm-embedding-deployment \
    --resource-group $RESOURCE_GROUP \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:latest" \
    --parameters registryServer="${ACR_NAME}.azurecr.io" \
    --parameters registryUsername="$ACR_USERNAME" \
    --parameters registryPassword="$ACR_PASSWORD"
```

## Configuration

### Resource Specifications

- **CPU**: 2 cores (configurable via `cpuCores` parameter)
- **Memory**: 4 GB (configurable via `memoryInGB` parameter)
- **Restart Policy**: Always
- **Public IP**: Enabled with DNS label
- **Health Probes**: Liveness and readiness checks configured

### Customizable Parameters

Edit `main.parameters.json` to customize:

```json
{
  "containerGroupName": "llm-embedding-api-prod",
  "location": "eastus",
  "cpuCores": 2,
  "memoryInGB": 4,
  "containerPort": 8080,
  "autoDownloadModel": true,
  "maxSequenceLength": 2048,
  "embeddingDimension": 768,
  "huggingFaceToken": "",
  "restartPolicy": "Always"
}
```

## Deployment Options

### With Hugging Face Token

For private models or to avoid rate limits:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -HuggingFaceToken "hf_xxxxx"
```

### Preview Mode (WhatIf)

Preview changes without deploying:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -WhatIf
```

### Skip Image Build

Deploy existing image without rebuilding:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -SkipBuild
```

## Outputs

After successful deployment, the script provides:

- **FQDN**: Fully qualified domain name
- **IP Address**: Public IP address
- **API Endpoint**: `http://<FQDN>:8080`
- **Health Endpoint**: `http://<FQDN>:8080/health`
- **Azure Portal Link**: Direct link to resource group

## Monitoring

### View Logs

```bash
az container logs \
    --resource-group llm-embedding-rg \
    --name llm-embedding-api-prod \
    --follow
```

### Check Container Status

```bash
az container show \
    --resource-group llm-embedding-rg \
    --name llm-embedding-api-prod \
    --query "{Status:instanceView.state,IP:ipAddress.ip,FQDN:ipAddress.fqdn}" \
    --output table
```

## Cost Estimation

**2 CPU / 4GB RAM Configuration**:
- Daily: ~$2.64 USD
- Monthly: ~$79 USD
- Annual: ~$950 USD

*Pricing for East US region (subject to change)*

## Scaling

### Vertical Scaling

Increase resources for higher throughput:

```bash
az deployment group create \
    --name llm-embedding-scale \
    --resource-group llm-embedding-rg \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters cpuCores=4 \
    --parameters memoryInGB=8
```

### Horizontal Scaling

For production workloads requiring >10 req/sec:

1. Deploy multiple ACI instances
2. Add Azure Application Gateway or Front Door for load balancing
3. Or migrate to Azure Container Apps / AKS for auto-scaling

## Troubleshooting

### Common Issues

**Container fails to start**:
- Check logs: `az container logs --resource-group <rg> --name <name>`
- Verify ACR credentials
- Ensure model download timeout is sufficient

**Slow performance**:
- Increase CPU/memory resources
- Verify model is cached
- Check for model download on each restart

**Health probe failures**:
- Increase `initialDelaySeconds` for readiness probe
- Check application logs for startup errors
- Verify port 8080 is exposed correctly

## Security

### Best Practices

1. **Secrets Management**: Use Azure Key Vault for production
2. **Private Networking**: Deploy in VNet for production environments
3. **ACR Authentication**: Use service principal instead of admin credentials
4. **HTTPS**: Add TLS termination via Application Gateway/Front Door
5. **Monitoring**: Enable Log Analytics for audit trails

### Secure Deployment

Store secrets in Key Vault:

```bash
# Create Key Vault
az keyvault create \
    --name llm-embedding-kv \
    --resource-group llm-embedding-rg \
    --location eastus

# Store HF token
az keyvault secret set \
    --vault-name llm-embedding-kv \
    --name hf-token \
    --value "hf_xxxxx"

# Reference in deployment (requires managed identity)
```

## Cleanup

### Delete Container Instance

```bash
az container delete \
    --resource-group llm-embedding-rg \
    --name llm-embedding-api-prod \
    --yes
```

### Delete Resource Group

```bash
az group delete \
    --name llm-embedding-rg \
    --yes \
    --no-wait
```

## Documentation

- [Full Deployment Guide](../docs/AZURE_DEPLOYMENT_GUIDE.md)
- [Quick Reference](../docs/ACI_QUICK_REFERENCE.md)
- [Performance Monitoring](../docs/PERFORMANCE_MONITORING.md)
- [API Documentation](../README.md)

## Support

For issues or questions:
1. Check the [deployment guide](../docs/AZURE_DEPLOYMENT_GUIDE.md)
2. Review Azure Container Instances [documentation](https://learn.microsoft.com/azure/container-instances/)
3. Inspect container logs and events
4. Verify quota limits in your region

## Advanced Features

### Log Analytics Integration

Enable advanced monitoring:

```bash
WORKSPACE_ID=$(az monitor log-analytics workspace create \
    --resource-group llm-embedding-rg \
    --workspace-name llm-embedding-logs \
    --query customerId -o tsv)

WORKSPACE_KEY=$(az monitor log-analytics workspace get-shared-keys \
    --resource-group llm-embedding-rg \
    --workspace-name llm-embedding-logs \
    --query primarySharedKey -o tsv)

# Redeploy with Log Analytics
az deployment group create \
    --name llm-embedding-with-logs \
    --resource-group llm-embedding-rg \
    --template-file main.bicep \
    --parameters @main.parameters.json \
    --parameters logAnalyticsWorkspaceId="$WORKSPACE_ID" \
    --parameters logAnalyticsWorkspaceKey="$WORKSPACE_KEY"
```

### Private VNet Deployment

For production environments requiring network isolation, modify the Bicep template to use private IP and subnet delegation:

```bicep
ipAddress: {
  type: 'Private'
  ports: [...]
}
subnetIds: [
  {
    id: '<subnet-resource-id>'
  }
]
```

## Next Steps

1. ✅ Deploy to dev/test environment
2. Test endpoints and health checks
3. Run load tests to validate performance
4. Configure monitoring and alerts
5. Set up CI/CD pipeline
6. Plan production deployment with private networking
7. Implement auto-scaling strategy (Container Apps/AKS)
