// The one VM. It runs the office server's compose stack unchanged: SQL Server, Redis, MinIO, the application,
// the packet renderer, ClamAV and nginx, all as containers. Patterns follow ../../modules/host.bicep (Ubuntu
// 24.04, Trusted Launch, platform patching, a cloud-init that installs Docker and starts nothing), with two
// differences: the NIC carries the public IP, and every Docker volume lives on the data disk.
//
// WHY THE DATA DISK MATTERS. Docker's data root (/srv/docker) holds the SQL Server and MinIO volumes, i.e.
// every appointment and every document. If the data disk were not mounted, Docker would quietly write them to
// the OS disk instead, which nothing backs up as a disk and which is rebuilt from the image. So cloud-init
// STOPS before installing Docker when it cannot find and mount the data disk, rather than carrying on.

@description('Azure region.')
param location string

@description('VM name.')
param vmName string

@description('Subnet the VM sits in.')
param subnetId string

@description('Public IP for the VM NIC.')
param publicIpId string

@description('VM size.')
param vmSize string

@description('Administrator user name.')
param adminUsername string

@secure()
@description('SSH public key for the administrator.')
param adminSshPublicKey string

@description('Data disk size in GiB.')
param dataDiskSizeGb int

@description('Data disk storage type.')
param dataDiskSku string

@description('Encryption at host: the disk caches and temporary storage on the Azure host are encrypted too. Needs the EncryptionAtHost feature registered on the subscription.')
param encryptionAtHost bool

@description('Static private address inside the VM subnet.')
param privateIpAddress string = '10.30.0.10'

@description('Tags applied to every resource.')
param tags object

// cloud-init. Installs the container runtime and mounts the data disk; it deliberately does NOT clone the
// repository or start anything: bringing the stack up is a runbook step done with the secrets in hand.
var cloudInitTemplate = '''
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
        "data-root": "/srv/docker",
        "log-driver": "json-file",
        "log-opts": { "max-size": "50m", "max-file": "5" },
        "live-restore": true
      }
runcmd:
  - |
    set -eu
    dev=""
    for attempt in $(seq 1 30); do
      udevadm settle || true
      for link in /dev/disk/azure/data/by-lun/0 /dev/disk/azure/scsi1/lun0; do
        if [ -e "$link" ]; then dev="$(readlink -f "$link")"; break 2; fi
      done
      sleep 2
    done
    if [ -z "$dev" ]; then
      echo "portal: the LUN 0 data disk was not found; stopping BEFORE Docker is installed onto the OS disk" >&2
      exit 1
    fi
    blkid "$dev" >/dev/null 2>&1 || mkfs.ext4 -L portaldata "$dev"
    mkdir -p /srv
    grep -q 'LABEL=portaldata' /etc/fstab || echo 'LABEL=portaldata /srv ext4 defaults,nofail,discard 0 2' >> /etc/fstab
    mountpoint -q /srv || mount /srv
    mountpoint -q /srv
    mkdir -p /srv/docker /srv/backups
  - install -m 0755 -d /etc/apt/keyrings
  - curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  - chmod a+r /etc/apt/keyrings/docker.asc
  - echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu noble stable" > /etc/apt/sources.list.d/docker.list
  - apt-get update
  - apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  - usermod -aG docker __ADMIN__
  - systemctl enable --now docker
'''

var cloudInit = replace(cloudInitTemplate, '__ADMIN__', adminUsername)

resource nic 'Microsoft.Network/networkInterfaces@2023-11-01' = {
  name: 'nic-${vmName}'
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
            id: subnetId
          }
          publicIPAddress: {
            id: publicIpId
          }
        }
      }
    ]
  }
}

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' = {
  name: vmName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  tags: tags
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
          name: '${vmName}-datadisk'
          lun: 0
          createOption: 'Empty'
          diskSizeGB: dataDiskSizeGb
          managedDisk: {
            storageAccountType: dataDiskSku
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
      // Managed disks are always encrypted at rest with platform-managed keys. Encryption at host extends that to
      // what lives on the Azure host: the OS disk's read/write cache (the data disk uses no cache) and temporary
      // storage. Customer-managed keys (a disk encryption set and a Key Vault) are deliberately NOT used: they add a
      // vault whose loss or a disabled key shuts the VM down, for a control the platform keys already provide.
      encryptionAtHost: encryptionAtHost
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

output vmId string = vm.id
output principalId string = vm.identity.principalId
