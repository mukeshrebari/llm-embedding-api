// VNet Infrastructure for LLM Embedding API
// This template creates a Virtual Network with a dedicated subnet for Azure Container Instances
// Use this template first to create networking infrastructure before deploying the container

@description('Name of the Virtual Network')
param vnetName string = 'llm-embedding-vnet'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Virtual Network address prefix')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('Subnet name for ACI')
param subnetName string = 'aci-subnet'

@description('Subnet address prefix for ACI')
param subnetAddressPrefix string = '10.0.1.0/24'

@description('Tags to apply to resources')
param tags object = {
  Application: 'LLM-Embedding-API'
  Environment: 'Production'
  ManagedBy: 'Bicep'
}

// Virtual Network
resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: subnetAddressPrefix
          delegations: [
            {
              name: 'delegation'
              properties: {
                serviceName: 'Microsoft.ContainerInstance/containerGroups'
              }
            }
          ]
          serviceEndpoints: [
            {
              service: 'Microsoft.Storage'
              locations: [
                location
              ]
            }
            {
              service: 'Microsoft.KeyVault'
              locations: [
                location
              ]
            }
          ]
        }
      }
    ]
  }
}

// Network Security Group for the ACI subnet
resource nsg 'Microsoft.Network/networkSecurityGroups@2023-05-01' = {
  name: '${subnetName}-nsg'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowHTTPInbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '8080'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          description: 'Allow HTTP traffic to container on port 8080'
        }
      }
      {
        name: 'AllowVNetInbound'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: 'VirtualNetwork'
          description: 'Allow all traffic within VNet'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          priority: 1000
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          description: 'Deny all other inbound traffic'
        }
      }
    ]
  }
}

// Update subnet to reference NSG
resource subnetWithNsg 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' = {
  parent: vnet
  name: subnetName
  properties: {
    addressPrefix: subnetAddressPrefix
    networkSecurityGroup: {
      id: nsg.id
    }
    delegations: [
      {
        name: 'delegation'
        properties: {
          serviceName: 'Microsoft.ContainerInstance/containerGroups'
        }
      }
    ]
    serviceEndpoints: [
      {
        service: 'Microsoft.Storage'
        locations: [
          location
        ]
      }
      {
        service: 'Microsoft.KeyVault'
        locations: [
          location
        ]
      }
    ]
  }
}

// Outputs
@description('Virtual Network resource ID')
output vnetResourceId string = vnet.id

@description('Virtual Network name')
output vnetName string = vnet.name

@description('ACI Subnet resource ID')
output subnetResourceId string = subnetWithNsg.id

@description('ACI Subnet name')
output subnetName string = subnetName

@description('Network Security Group resource ID')
output nsgResourceId string = nsg.id

@description('VNet address space')
output vnetAddressSpace string = vnetAddressPrefix

@description('Subnet address prefix')
output subnetAddressPrefix string = subnetAddressPrefix
