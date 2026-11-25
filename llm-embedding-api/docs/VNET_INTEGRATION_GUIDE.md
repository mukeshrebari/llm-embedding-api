# Virtual Network Integration Guide

This guide explains how to deploy the LLM Embedding API with Azure Virtual Network (VNet) integration for both internal and external access.

## Overview

The deployment supports three networking modes:

1. **Public Only** (default): Traditional public IP with DNS name
2. **Private Only**: Private IP in VNet, no public access
3. **Dual Mode**: Private IP in VNet + public IP for debugging

## Architecture

```
┌─────────────────────────────────────────┐
│         Azure Virtual Network           │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │    ACI Subnet (10.0.1.0/24)       │ │
│  │  Delegated to:                    │ │
│  │  Microsoft.ContainerInstance      │ │
│  │  /containerGroups                 │ │
│  │                                   │ │
│  │  ┌────────────────────────────┐  │ │
│  │  │  LLM Embedding API         │  │ │
│  │  │  Container Instance        │  │ │
│  │  │                            │  │ │
│  │  │  Private IP: 10.0.1.4      │  │ │
│  │  │  Public IP: (optional)     │  │ │
│  │  └────────────────────────────┘  │ │
│  │                                   │ │
│  └───────────────────────────────────┘ │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │  Other Services Subnet            │ │
│  │  (App Services, VMs, etc.)        │ │
│  │  Can access via Private IP        │ │
│  └───────────────────────────────────┘ │
└─────────────────────────────────────────┘
         │
         │ (Public IP - Optional)
         ▼
    Internet Access
   (For Debugging)
```

## Deployment Options

### Option 1: Deploy New VNet + Container (Recommended for Testing)

Deploy everything in one step:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -DeployVnet
```

This will:
- Create a new VNet (10.0.0.0/16)
- Create ACI subnet (10.0.1.0/24) with proper delegation
- Create Network Security Group (NSG)
- Deploy container with dual networking (private + public)

### Option 2: Use Existing VNet (Production)

If you already have a VNet:

```powershell
# Get your VNet resource ID
$vnetId = az network vnet show `
    --resource-group "my-existing-rg" `
    --name "my-existing-vnet" `
    --query id `
    --output tsv

# Deploy with dual networking (private + public)
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId `
    -SubnetName "aci-subnet" `
    -EnablePublicIp $true
```

### Option 3: Private Only (Maximum Security)

For production environments where you don't need external access:

```powershell
.\Deploy-ACI.ps1 `
    -ResourceGroupName "llm-embedding-rg" `
    -ContainerRegistry "myregistry" `
    -VnetResourceId $vnetId `
    -SubnetName "aci-subnet" `
    -EnablePublicIp $false
```

This configuration:
- ✅ Provides private IP for internal services
- ✅ No public exposure
- ❌ Cannot access from internet (even for debugging)

## Prerequisites for VNet Integration

### 1. Subnet Requirements

The subnet used for ACI **must** be delegated to `Microsoft.ContainerInstance/containerGroups`:

```bash
# Check if subnet is properly delegated
az network vnet subnet show \
    --resource-group "my-rg" \
    --vnet-name "my-vnet" \
    --name "aci-subnet" \
    --query "delegations[].serviceName"

# If not delegated, add delegation
az network vnet subnet update \
    --resource-group "my-rg" \
    --vnet-name "my-vnet" \
    --name "aci-subnet" \
    --delegations Microsoft.ContainerInstance/containerGroups
```

### 2. Subnet Size

Minimum subnet size: **/28** (16 IP addresses)
Recommended: **/24** (256 IP addresses) for production

### 3. Network Security Group (NSG)

Our deployment automatically creates an NSG with these rules:

| Priority | Name | Direction | Access | Protocol | Port | Source | Destination |
|----------|------|-----------|--------|----------|------|--------|-------------|
| 100 | AllowHTTPInbound | Inbound | Allow | TCP | 8080 | * | * |
| 110 | AllowVNetInbound | Inbound | Allow | * | * | VirtualNetwork | VirtualNetwork |
| 1000 | DenyAllInbound | Inbound | Deny | * | * | * | * |

## Testing VNet Connectivity

### Test Public Access (if enabled)

```powershell
# Get the public endpoint
$fqdn = az containerapp show \
    --resource-group "llm-embedding-rg" \
    --name "llm-embedding-api-prod" \
    --query "properties.outputs.fqdn.value" \
    --output tsv

# Test from your local machine
$body = @{ text = "Hello, world!" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://$fqdn/api/embed" -Method Post -Body $body -ContentType "application/json"
```

### Test Private Access (from within VNet)

Deploy a test VM or use Azure Bastion to access a VM in the same VNet:

```bash
# Get the private IP
PRIVATE_IP=$(az container show \
    --resource-group "llm-embedding-rg" \
    --name "llm-embedding-api-prod" \
    --query "ipAddress.ip" \
    --output tsv)

# Test from VM within VNet
curl -X POST "http://$PRIVATE_IP:8080/api/embed" \
    -H "Content-Type: application/json" \
    -d '{"text":"Hello from VNet!"}'
```

### Test from Azure App Service

If you have an App Service in the same VNet:

```csharp
// C# example from App Service
var client = new HttpClient();
var privateIp = "10.0.1.4"; // Your container's private IP
var content = new StringContent(
    JsonSerializer.Serialize(new { text = "Hello!" }), 
    Encoding.UTF8, 
    "application/json"
);
var response = await client.PostAsync(
    $"http://{privateIp}:8080/api/embed", 
    content
);
```

## Manual VNet Deployment

If you prefer to deploy VNet infrastructure separately:

### 1. Deploy VNet Infrastructure

```powershell
az deployment group create \
    --resource-group "llm-embedding-rg" \
    --template-file "infra/vnet.bicep" \
    --parameters "infra/vnet.parameters.json"
```

### 2. Get VNet Resource ID

```powershell
$vnetId = az network vnet show \
    --resource-group "llm-embedding-rg" \
    --name "llm-embedding-vnet" \
    --query id \
    --output tsv
```

### 3. Deploy Container with VNet

```powershell
.\Deploy-ACI.ps1 \
    -ResourceGroupName "llm-embedding-rg" \
    -ContainerRegistry "myregistry" \
    -VnetResourceId $vnetId
```

## Bicep Parameters for VNet

In `main.parameters.json`:

```json
{
  "vnetResourceId": {
    "value": "/subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet-name>"
  },
  "subnetName": {
    "value": "aci-subnet"
  },
  "enablePublicIp": {
    "value": true
  }
}
```

## Troubleshooting VNet Issues

### Issue: "Subnet is not delegated"

**Error:**
```
Subnet 'aci-subnet' is not delegated to Microsoft.ContainerInstance/containerGroups
```

**Solution:**
```bash
az network vnet subnet update \
    --resource-group "my-rg" \
    --vnet-name "my-vnet" \
    --name "aci-subnet" \
    --delegations Microsoft.ContainerInstance/containerGroups
```

### Issue: "Subnet is too small"

**Error:**
```
The subnet has insufficient IP addresses
```

**Solution:**
Use a larger subnet. Minimum /28, recommended /24:

```bash
az network vnet subnet create \
    --resource-group "my-rg" \
    --vnet-name "my-vnet" \
    --name "aci-subnet" \
    --address-prefix "10.0.1.0/24"
```

### Issue: Cannot access private IP from other services

**Checklist:**
1. ✅ Is the service in the same VNet?
2. ✅ Is the subnet NSG allowing traffic?
3. ✅ Is the container running? Check: `az container show --resource-group ... --name ...`
4. ✅ Try accessing the health endpoint first: `http://<private-ip>:8080/health`

### Issue: Cannot access public IP (dual mode)

**Checklist:**
1. ✅ Verify `enablePublicIp` is set to `true`
2. ✅ Check if FQDN is assigned: Should not be "N/A - Private IP only"
3. ✅ Wait 2-3 minutes for DNS propagation
4. ✅ Test with IP address directly first

## Security Recommendations

### Production (Private Only)

For maximum security:
- ✅ Set `enablePublicIp: false`
- ✅ Use private endpoints only
- ✅ Access via VPN or ExpressRoute
- ✅ Use Azure Private DNS zones
- ✅ Enable Azure Firewall or Network Virtual Appliance (NVA)

### Development/Staging (Dual Mode)

For debugging while maintaining security:
- ✅ Set `enablePublicIp: true`
- ✅ Use NSG to restrict public access by source IP
- ✅ Use Azure API Management as a gateway
- ✅ Enable Azure DDoS Protection
- ✅ Monitor access with Log Analytics

## Cost Considerations

### Public Only
- Container Instance: ~$79/month (2 CPU, 4GB RAM)
- **Total: ~$79/month**

### Private Only (VNet)
- Container Instance: ~$79/month
- Virtual Network: Free
- NSG: Free
- **Total: ~$79/month**

### Dual Mode (VNet + Public IP)
- Container Instance: ~$79/month
- Public IP (Static): ~$3.65/month
- Virtual Network: Free
- NSG: Free
- **Total: ~$82.65/month**

### With Additional Services
- Log Analytics: ~$2-10/month (depends on ingestion)
- Azure Firewall: ~$360/month (if needed)
- VPN Gateway: ~$27-$650/month (if needed)
- ExpressRoute: ~$55-$13,000/month (if needed)

## Integration Examples

### With Azure App Service

```powershell
# Enable VNet integration for App Service
az webapp vnet-integration add \
    --resource-group "my-rg" \
    --name "my-app-service" \
    --vnet "llm-embedding-vnet" \
    --subnet "app-service-subnet"

# Update App Service config to use private IP
az webapp config appsettings set \
    --resource-group "my-rg" \
    --name "my-app-service" \
    --settings EMBEDDING_API_URL="http://10.0.1.4:8080"
```

### With Azure Functions

```powershell
# Enable VNet integration
az functionapp vnet-integration add \
    --resource-group "my-rg" \
    --name "my-function-app" \
    --vnet "llm-embedding-vnet" \
    --subnet "function-subnet"

# Use private IP in application settings
az functionapp config appsettings set \
    --resource-group "my-rg" \
    --name "my-function-app" \
    --settings EMBEDDING_API_URL="http://10.0.1.4:8080"
```

### With Azure Kubernetes Service (AKS)

If your AKS cluster is in the same VNet or peered VNet:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: embedding-api-config
data:
  # Use private IP for internal traffic
  EMBEDDING_API_URL: "http://10.0.1.4:8080"
```

## Next Steps

1. **Choose your networking mode** based on requirements
2. **Deploy VNet infrastructure** (if not using existing VNet)
3. **Deploy container** with appropriate VNet parameters
4. **Test connectivity** from internal services
5. **Configure DNS** (optional - Azure Private DNS)
6. **Set up monitoring** (Log Analytics, Application Insights)
7. **Implement authentication** (Azure API Management, Entra ID)

## Additional Resources

- [Azure Container Instances VNet Documentation](https://learn.microsoft.com/azure/container-instances/container-instances-vnet)
- [VNet Peering](https://learn.microsoft.com/azure/virtual-network/virtual-network-peering-overview)
- [Azure Private Link](https://learn.microsoft.com/azure/private-link/private-link-overview)
- [Network Security Groups](https://learn.microsoft.com/azure/virtual-network/network-security-groups-overview)
