# Azure Deployment Checklist

Use this checklist to ensure a successful deployment of the LLM Embedding API to Azure Container Instances.

## Pre-Deployment Checklist

### 1. Prerequisites ✅

- [ ] Azure CLI installed and up to date
  ```powershell
  az --version
  ```

- [ ] Docker installed and running
  ```powershell
  docker --version
  ```

- [ ] Logged into Azure
  ```bash
  az login
  az account show  # Verify correct subscription
  ```

- [ ] Azure Container Registry created (or will be created)
  ```bash
  az acr list --output table
  ```

### 2. Configuration Decisions 📋

#### Networking Mode

Choose your networking configuration:

- [ ] **Public Only** (Default)
  - ✅ Quick to deploy
  - ✅ Internet accessible
  - ❌ No VNet integration
  - **Use for**: Testing, demos, public APIs

- [ ] **Dual Mode** (Private VNet + Public IP)
  - ✅ Internal service access via private IP
  - ✅ External debugging via public IP
  - ✅ VNet integration
  - **Use for**: Development, staging, debugging

- [ ] **Private Only** (VNet, no public IP)
  - ✅ Maximum security
  - ✅ Internal access only
  - ❌ No external access
  - **Use for**: Production, internal APIs

#### Resources

- [ ] Resource Group Name: `________________`
- [ ] Location (region): `________________` (e.g., eastus, westus2)
- [ ] Container Registry Name: `________________`
- [ ] CPU Cores: `2` (default, can be adjusted)
- [ ] Memory (GB): `4` (default, can be adjusted)

#### Optional Configuration

- [ ] Hugging Face token (for private models): `________________`
- [ ] Log Analytics Workspace (for monitoring): `________________`
- [ ] VNet Resource ID (if using existing VNet): `________________`
- [ ] Subnet Name (if using VNet): `________________` (default: aci-subnet)

### 3. Review Documentation 📖

- [ ] Read [Azure Deployment Guide](docs/AZURE_DEPLOYMENT_GUIDE.md)
- [ ] Review [VNet Integration Guide](docs/VNET_INTEGRATION_GUIDE.md) (if using VNet)
- [ ] Bookmark [ACI Quick Reference](docs/ACI_QUICK_REFERENCE.md)

## Deployment Steps

### Option A: Public Only Deployment

```powershell
# Navigate to infrastructure directory
cd infra

# Run deployment
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -HuggingFaceToken "hf_xxx"  # Optional
```

**Checklist:**
- [ ] Script completes successfully
- [ ] FQDN is displayed in output
- [ ] Health check passes
- [ ] Test embedding endpoint works

### Option B: VNet Deployment (New VNet)

```powershell
cd infra

# Deploy VNet infrastructure and container together
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -DeployVnet `
    -EnablePublicIp $true  # Set to $false for private-only
```

**Checklist:**
- [ ] VNet deployment completes
- [ ] VNet resource ID is captured
- [ ] Container deployment completes
- [ ] Private IP is displayed
- [ ] Public FQDN is displayed (if EnablePublicIp = true)
- [ ] Health check passes
- [ ] Test from within VNet (if possible)
- [ ] Test from internet (if public IP enabled)

### Option C: VNet Deployment (Existing VNet)

**Step 1: Verify VNet Prerequisites**

```bash
# Get VNet resource ID
VNET_ID=$(az network vnet show \
    --resource-group "existing-vnet-rg" \
    --name "my-existing-vnet" \
    --query id \
    --output tsv)

# Check subnet delegation
az network vnet subnet show \
    --resource-group "existing-vnet-rg" \
    --vnet-name "my-existing-vnet" \
    --name "aci-subnet" \
    --query "delegations[].serviceName"

# If not delegated, add delegation
az network vnet subnet update \
    --resource-group "existing-vnet-rg" \
    --vnet-name "my-existing-vnet" \
    --name "aci-subnet" \
    --delegations Microsoft.ContainerInstance/containerGroups
```

**Step 2: Deploy Container**

```powershell
cd infra

$vnetId = "/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>"

.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -Location "eastus" `
    -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId `
    -SubnetName "aci-subnet" `
    -EnablePublicIp $true  # Set to $false for private-only
```

**Checklist:**
- [ ] Subnet is delegated to Microsoft.ContainerInstance/containerGroups
- [ ] Subnet has sufficient IP addresses (/28 minimum, /24 recommended)
- [ ] Container deployment completes
- [ ] Private IP is in correct subnet range
- [ ] Public FQDN is displayed (if EnablePublicIp = true)
- [ ] Health check passes

## Post-Deployment Verification

### 1. Basic Health Check ✅

```powershell
# Test health endpoint
$healthUrl = "<HEALTH_ENDPOINT_FROM_OUTPUT>"
Invoke-RestMethod -Uri $healthUrl
```

**Expected Response:**
```json
"Healthy"
```

- [ ] Health endpoint returns "Healthy"

### 2. Embedding Functionality Test ✅

```powershell
# Test embedding endpoint
$apiUrl = "<API_ENDPOINT_FROM_OUTPUT>"
$body = @{ text = "Hello, world!" } | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "$apiUrl/api/embed" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

# Check response
$response
```

**Expected Response:**
```json
{
  "embedding": [0.123, -0.456, ...],  // 768 dimensions
  "dimension": 768,
  "processingTimeMs": 1234
}
```

- [ ] Embedding endpoint responds successfully
- [ ] Embedding array has 768 dimensions
- [ ] Processing time is reasonable (< 5000ms)

### 3. Performance Metrics Test ✅

```powershell
# Check performance metrics
$metricsUrl = "<API_ENDPOINT_FROM_OUTPUT>"
Invoke-RestMethod -Uri "$metricsUrl/api/metrics"
```

**Expected Response:**
```json
{
  "totalRequests": 1,
  "averageResponseTime": 1234.56,
  "minResponseTime": 1234,
  "maxResponseTime": 1234,
  "requestsPerSecond": 0.1,
  "uptime": "00:05:30",
  "timestamp": "2025-01-15T10:30:00Z"
}
```

- [ ] Metrics endpoint responds successfully
- [ ] Metrics show reasonable values

### 4. Network Connectivity Test (VNet Mode) ✅

**If using VNet, test private access:**

From a VM or service within the VNet:
```bash
# Using private IP
PRIVATE_IP="<PRIVATE_IP_FROM_OUTPUT>"

# Health check
curl http://$PRIVATE_IP:8080/health

# Embedding test
curl -X POST "http://$PRIVATE_IP:8080/api/embed" \
    -H "Content-Type: application/json" \
    -d '{"text":"Hello from VNet!"}'
```

- [ ] Private IP is accessible from within VNet
- [ ] Health endpoint works via private IP
- [ ] Embedding endpoint works via private IP

**If public IP is enabled, test external access:**

```powershell
# Using public FQDN
$fqdn = "<FQDN_FROM_OUTPUT>"

# Health check
Invoke-RestMethod -Uri "http://$fqdn/health"

# Embedding test
$body = @{ text = "Hello from internet!" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://$fqdn/api/embed" -Method Post -Body $body -ContentType "application/json"
```

- [ ] Public FQDN is accessible from internet
- [ ] Health endpoint works via FQDN
- [ ] Embedding endpoint works via FQDN

## Monitoring Setup (Optional)

### 1. Log Analytics ✅

If you configured Log Analytics workspace:

```bash
# View logs
az monitor log-analytics query \
    --workspace "<WORKSPACE_ID>" \
    --analytics-query "ContainerInstanceLog_CL | where TimeGenerated > ago(1h) | limit 100"
```

- [ ] Logs are being ingested to Log Analytics
- [ ] Container logs are visible
- [ ] No error messages in logs

### 2. Azure Portal Monitoring ✅

Navigate to Azure Portal → Resource Group → Container Instance

- [ ] Container state is "Running"
- [ ] No restart events
- [ ] CPU usage is reasonable (< 80%)
- [ ] Memory usage is reasonable (< 3.5GB)
- [ ] No health probe failures

## Security Hardening (Production)

### 1. Network Security ✅

If using VNet:
- [ ] NSG rules are properly configured
- [ ] Only required ports are open (8080)
- [ ] Consider disabling public IP (private-only mode)
- [ ] Review source IP restrictions in NSG

### 2. Access Control ✅

- [ ] Consider implementing Azure API Management
- [ ] Set up Azure AD authentication (if needed)
- [ ] Review ACR access permissions
- [ ] Implement rate limiting (via API Management)

### 3. Secrets Management ✅

- [ ] Move ACR credentials to Azure Key Vault
- [ ] Move Hugging Face token to Key Vault (if used)
- [ ] Use Managed Identity instead of service principal (if possible)
- [ ] Rotate credentials regularly

### 4. Monitoring & Alerts ✅

- [ ] Set up Azure Monitor alerts for:
  - Container restarts
  - High CPU usage (> 80%)
  - High memory usage (> 3.5GB)
  - Health probe failures
  - High response times

## Cost Optimization

### 1. Right-Sizing ✅

Current configuration: 2 CPU, 4GB RAM ≈ $79/month

- [ ] Monitor actual CPU/memory usage
- [ ] Consider reducing to 1 CPU / 2GB if underutilized
- [ ] Consider increasing if experiencing performance issues

### 2. Scheduling ✅

For non-production environments:

- [ ] Consider stopping container during off-hours
- [ ] Use Azure Automation runbooks for scheduling
- [ ] Estimated savings: up to 66% (if only running 8 hours/day)

## Troubleshooting

### Common Issues

**Issue: Container fails to start**
- [ ] Check container logs in Azure Portal
- [ ] Verify ACR credentials are correct
- [ ] Ensure image exists in ACR
- [ ] Check Hugging Face token (if required)

**Issue: Health check fails**
- [ ] Wait 2-3 minutes for model download
- [ ] Check container logs for errors
- [ ] Verify port 8080 is accessible
- [ ] Test with direct IP access

**Issue: Cannot access via private IP**
- [ ] Verify subnet delegation
- [ ] Check NSG rules
- [ ] Ensure client is in same VNet (or peered VNet)
- [ ] Test with a VM in the same subnet

**Issue: Cannot access via public IP**
- [ ] Verify `enablePublicIp` is set to `true`
- [ ] Wait 2-3 minutes for DNS propagation
- [ ] Try using IP address directly instead of FQDN
- [ ] Check NSG allows inbound on port 8080

## Next Steps

### Development Environment
- [ ] Configure internal services to use private IP
- [ ] Set up CI/CD pipeline for automated deployments
- [ ] Configure staging environment
- [ ] Set up integration tests

### Production Environment
- [ ] Switch to private-only mode (`enablePublicIp: false`)
- [ ] Implement API Management gateway
- [ ] Set up comprehensive monitoring and alerts
- [ ] Configure backup and disaster recovery
- [ ] Document incident response procedures
- [ ] Schedule regular security reviews

### Scaling Considerations

When you need more capacity:
- [ ] Vertical scaling: Increase CPU/memory (up to 4 CPU / 16GB)
- [ ] Horizontal scaling: Deploy multiple ACI instances behind load balancer
- [ ] Consider migrating to Azure Container Apps for built-in scaling
- [ ] Consider migrating to AKS for advanced orchestration

## Documentation Links

- [Azure Deployment Guide](docs/AZURE_DEPLOYMENT_GUIDE.md) - Complete guide
- [VNet Integration Guide](docs/VNET_INTEGRATION_GUIDE.md) - Networking details
- [ACI Quick Reference](docs/ACI_QUICK_REFERENCE.md) - Commands and troubleshooting
- [Performance Monitoring](docs/PERFORMANCE_MONITORING.md) - Monitoring setup
- [Configuration Guide](CONFIGURATION.md) - Application configuration

## Support

For issues or questions:
1. Check troubleshooting section in guides
2. Review Azure Container Instances documentation
3. Check container logs in Azure Portal
4. Review deployment script output for errors

---

**Deployment Status**: ⬜ Not Started | ⏳ In Progress | ✅ Complete

**Last Updated**: _____________
**Deployed By**: _____________
**Environment**: _____________
