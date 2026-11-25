# Azure Container Instances - Quick Reference

## Quick Deploy Commands

### Prerequisites Check
```bash
az --version
docker --version
az account show
```

### One-Command Deploy (PowerShell)
```powershell
cd infra
.\Deploy-ACI.ps1 -ResourceGroupName "llm-embedding-rg" -ContainerRegistry "myregistry"
```

If the Azure Container Registry you are targeting is in a different resource group than the deployment resource group, append `-ContainerRegistryResourceGroup "<rg-name>"` to the call above.

### Manual Deploy (Bash)
```bash
# Variables
RESOURCE_GROUP="llm-embedding-rg"
LOCATION="eastus"
ACR_NAME="myregistry"
IMAGE_TAG="latest"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create ACR (if needed)
az acr create --name $ACR_NAME --resource-group $RESOURCE_GROUP --sku Basic --admin-enabled true

# Build and push
az acr login --name $ACR_NAME
docker build -t ${ACR_NAME}.azurecr.io/llm-embedding-api:${IMAGE_TAG} -f Dockerfile .
docker push ${ACR_NAME}.azurecr.io/llm-embedding-api:${IMAGE_TAG}

# Get credentials
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

# Deploy
az deployment group create \
    --name llm-embedding-$(date +%Y%m%d-%H%M%S) \
    --resource-group $RESOURCE_GROUP \
    --template-file infra/main.bicep \
    --parameters containerImage="${ACR_NAME}.azurecr.io/llm-embedding-api:${IMAGE_TAG}" \
    --parameters registryServer="${ACR_NAME}.azurecr.io" \
    --parameters registryUsername="$ACR_USERNAME" \
    --parameters registryPassword="$ACR_PASSWORD" \
    --parameters location="$LOCATION"
```

## Common Operations

### View Logs
```bash
az container logs --resource-group llm-embedding-rg --name llm-embedding-api-prod --follow
```

### Check Status
```bash
az container show --resource-group llm-embedding-rg --name llm-embedding-api-prod \
    --query "{Status:instanceView.state,IP:ipAddress.ip,FQDN:ipAddress.fqdn}" -o table
```

### Restart Container
```bash
az container restart --resource-group llm-embedding-rg --name llm-embedding-api-prod
```

### Delete Deployment
```bash
az container delete --resource-group llm-embedding-rg --name llm-embedding-api-prod --yes
```

## Testing Endpoints

### Health Check
```bash
curl http://<FQDN>:8080/health
```

### Generate Embedding
```bash
curl -X POST http://<FQDN>:8080/api/embed \
    -H "Content-Type: application/json" \
    -d '{"text":"Test message"}'
```

### Performance Metrics
```bash
curl http://<FQDN>:8080/api/metrics/current
curl http://<FQDN>:8080/api/metrics/statistics
```

## PowerShell Equivalents

### Test Embedding
```powershell
$fqdn = "llm-embed-xxxx.eastus.azurecontainer.io"
$body = @{ text = "Test message" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://${fqdn}:8080/api/embed" -Method Post -Body $body -ContentType "application/json"
```

### Get Metrics
```powershell
Invoke-RestMethod -Uri "http://${fqdn}:8080/api/metrics/current"
```

## Configuration Parameters

| Parameter | Default | Production |
|-----------|---------|------------|
| CPU Cores | 2 | 2-4 |
| Memory GB | 4 | 4-8 |
| Max Sequence Length | 2048 | 2048 |
| Embedding Dimension | 768 | 768 |
| Auto Download | true | true |
| Restart Policy | Always | Always |

## Resource Specifications

- **SKU**: Standard (2 CPU / 4GB RAM)
- **OS**: Linux
- **Container Port**: 8080
- **Protocol**: HTTP (TCP)
- **Restart Policy**: Always
- **Public IP**: Yes (with DNS label)

## Cost (Approximate)

- **Daily**: ~$2.64 USD
- **Monthly**: ~$79 USD
- **Yearly**: ~$950 USD

*Prices for East US region, subject to change*

## Performance Expectations

Based on load test results with 2 CPU / 4GB RAM:

| Metric | Value |
|--------|-------|
| Throughput | ~1 req/sec |
| Avg Latency | 1-2 seconds |
| P95 Latency | 3-5 seconds |
| P99 Latency | 9-11 seconds |
| Memory Peak | ~700 MB |
| CPU Average | ~20% |

## Troubleshooting Quick Fixes

### Container Won't Start
```bash
# Check events
az container show --resource-group llm-embedding-rg --name llm-embedding-api-prod \
    --query "instanceView.events" -o table

# View full logs
az container logs --resource-group llm-embedding-rg --name llm-embedding-api-prod
```

### Model Download Timeout
- Increase readiness probe `initialDelaySeconds` to 120
- Verify Hugging Face token if using private model
- Check network connectivity

### High Memory Usage
- Reduce `MaxSequenceLength` to 1024
- Increase memory to 8GB
- Consider using quantized model

### Slow Responses
- Verify model is downloaded and cached
- Check CPU utilization
- Test with shorter input texts
- Consider scaling to 4 CPU cores

## Security Checklist

- [ ] ACR admin account disabled (use service principal)
- [ ] Hugging Face token stored as secure parameter
- [ ] Log Analytics workspace configured
- [ ] Resource group tags applied
- [ ] Cost alerts configured
- [ ] Consider private VNet deployment for production

## Monitoring URLs

- **Azure Portal**: https://portal.azure.com/#@/resource/subscriptions/{subscription-id}/resourceGroups/{rg-name}/overview
- **Container Metrics**: Azure Portal > Container Instance > Metrics
- **Log Analytics**: Azure Portal > Log Analytics Workspace > Logs

## Next Steps After Deployment

1. Test health endpoint: `curl http://<FQDN>:8080/health`
2. Test embedding endpoint with sample text
3. Run load test: `cd tests/LoadTest && python load_test.py --url http://<FQDN>:8080/api/embed --requests 100 --concurrent 10`
4. Monitor logs for errors
5. Configure alerts for failures
6. Set up CI/CD pipeline
7. Plan scaling strategy

## Support

- Documentation: `docs/AZURE_DEPLOYMENT_GUIDE.md`
- API Docs: `http://<FQDN>:8080/swagger`
- Load Testing: `tests/LoadTest/README.md`
- Performance Guide: `docs/PERFORMANCE_MONITORING.md`
