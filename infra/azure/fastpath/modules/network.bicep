// Network for the fast path: one virtual network, one VM subnet, its network security group, and ONE static
// public IP that the VM uses for both directions. Inbound it is what DNS points at; outbound it is the address
// the mail relay and any partner sees, so it is the one address worth putting on an allow-list.
//
// Web traffic (80 and 443) is open to the internet: nginx on the VM terminates TLS and redirects 80 to 443.
// SSH is CLOSED unless an address is listed. The office has no fixed public address, so a permanent office
// allow-list is not possible; README.md covers the ways in.

@description('Azure region.')
param location string

@description('Short environment name used in resource names.')
param envName string

@description('Source prefixes allowed to SSH (port 22). Empty = no SSH rule at all.')
param sshAllowedSourcePrefixes array

@description('Also deploy Azure Bastion Basic and its subnet.')
param deployBastion bool

@description('Put a CanNotDelete lock on the public IP.')
param lockPublicIp bool

@description('Tags applied to every resource.')
param tags object

var vnetName = 'vnet-portal-${envName}'
var vmSubnetName = 'snet-vm'
var vnetPrefix = '10.30.0.0/24'
var vmSubnetPrefix = '10.30.0.0/26'
var bastionSubnetPrefix = '10.30.0.64/26'

var webRules = [
  {
    name: 'allow-https-from-internet'
    properties: {
      priority: 100
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
    // Kept so a plain http:// link still lands somewhere: nginx answers every port-80 request with a 301 to https.
    name: 'allow-http-from-internet'
    properties: {
      priority: 110
      direction: 'Inbound'
      access: 'Allow'
      protocol: 'Tcp'
      sourceAddressPrefix: 'Internet'
      sourcePortRange: '*'
      destinationAddressPrefix: '*'
      destinationPortRange: '80'
    }
  }
]

var sshRules = empty(sshAllowedSourcePrefixes)
  ? []
  : [
      {
        name: 'allow-ssh-from-listed-admins'
        properties: {
          priority: 200
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefixes: sshAllowedSourcePrefixes
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '22'
        }
      }
    ]

var bastionRules = deployBastion
  ? [
      {
        name: 'allow-ssh-from-bastion'
        properties: {
          priority: 210
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: bastionSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '22'
        }
      }
    ]
  : []

resource nsg 'Microsoft.Network/networkSecurityGroups@2023-11-01' = {
  name: 'nsg-portal-${envName}-vm'
  location: location
  tags: tags
  properties: {
    securityRules: concat(webRules, sshRules, bastionRules)
  }
}

var vmSubnet = {
  name: vmSubnetName
  properties: {
    addressPrefix: vmSubnetPrefix
    networkSecurityGroup: {
      id: nsg.id
    }
    // The backup account admits this subnet only, through this endpoint. No locations list: the default covers
    // the region and its pair, which a geo-redundant account needs.
    serviceEndpoints: [
      {
        service: 'Microsoft.Storage'
      }
    ]
  }
}

var bastionSubnet = {
  name: 'AzureBastionSubnet'
  properties: {
    addressPrefix: bastionSubnetPrefix
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetPrefix
      ]
    }
    subnets: deployBastion ? [vmSubnet, bastionSubnet] : [vmSubnet]
  }
}

resource vmPublicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = {
  name: 'pip-portal-${envName}'
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

resource vmPublicIpLock 'Microsoft.Authorization/locks@2020-05-01' = if (lockPublicIp) {
  scope: vmPublicIp
  name: 'keep-portal-address'
  properties: {
    level: 'CanNotDelete'
    notes: 'DNS and mail-relay allow-lists point at this address. Remove the lock deliberately, never by accident.'
  }
}

resource bastionPublicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = if (deployBastion) {
  name: 'pip-portal-${envName}-bastion'
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

resource bastion 'Microsoft.Network/bastionHosts@2023-11-01' = if (deployBastion) {
  name: 'bas-portal-${envName}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, 'AzureBastionSubnet')
          }
          publicIPAddress: {
            id: bastionPublicIp.id
          }
        }
      }
    ]
  }
}

output vmSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnet.name, vmSubnetName)
output publicIpId string = vmPublicIp.id
output publicIpAddress string = vmPublicIp.properties.ipAddress
