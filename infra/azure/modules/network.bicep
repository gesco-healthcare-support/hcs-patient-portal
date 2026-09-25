// Virtual network, subnets, network security groups and the private DNS zones the
// private endpoints need.
//
// Layout: nothing except the Application Gateway is reachable from the internet. The
// application host has no public IP at all; it is reached through the gateway, and
// administered through Bastion or a jump path that is deliberately NOT created here.
//
// The gateway subnet must be dedicated to the Application Gateway - Azure refuses to
// place other resources in it - and must be at least /24 for a v2 SKU to scale.

@description('Azure region for every resource in this module.')
param location string

@description('Short environment name used in resource names, e.g. prod or pilot.')
param envName string

@description('Address space for the whole virtual network.')
param vnetAddressPrefix string = '10.20.0.0/16'

@description('Subnet for the Application Gateway. Must be dedicated and at least /24 for v2.')
param gatewaySubnetPrefix string = '10.20.1.0/24'

@description('Subnet for the application host.')
param appSubnetPrefix string = '10.20.2.0/24'

@description('Subnet holding the private endpoints for SQL, cache and storage.')
param privateEndpointSubnetPrefix string = '10.20.3.0/24'

@description('Tags applied to every resource.')
param tags object = {}

var vnetName = 'vnet-portal-${envName}'
var gatewaySubnetName = 'snet-gateway'
var appSubnetName = 'snet-app'
var privateEndpointSubnetName = 'snet-private-endpoints'

// ---------------------------------------------------------------- security groups

// The gateway subnet NSG must allow the Application Gateway control-plane ports or
// Azure marks the gateway unhealthy and it stops serving. 65200-65535 is the v2 range.
resource gatewayNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'nsg-gateway-${envName}'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'allow-gateway-manager'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: 'GatewayManager'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '65200-65535'
          description: 'Application Gateway v2 control plane. Removing this breaks the gateway.'
        }
      }
      {
        name: 'allow-azure-load-balancer'
        properties: {
          priority: 110
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
          description: 'Health probing from the platform load balancer.'
        }
      }
      {
        name: 'allow-https-from-internet'
        properties: {
          priority: 200
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: 'Internet'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
        }
      }
      {
        name: 'allow-http-from-internet'
        properties: {
          priority: 210
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: 'Internet'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '80'
          description: 'Only so the gateway can answer the HTTP-to-HTTPS redirect itself.'
        }
      }
    ]
  }
}

// The application host accepts traffic ONLY from the gateway subnet. It has no public
// IP, so there is no inbound path from the internet even if a rule were wrong.
resource appNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'nsg-app-${envName}'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'allow-https-from-gateway'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: gatewaySubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
          description: 'End-to-end TLS: the gateway speaks HTTPS to nginx on the host.'
        }
      }
      {
        name: 'deny-all-other-inbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
          description: 'Explicit, so the intent is visible rather than relying on the platform default.'
        }
      }
    ]
  }
}

resource privateEndpointNsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'nsg-private-endpoints-${envName}'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'allow-from-app-subnet'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: appSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRanges: [
            '1433' // Azure SQL
            '10000' // Azure Managed Redis, TLS only
            '443' // Blob storage and Key Vault
          ]
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- outbound
//
// The host's outbound traffic (image pulls, OS updates, certificate issuance, the malware
// signature feed, calls to the Case Tracker partner) leaves through a NAT gateway with a
// static address. Azure's implicit "default outbound access" is switched OFF on the app
// subnet below: Microsoft says that address is not recommended for production and can
// change without notice, and a partner allow list cannot be built on an address that moves.
// Only the app subnet: the Application Gateway v2 subnet must keep its own outbound path.
// The flag is set before the host exists because changing it later needs the VM stopped.

resource natPublicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = {
  name: 'pip-nat-${envName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
  }
}

resource natGateway 'Microsoft.Network/natGateways@2023-11-01' = {
  name: 'nat-portal-${envName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    idleTimeoutInMinutes: 4
    publicIpAddresses: [
      {
        id: natPublicIp.id
      }
    ]
  }
}

// ---------------------------------------------------------------- the network

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
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
        name: gatewaySubnetName
        properties: {
          addressPrefix: gatewaySubnetPrefix
          networkSecurityGroup: {
            id: gatewayNsg.id
          }
        }
      }
      {
        name: appSubnetName
        properties: {
          addressPrefix: appSubnetPrefix
          networkSecurityGroup: {
            id: appNsg.id
          }
          natGateway: {
            id: natGateway.id
          }
          defaultOutboundAccess: false
        }
      }
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          networkSecurityGroup: {
            id: privateEndpointNsg.id
          }
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- private DNS
//
// A private endpoint is only useful if the service's public name resolves to the
// private address from inside the network. These zones are what make that true; the
// A records are created by the privateDnsZoneGroup on each endpoint.

// No zone for the container registry on purpose: private endpoints are a PREMIUM ACR
// feature, and this design uses Basic or Standard. The host pulls images over the
// public registry endpoint authenticated by its managed identity, which is acceptable
// because images are not PHI. If the registry is ever moved to Premium, add
// 'privatelink${environment().suffixes.acrLoginServer}' here and an endpoint alongside it.
var privateZoneNames = [
  'privatelink${environment().suffixes.sqlServerHostname}'
  'privatelink.redis.azure.net' // Azure Managed Redis (sub-resource redisEnterprise)
  'privatelink.blob.${environment().suffixes.storage}'
  'privatelink.vaultcore.azure.net'
]

resource privateZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [
  for zoneName in privateZoneNames: {
    name: zoneName
    location: 'global'
    tags: tags
  }
]

resource privateZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [
  for (zoneName, i) in privateZoneNames: {
    name: '${zoneName}/link-${vnetName}'
    location: 'global'
    tags: tags
    properties: {
      registrationEnabled: false
      virtualNetwork: {
        id: vnet.id
      }
    }
    dependsOn: [
      privateZones[i]
    ]
  }
]

// ---------------------------------------------------------------- outputs

output vnetId string = vnet.id
output vnetName string = vnet.name
output gatewaySubnetId string = '${vnet.id}/subnets/${gatewaySubnetName}'
output appSubnetId string = '${vnet.id}/subnets/${appSubnetName}'
output privateEndpointSubnetId string = '${vnet.id}/subnets/${privateEndpointSubnetName}'
output sqlPrivateDnsZoneId string = privateZones[0].id
output redisPrivateDnsZoneId string = privateZones[1].id
output blobPrivateDnsZoneId string = privateZones[2].id
output keyVaultPrivateDnsZoneId string = privateZones[3].id
// The single address the host's outbound traffic comes from, for partner allow lists.
output natPublicIpAddress string = natPublicIp.properties.ipAddress
