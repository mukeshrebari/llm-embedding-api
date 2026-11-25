// Azure Container Instance deployment for LLM Embedding API
// This template deploys the embedding service with 2 CPU cores and 4GB RAM
// optimized for production workloads with proper security and monitoring

@description('Name of the container group')
param containerGroupName string = 'llm-embedding-api'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Container image to deploy')
param containerImage string

@description('Container registry server (e.g., myregistry.azurecr.io)')
param registryServer string = ''

@description('Container registry username')
@secure()
param registryUsername string = ''

@description('Container registry password')
@secure()
param registryPassword string = ''

@description('User-assigned managed identity resource ID for pulling from ACR (preferred over admin credentials)')
param userAssignedIdentityResourceId string = ''

@description('User-assigned managed identity client ID for ACR pulls (required when identity is used)')
param userAssignedIdentityClientId string = ''

@description('DNS name label for the public IP (must be globally unique)')
param dnsNameLabel string = '${containerGroupName}-${uniqueString(resourceGroup().id)}'

@description('Virtual Network resource ID for private networking (leave empty for public-only)')
param vnetResourceId string = ''

@description('Subnet name within the VNet for container deployment')
param subnetName string = 'aci-subnet'

@description('Enable public IP address (dual-mode: both public and private when VNet is provided)')
param enablePublicIp bool = true

@description('Port the container listens on')
param containerPort int = 8080

@description('Number of CPU cores to allocate')
param cpuCores int = 2

@description('Memory in GB to allocate')
param memoryInGB int = 4

@description('Hugging Face API token for model downloads (optional)')
@secure()
param huggingFaceToken string = ''

@description('SHA-256 hex hash of a static API key to accept (Auth:ApiKeys:0:KeyHash). Compute with tools/hash-key — never pass the plaintext key. Prefer sourcing this from Key Vault.')
@secure()
param apiKeyHash string = ''

@description('Human-readable label for the static API key configured via apiKeyHash')
param apiKeyName string = 'aci-deployment'

@description('Enable delegating API key validation to the organization service instead of (or in addition to) the static key above')
param authRemoteValidationEnabled bool = false

@description('Organization service API key validation endpoint, e.g. https://organization.internal/api/org/apikeys/validate')
param authRemoteValidationEndpoint string = ''

@description('Requests per minute permitted per API key (or per client IP) for general /api/* endpoints')
param rateLimitPermitPerMinute int = 120

@description('Requests per minute permitted per API key (or per client IP) for the batch embedding endpoint')
param rateLimitBatchPermitPerMinute int = 30

@description('Enable automatic model download on startup')
param autoDownloadModel bool = true

@description('Maximum sequence length for tokenization')
param maxSequenceLength int = 2048

@description('Embedding output dimension')
param embeddingDimension int = 768

@description('Log Analytics workspace ID for monitoring')
param logAnalyticsWorkspaceId string = ''

@description('Log Analytics workspace key')
@secure()
param logAnalyticsWorkspaceKey string = ''

@description('Storage account name for Azure File share (optional)')
param storageAccountName string = ''

@description('Storage account key (secure) for Azure File share')
@secure()
param storageAccountKey string = ''

@description('File share name to mount for models')
param fileShareName string = 'llm-models'

@description('Restart policy for the container')
@allowed([
  'Always'
  'OnFailure'
  'Never'
])
param restartPolicy string = 'Always'

@description('Tags to apply to all resources')
param tags object = {
  Application: 'LLM-Embedding-API'
  Environment: 'Production'
  ManagedBy: 'Bicep'
}

var useUserAssignedIdentity = !empty(userAssignedIdentityResourceId)

// Define container group resource
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  tags: tags
  identity: useUserAssignedIdentity ? {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityResourceId}': {}
    }
  } : null
  properties: {
    containers: [
      {
        name: 'llm-embedding-api'
        properties: {
          image: containerImage
          ports: [
            {
              port: containerPort
              protocol: 'TCP'
            }
          ]
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: memoryInGB
            }
          }
          environmentVariables: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:${containerPort}'
            }
            {
              name: 'Embedding__AutoDownload'
              value: string(autoDownloadModel)
            }
            {
              name: 'Embedding__MaxSequenceLength'
              value: string(maxSequenceLength)
            }
            {
              name: 'Embedding__EmbeddingDimension'
              value: string(embeddingDimension)
            }
            {
              name: 'Embedding__HuggingFaceToken'
              secureValue: huggingFaceToken
            }
            {
              name: 'Embedding__HuggingFaceRepoId'
              value: 'onnx-community/embeddinggemma-300m-ONNX'
            }
            {
              name: 'Embedding__HuggingFaceFilename'
              value: 'onnx/model_fp16.onnx'
            }
            {
              name: 'Auth__ApiKeys__0__KeyHash'
              secureValue: apiKeyHash
            }
            {
              name: 'Auth__ApiKeys__0__Name'
              value: apiKeyName
            }
            {
              name: 'Auth__RemoteValidation__Enabled'
              value: string(authRemoteValidationEnabled)
            }
            {
              name: 'Auth__RemoteValidation__Endpoint'
              value: authRemoteValidationEndpoint
            }
            {
              name: 'RateLimiting__PermitPerMinute'
              value: string(rateLimitPermitPerMinute)
            }
            {
              name: 'RateLimiting__BatchPermitPerMinute'
              value: string(rateLimitBatchPermitPerMinute)
            }
            // Mount marker: the container will mount Azure File share to /app/models when configured
          ]
          volumeMounts: !empty(storageAccountName) ? [
            {
              name: 'models-volume'
              mountPath: '/app/models'
              readOnly: false
            }
          ] : []
          // Health probe configuration
          livenessProbe: {
            httpGet: {
              path: '/health'
              port: containerPort
              scheme: 'Http'
            }
            initialDelaySeconds: 30
            periodSeconds: 30
            failureThreshold: 3
            successThreshold: 1
            timeoutSeconds: 5
          }
          readinessProbe: {
            httpGet: {
              path: '/health'
              port: containerPort
              scheme: 'Http'
            }
            initialDelaySeconds: 60
            periodSeconds: 10
            failureThreshold: 3
            successThreshold: 1
            timeoutSeconds: 5
          }
        }
      }
    ]
    // Registry credentials (only if using private registry)
    imageRegistryCredentials: !empty(registryServer) ? [
      !empty(userAssignedIdentityClientId) ? {
        server: registryServer
        identity: userAssignedIdentityClientId
      } : {
        server: registryServer
        username: registryUsername
        password: registryPassword
      }
    ] : []
    // Subnet configuration for VNet integration
    subnetIds: !empty(vnetResourceId) ? [
      {
        id: '${vnetResourceId}/subnets/${subnetName}'
      }
    ] : []
    // IP address configuration
    // Dual-mode: When VNet is provided AND enablePublicIp=true, container gets both private IP and public IP
    // Public-only: When VNet is empty, container gets only public IP
    // Private-only: When VNet is provided AND enablePublicIp=false, container gets only private IP
    ipAddress: {
      type: !empty(vnetResourceId) ? 'Private' : 'Public'
      dnsNameLabel: enablePublicIp ? dnsNameLabel : null
      ports: [
        {
          port: containerPort
          protocol: 'TCP'
        }
      ]
    }
    osType: 'Linux'
    restartPolicy: restartPolicy
    // Log Analytics integration (if workspace provided)
    diagnostics: !empty(logAnalyticsWorkspaceId) ? {
      logAnalytics: {
        workspaceId: logAnalyticsWorkspaceId
        workspaceKey: logAnalyticsWorkspaceKey
        logType: 'ContainerInstanceLogs'
      }
    } : null
    // Volumes - include Azure File share when storageAccountName provided
    volumes: !empty(storageAccountName) ? [
      {
        name: 'models-volume'
        azureFile: {
          shareName: fileShareName
          storageAccountName: storageAccountName
          storageAccountKey: storageAccountKey
        }
      }
    ] : []
  }
}

// Outputs
@description('The FQDN of the container group (if public IP enabled)')
output fqdn string = enablePublicIp && !empty(containerGroup.properties.ipAddress.fqdn) ? containerGroup.properties.ipAddress.fqdn : 'N/A - Private IP only'

@description('The IP address of the container group (public or private depending on configuration)')
output ipAddress string = containerGroup.properties.ipAddress.ip

@description('The network mode (Public, Private, or Dual)')
output networkMode string = !empty(vnetResourceId) ? (enablePublicIp ? 'Dual (Public + Private VNet)' : 'Private (VNet only)') : 'Public only'

@description('The API endpoint URL (use private IP when in VNet)')
output apiEndpoint string = enablePublicIp && !empty(containerGroup.properties.ipAddress.fqdn) ? 'http://${containerGroup.properties.ipAddress.fqdn}:${containerPort}' : 'http://${containerGroup.properties.ipAddress.ip}:${containerPort}'

@description('The health check URL')
output healthEndpoint string = enablePublicIp && !empty(containerGroup.properties.ipAddress.fqdn) ? 'http://${containerGroup.properties.ipAddress.fqdn}:${containerPort}/health' : 'http://${containerGroup.properties.ipAddress.ip}:${containerPort}/health'

@description('The container group resource ID')
output resourceId string = containerGroup.id

@description('Private IP address (when VNet integration is enabled)')
output privateIpAddress string = !empty(vnetResourceId) ? containerGroup.properties.ipAddress.ip : 'N/A - Not using VNet'
