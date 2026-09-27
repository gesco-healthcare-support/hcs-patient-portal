// The application host: one Linux VM running the same containers the office box runs
// today, minus SQL Server and Redis. MinIO stays on this host with BOTH buckets for now
// (decision 16), on the zone-redundant data disk below.
//
// WHY A VM AND NOT A CONTAINER SERVICE. App Service multi-container, which would have
// taken the compose file directly, retires on 2027-03-31, and its replacement puts every
// container on one localhost so every internal address changes. A VM keeps
// docker-compose.prod.yml working essentially as written, which is the smallest possible
// change on the application side during a platform move.
//
// The host has NO public IP. It is reached through the Application Gateway for traffic,
// and for administration through a path this template deliberately does not create -
// choose Bastion or a jump host consciously rather than inheriting one.

@description('Azure region for every resource in this module.')
param location string

@description('Short environment name used in resource names, e.g. prod or pilot.')
param envName string

@description('Subnet the host sits in.')
param appSubnetId string

@description('VM size. D4s_v5 is 4 vCPU / 16 GiB. The compose file already declares roughly 6.5 GiB of limits once the database container is gone, and the upload malware scanner (ClamAV) needs 3 GiB minimum and 4 GiB preferred on top of that, which does not fit in the 8 GiB of a D2s_v5.')
param vmSize string = 'Standard_D4s_v5'

@description('Administrator user name for the host.')
param adminUsername string = 'apadmin'

@description('SSH public key for the administrator. Password authentication is disabled outright.')
@secure()
param adminSshPublicKey string

@description('Data disk size in GiB. 128 is the P10 the design prices; 512 (P20) is the step up.')
param dataDiskSizeGb int = 128

@description('Log Analytics workspace the host agent reports to.')
param workspaceId string

@description('Static private address for the host, inside the application subnet. Static rather than dynamic because the Application Gateway backend pool points at this address; a dynamic address can change across a deallocate and silently empty the pool.')
param privateIpAddress string = '10.20.2.10'

@description('Tags applied to every resource.')
param tags object = {}

var vmName = 'vm-portal-${envName}'
var nicName = 'nic-portal-${envName}'

// cloud-init installs the container runtime and prepares the data disk mount point.
// It deliberately does NOT clone the repository or start anything: bringing the stack
// up is a runbook step a human performs with secrets in hand, not something that
// happens silently on first boot.
var cloudInit = '''
#cloud-config
package_update: true
package_upgrade: true
packages:
  - ca-certificates
  - curl
  - gnupg
  - jq
write_files:
  - path: /etc/docker/daemon.json
    content: |
      {
        "log-driver": "json-file",
        "log-opts": { "max-size": "50m", "max-file": "5" },
        "live-restore": true
      }
runcmd:
  - install -m 0755 -d /etc/apt/keyrings
  - curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  - chmod a+r /etc/apt/keyrings/docker.asc
  - echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu noble stable" > /etc/apt/sources.list.d/docker.list
  - apt-get update
  - apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  - usermod -aG docker apadmin
  - systemctl enable --now docker
  - mkdir -p /srv/portal
  - chown apadmin:apadmin /srv/portal
'''

resource nic 'Microsoft.Network/networkInterfaces@2023-11-01' = {
  name: nicName
  location: location
  tags: tags
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          privateIPAllocationMethod: 'Static'
          privateIPAddress: privateIpAddress
          subnet: {
            id: appSubnetId
          }
          // No publicIPAddress on purpose.
        }
      }
    ]
  }
}

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: vmName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    osProfile: {
      computerName: vmName
      adminUsername: adminUsername
      customData: base64(cloudInit)
      linuxConfiguration: {
        disablePasswordAuthentication: true
        ssh: {
          publicKeys: [
            {
              path: '/home/${adminUsername}/.ssh/authorized_keys'
              keyData: adminSshPublicKey
            }
          ]
        }
        patchSettings: {
          patchMode: 'AutomaticByPlatform'
          assessmentMode: 'AutomaticByPlatform'
        }
      }
    }
    storageProfile: {
      imageReference: {
        publisher: 'Canonical'
        offer: 'ubuntu-24_04-lts'
        sku: 'server'
        version: 'latest'
      }
      osDisk: {
        name: '${vmName}-osdisk'
        createOption: 'FromImage'
        diskSizeGB: 64
        managedDisk: {
          storageAccountType: 'Premium_LRS'
        }
        caching: 'ReadWrite'
      }
      dataDisks: [
        {
          // Zone-redundant: this disk holds MinIO, which keeps BOTH buckets for now
          // (decision 16), so it is the only copy of documents between mirror runs. ZRS
          // survives the loss of one zone; LRS lives in one datacenter. The OS disk stays
          // LRS because it is rebuilt from the image, not restored.
          name: '${vmName}-datadisk'
          lun: 0
          createOption: 'Empty'
          diskSizeGB: dataDiskSizeGb
          managedDisk: {
            storageAccountType: 'Premium_ZRS'
          }
          caching: 'None'
        }
      ]
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: nic.id
        }
      ]
    }
    securityProfile: {
      securityType: 'TrustedLaunch'
      uefiSettings: {
        secureBootEnabled: true
        vTpmEnabled: true
      }
    }
    diagnosticsProfile: {
      bootDiagnostics: {
        enabled: true
      }
    }
  }
}

// The monitoring agent. Syslog only at this point (the rule below) - see the warning in
// platform.bicep about application logs carrying user claims.
resource monitorAgent 'Microsoft.Compute/virtualMachines/extensions@2024-07-01' = {
  parent: vm
  name: 'AzureMonitorLinuxAgent'
  location: location
  properties: {
    publisher: 'Microsoft.Azure.Monitor'
    type: 'AzureMonitorLinuxAgent'
    typeHandlerVersion: '1.0'
    autoUpgradeMinorVersion: true
    enableAutomaticUpgrade: true
  }
}

// The agent collects nothing until a data collection rule is associated with the VM.
// SYSLOG ONLY for now: the containers log through Docker's json-file driver, not syslog,
// so nothing written by the application reaches the workspace through this rule.
// Container logs are added only after the PHI check on them (plan item B10).
// Sign-in activity (auth, authpriv) is kept from Info up for the access trail; every other
// facility from Warning up.
resource syslogRule 'Microsoft.Insights/dataCollectionRules@2023-03-11' = {
  name: 'dcr-portal-${envName}-syslog'
  location: location
  tags: tags
  kind: 'Linux'
  properties: {
    dataSources: {
      syslog: [
        {
          name: 'access-trail'
          streams: [
            'Microsoft-Syslog'
          ]
          facilityNames: [
            'auth'
            'authpriv'
          ]
          logLevels: [
            'Info'
            'Notice'
            'Warning'
            'Error'
            'Critical'
            'Alert'
            'Emergency'
          ]
        }
        {
          name: 'system-warnings'
          streams: [
            'Microsoft-Syslog'
          ]
          facilityNames: [
            'cron'
            'daemon'
            'kern'
            'syslog'
            'user'
          ]
          logLevels: [
            'Warning'
            'Error'
            'Critical'
            'Alert'
            'Emergency'
          ]
        }
      ]
    }
    destinations: {
      logAnalytics: [
        {
          name: 'workspace'
          workspaceResourceId: workspaceId
        }
      ]
    }
    dataFlows: [
      {
        streams: [
          'Microsoft-Syslog'
        ]
        destinations: [
          'workspace'
        ]
      }
    ]
  }
}

resource syslogRuleAssociation 'Microsoft.Insights/dataCollectionRuleAssociations@2023-03-11' = {
  scope: vm
  name: 'syslog'
  properties: {
    dataCollectionRuleId: syslogRule.id
  }
}

output vmId string = vm.id
output vmName string = vm.name
output principalId string = vm.identity.principalId
output privateIpAddress string = nic.properties.ipConfigurations[0].properties.privateIPAddress
output workspaceIdEcho string = workspaceId
