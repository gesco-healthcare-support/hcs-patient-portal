// FAST PATH: ONE Azure VM running the same docker-compose.prod.yml stack as the office server -- SQL Server,
// Redis, MinIO and the application as containers, nginx terminating TLS -- plus an off-server backup account
// that keeps database dumps and documents for seven years.
//
// Decided 2026-10-12 to go live quickly. The full design one folder up (Application Gateway with WAF, Azure SQL,
// managed Redis, private endpoints, Bastion) is built later, in stages, without taking this down. Nothing here
// reuses a name the full design creates except the VM's (vm-portal-<env>), so the two can coexist in one group
// during a migration.
//
// DEPLOYED BY HAND by a subscription Owner, never by CI. infra.yml builds and lints this folder; its deploy job
// deploys ../main.bicep only. Read README.md beside this file before the first deployment.

targetScope = 'resourceGroup'

@description('Azure region. Defaults to the resource group region.')
param location string = resourceGroup().location

@description('Short environment name used in resource names.')
@maxLength(8)
param envName string = 'prod'

@description('Public base domain, e.g. portal.example.com. Offices resolve as {office}.<base>, {office}.api.<base> and {office}.auth.<base>. An Azure DNS zone with EXACTLY this name must already exist in this resource group; this template adds records to it and never creates or deletes it.')
param baseDomain string

@description('Administrator user on the VM. apadmin matches the office server, so every unit file in scripts/hosting/systemd works unchanged.')
param adminUsername string = 'apadmin'

@secure()
@description('SSH public key for the administrator. Password sign-in is disabled.')
param adminSshPublicKey string

@description('VM size. Standard_D4s_v5 is 4 vCPU / 16 GiB, the same memory as the office server, which runs this exact stack including SQL Server and the upload malware scanner (3-4 GiB on its own).')
param vmSize string = 'Standard_D4s_v5'

@description('Data disk size in GiB. It holds SQL Server, MinIO (every document) and the Docker images. 128 is a P10, 256 a P15, 512 a P20; a managed disk can grow later but never shrink.')
@allowed([
  128
  256
  512
])
param dataDiskSizeGb int = 128

@description('Data disk redundancy. Premium_ZRS survives the loss of one availability zone; Premium_LRS lives in one datacenter and costs less. Either way the nightly off-server backup is the recovery copy; this decides how likely you are to need it.')
@allowed([
  'Premium_LRS'
  'Premium_ZRS'
])
param dataDiskSku string = 'Premium_ZRS'

@description('Encrypt the VM\'s disk caches and temporary storage on the Azure host itself (encryption at host). Managed disks are always encrypted at rest; without this, the OS disk\'s read/write cache on the host is not. No separate charge. NEEDS a one-time registration on the subscription first: az feature register --namespace Microsoft.Compute --name EncryptionAtHost, then wait until az feature show reports Registered. See README.md.')
param encryptionAtHost bool = true

@description('Source address prefixes (CIDR) allowed to reach SSH on port 22. Empty = SSH is closed to the internet, which is the default. The office has no fixed public address, so the intended use is to add your current address when you need a shell and remove it afterwards. See README.md.')
param sshAllowedSourcePrefixes array = []

@description('Deploy Azure Bastion Basic, for browser SSH with no port open. It is billed for every hour it exists; see COSTS.md. Bastion Developer, the free tier, is not offered in West US 2.')
param deployBastion bool = false

@description('Backup account redundancy. Standard_GRS keeps a second copy in the paired region. Only LRS, GRS and RA-GRS support the Archive tier the database dumps move to, which is why ZRS and GZRS are not offered here.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param backupStorageSku string = 'Standard_GRS'

@description('Globally unique backup storage account name: 3-24 lowercase letters and digits.')
@minLength(3)
@maxLength(24)
param backupStorageAccountName string = 'stportalbk${uniqueString(resourceGroup().id)}'

@description('Age in days at which a database dump moves to the Archive tier: offline, and reading it back takes hours. Must be more than 90 (the Cold step). 0 = never archive.')
param dbBackupArchiveAfterDays int = 365

@description('Soft-delete window for blobs and containers, in days. A deleted blob or container can be restored within it.')
@minValue(7)
@maxValue(365)
param softDeleteDays int = 30

@description('Enable version-level immutability SUPPORT on the backup account. This sets NO retention policy and locks nothing. It only keeps the option open, because account-level support can be switched on when the account is created and never afterwards. Whether to lock backups for seven years is a decision for counsel.')
param enableImmutabilitySupport bool = true

@description('Encrypt the backup account a second time at the storage infrastructure layer, with a separate Microsoft-managed key (double encryption). Like immutability support, this can be chosen ONLY when the account is created. No separate charge in the Retail Prices API. Microsoft recommends it where double encryption is a compliance requirement; it is on here because the account holds seven years of patient data and the choice cannot be revisited.')
param requireInfrastructureEncryption bool = true

@description('Extra address prefixes allowed to reach the backup account directly, for example an administrator browsing it in the portal. The VM subnet is always allowed. Empty = the VM only.')
param backupStorageAllowedIpRanges array = []

@description('Put CanNotDelete locks on the backup account and the public IP. The IP is in DNS and on mail-relay allow-lists, and the account holds the only off-server copy; neither should disappear by accident. A subscription Owner can remove a lock deliberately.')
param lockCriticalResources bool = true

@description('Optional address for CAA violation reports (iodef). Empty = none.')
param caaReportEmail string = ''

@description('Deploy Azure Relay for the Case Tracker\'s document reads (docs/integration/case-tracker-relay.md). OFF by default: turn it on after go-live, before the Case Tracker\'s document reads are switched on. About $10/month for the listener plus $1/GB beyond 5 GB a month; see COSTS.md.')
param deployCaseTrackerRelay bool = false

@description('Globally unique Relay namespace name, used only when deployCaseTrackerRelay is true.')
@minLength(6)
@maxLength(50)
param relayNamespaceName string = 'relay-portal-${uniqueString(resourceGroup().id)}'

@description('Address emailed when the Relay has had no listener for 15 minutes. Empty = no alert. Used only when deployCaseTrackerRelay is true.')
param relayAlertEmail string = ''

@description('Run the outside "site is down" check (modules/uptime.bicep): Azure probes https://health.<baseDomain>/health-status from three regions every 15 minutes and emails uptimeAlertEmail. ON by default; about $4.50/month, see COSTS.md.')
param deployUptimeCheck bool = true

@description('Who is emailed when the site is down or its TLS certificate has under 14 days left. REQUIRED while deployUptimeCheck is true: an empty value fails validation.')
param uptimeAlertEmail string = ''

@description('Seconds between outside checks from each region: 900 (default, about $4.38/month) or 300 (about $13.14/month, faster alerts).')
@allowed([
  300
  900
])
param uptimeFrequencySeconds int = 900

@description('Tags applied to every resource.')
param tags object = {
  workload: 'appointment-portal'
  layout: 'fastpath'
}

var vmName = 'vm-portal-${envName}'

// Built-in role ids (stable across tenants).
var dnsZoneContributorRoleId = 'befefa01-2a29-4197-83a8-272ff33ce314'

// ---------------------------------------------------------------- existing resources referenced here
//
// The backup account and its two containers are created by the backup-storage module in this same deployment;
// these references only let the role assignments below target the containers. The DNS zone is the one created
// by hand beforehand.

resource backupAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: backupStorageAccountName
}

resource backupBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' existing = {
  parent: backupAccount
  name: 'default'
}

resource dbBackupsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' existing = {
  parent: backupBlobService
  name: 'db-backups'
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' existing = {
  parent: backupBlobService
  name: 'documents'
}

// DNS-01 certificate issuance from the VM (Let's Encrypt) writes _acme-challenge TXT records in the zone.
resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: baseDomain
}

// ---------------------------------------------------------------- the VM's rights
//
// The backup script only ever COPIES (scripts/hosting/backup-azure-blob.sh), so the VM gets a role that can
// read, list and write blobs but NOT delete them. With versioning on, an overwrite keeps the old version, so a
// compromised VM can neither delete the backups nor destroy them by overwriting. The built-in Storage Blob Data
// Contributor would include delete; this is the same thing without it.

resource backupWriterRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'portal-backup-writer-no-delete')
  properties: {
    roleName: 'Portal Backup Writer, no delete (${resourceGroup().name})'
    description: 'Read, list and write blobs in the portal backup containers. Deliberately no delete: the backup only ever copies.'
    type: 'CustomRole'
    assignableScopes: [
      resourceGroup().id
    ]
    permissions: [
      {
        actions: [
          'Microsoft.Storage/storageAccounts/blobServices/containers/read'
        ]
        dataActions: [
          'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read'
          'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/write'
          'Microsoft.Storage/storageAccounts/blobServices/containers/blobs/add/action'
        ]
      }
    ]
  }
}

resource dbBackupsWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: dbBackupsContainer
  name: guid(dbBackupsContainer.id, vmName, backupWriterRole.id)
  dependsOn: [
    backup
  ]
  properties: {
    roleDefinitionId: backupWriterRole.id
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource documentsWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: documentsContainer
  name: guid(documentsContainer.id, vmName, backupWriterRole.id)
  dependsOn: [
    backup
  ]
  properties: {
    roleDefinitionId: backupWriterRole.id
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource dnsZoneWriter 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: dnsZone
  name: guid(dnsZone.id, vmName, dnsZoneContributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dnsZoneContributorRoleId)
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------- modules

module network 'modules/network.bicep' = {
  name: 'fastpath-network'
  params: {
    location: location
    envName: envName
    sshAllowedSourcePrefixes: sshAllowedSourcePrefixes
    deployBastion: deployBastion
    lockPublicIp: lockCriticalResources
    tags: tags
  }
}

module host 'modules/host.bicep' = {
  name: 'fastpath-host'
  params: {
    location: location
    vmName: vmName
    subnetId: network.outputs.vmSubnetId
    publicIpId: network.outputs.publicIpId
    vmSize: vmSize
    adminUsername: adminUsername
    adminSshPublicKey: adminSshPublicKey
    dataDiskSizeGb: dataDiskSizeGb
    dataDiskSku: dataDiskSku
    encryptionAtHost: encryptionAtHost
    tags: tags
  }
}

module backup 'modules/backup-storage.bicep' = {
  name: 'fastpath-backup-storage'
  params: {
    location: location
    accountName: backupStorageAccountName
    sku: backupStorageSku
    softDeleteDays: softDeleteDays
    dbBackupArchiveAfterDays: dbBackupArchiveAfterDays
    enableImmutabilitySupport: enableImmutabilitySupport
    requireInfrastructureEncryption: requireInfrastructureEncryption
    vmSubnetId: network.outputs.vmSubnetId
    allowedIpRanges: backupStorageAllowedIpRanges
    lockAccount: lockCriticalResources
    tags: tags
  }
}

module dns 'modules/dns-records.bicep' = {
  name: 'fastpath-dns-records'
  params: {
    zoneName: baseDomain
    ipAddress: network.outputs.publicIpAddress
    caaReportEmail: caaReportEmail
  }
}

module relay 'modules/relay.bicep' = if (deployCaseTrackerRelay) {
  name: 'fastpath-case-tracker-relay'
  params: {
    location: location
    namespaceName: relayNamespaceName
    vmPrincipalId: host.outputs.principalId
    alertEmail: relayAlertEmail
    tags: tags
  }
}

module uptime 'modules/uptime.bicep' = if (deployUptimeCheck) {
  name: 'fastpath-uptime-check'
  params: {
    location: location
    envName: envName
    healthUrl: 'https://health.${baseDomain}/health-status'
    alertEmail: uptimeAlertEmail
    frequencySeconds: uptimeFrequencySeconds
    tags: tags
  }
}

output publicIpAddress string = network.outputs.publicIpAddress
output vmName string = vmName
output vmPrincipalId string = host.outputs.principalId
output backupStorageAccountName string = backup.outputs.accountName
output dnsNameServers array = dns.outputs.nameServers
output relayNamespaceName string = relay.?outputs.namespaceName ?? ''
output relayHybridConnectionName string = relay.?outputs.hybridConnectionName ?? ''
output uptimeWebTestName string = uptime.?outputs.webTestName ?? ''
