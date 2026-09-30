// Patient Portal - Azure infrastructure, "Standard" tier of the design pack.
//
// Deploy at resource-group scope:
//   az deployment group create -g <rg> -f main.bicep -p @main.parameters.json
//
// THE FIRST DEPLOYMENT IS TWO PHASES. The gateway needs the TLS certificate from Key Vault,
// the certificate is issued on the host after the host exists, and the vault is reachable
// only from inside the network. So:
//   phase 1  deployGateway=false - everything except the gateway, its WAF policies and
//            its diagnostics. The public IP and the DNS zone ARE created: issuance proves
//            domain control through that zone.
//   (issue the certificate into Key Vault from the host)
//   phase 2  deployGateway=true with tlsCertificateSecretId set.
//
// WHAT THIS BUILDS, and what it deliberately does not:
//
//   Builds   virtual network and subnets, network security groups, private DNS, a NAT
//              gateway for the host's outbound traffic
//            Key Vault (with the data-protection wrapping key), container registry,
//              Log Analytics
//            Azure SQL logical server + Standard elastic pool + the HOST database
//            Azure Managed Redis, documents and backups storage accounts
//            one Linux application host with no public address
//            Application Gateway WAF v2 with WAF policies, public IP, public DNS zone
//            the role assignments that let the host and gateway authenticate
//
//   Does not the office databases - adding an office is a portal action (D22)
//            the TLS certificate itself - see the two phases above
//            any administrative access path to the host - Bastion is a separate item
//            MinIO - it stays on the host in the compose file, with both buckets
//              (decision 16)
//
// TIER: this is the Standard tier - managed firewall and managed cache, ONE host.
// D19 chose a single host at launch. Note that deferring high availability is not
// symmetric on Azure: zone redundancy is not offered on Basic or Standard DTU, so
// adding it later is a database migration and a price step, not a setting.

targetScope = 'resourceGroup'

// ---------------------------------------------------------------- parameters

@description('Azure region. The design prices West US 2; D13 settled on US-only, not California specifically.')
param location string = resourceGroup().location

@description('Short environment name used in resource names, e.g. prod or pilot.')
@minLength(2)
@maxLength(8)
param envName string = 'pilot'

@description('Base public domain, e.g. portal.evaluators.com.')
param baseDomain string

@description('SQL administrator login name.')
param sqlAdminLogin string

@description('SQL administrator password. Pass from a pipeline secret or Key Vault reference, never from a file in the repository.')
@secure()
param sqlAdminPassword string

@description('Object id of the Entra group to set as SQL server administrator. REQUIRED: only an Entra admin can create the host identity\'s Entra login, and a human then never needs the SQL password.')
@minLength(36)
@maxLength(36)
param sqlEntraAdminObjectId string

@description('Display name of the Entra SQL administrator group.')
@minLength(1)
param sqlEntraAdminName string

@description('SSH public key for the host administrator account.')
@secure()
param adminSshPublicKey string

@description('Deploy the Application Gateway. False for phase 1 of the first deployment (see the header), true from phase 2 on.')
param deployGateway bool = false

@description('Key Vault secret id of the wildcard TLS certificate. Versionless, so certificate renewal does not need a redeployment. Empty in phase 1.')
@secure()
param tlsCertificateSecretId string = ''

@description('WAF mode. Detection during burn-in while false positives are tuned out; Prevention before go-live.')
@allowed([
  'Detection'
  'Prevention'
])
param wafMode string = 'Detection'

@description('DDoS IP Protection on the gateway public IP. On from the first deployment so its tuning learns the traffic profile before go-live.')
param enableDdosIpProtection bool = true

@description('Azure Managed Redis size. Balanced_B0 (0.5 GB) at launch; the cache can scale up but not down.')
@allowed([
  'Balanced_B0'
  'Balanced_B1'
  'Balanced_B3'
  'Balanced_B5'
])
param redisSkuName string = 'Balanced_B0'

@description('Host name the gateway health probe sends. Must be covered by the wildcard certificate.')
param probeHostName string = 'health.${baseDomain}'

@description('Container registry SKU.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param registrySku string = 'Basic'

@description('Elastic pool eDTU capacity. 100 at launch, 200 is the priced step.')
@allowed([
  50
  100
  200
])
param elasticPoolCapacity int = 100

@description('Host size. D4s_v5 (4 vCPU, 16 GiB): the upload malware scanner needs 3-4 GiB on top of the roughly 6.5 GiB the compose file already declares, which does not fit a D2s_v5.')
param vmSize string = 'Standard_D4s_v5'

@description('Create the public DNS zone here. Set false when the zone is delegated from a parent zone that already exists.')
param createDnsZone bool = true

@description('Tags applied to every resource.')
param tags object = {
  application: 'patient-portal'
  environment: envName
  managedBy: 'bicep'
  dataClassification: 'phi'
}

// Short, stable, deterministic per resource group - so names do not change on redeploy.
var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 6)

// Names are computed HERE and passed down, not read back out of module outputs. A role
// assignment's scope and name must be resolvable before the deployment starts, and a
// module output is not - so owning the names at this level is what makes the access
// section below compile at all.
var keyVaultName = 'kv-portal-${envName}-${uniqueSuffix}'
var registryName = 'acrportal${envName}${uniqueSuffix}'
var documentsAccountName = 'stdocs${envName}${uniqueSuffix}'
var backupsAccountName = 'stbackup${envName}${uniqueSuffix}'
var redisName = 'redis-portal-${envName}-${uniqueSuffix}'
var dataProtectionKeyName = 'dataprotection'

// Built-in role definition ids.
var roleAcrPull = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var roleKeyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var roleKeyVaultCertificateUser = 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba'
var roleKeyVaultCryptoServiceEncryptionUser = 'e147488a-f6f5-4113-8e2d-b22465e65bf6'
var roleStorageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// ---------------------------------------------------------------- identity
//
// The gateway needs a USER-assigned identity specifically: reading a certificate from
// Key Vault is not supported with a system-assigned identity on Application Gateway.

resource gatewayIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-gateway-${envName}'
  location: location
  tags: tags
}

// ---------------------------------------------------------------- modules

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    location: location
    envName: envName
    tags: tags
  }
}

module platform 'modules/platform.bicep' = {
  name: 'platform'
  params: {
    location: location
    envName: envName
    uniqueSuffix: uniqueSuffix
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    keyVaultPrivateDnsZoneId: network.outputs.keyVaultPrivateDnsZoneId
    registrySku: registrySku
    keyVaultName: keyVaultName
    registryName: registryName
    dataProtectionKeyName: dataProtectionKeyName
    tags: tags
  }
}

module data 'modules/data.bicep' = {
  name: 'data'
  params: {
    location: location
    envName: envName
    uniqueSuffix: uniqueSuffix
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    sqlPrivateDnsZoneId: network.outputs.sqlPrivateDnsZoneId
    redisPrivateDnsZoneId: network.outputs.redisPrivateDnsZoneId
    blobPrivateDnsZoneId: network.outputs.blobPrivateDnsZoneId
    sqlAdminLogin: sqlAdminLogin
    sqlAdminPassword: sqlAdminPassword
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    sqlEntraAdminName: sqlEntraAdminName
    elasticPoolCapacity: elasticPoolCapacity
    documentsAccountName: documentsAccountName
    backupsAccountName: backupsAccountName
    redisName: redisName
    redisSkuName: redisSkuName
    tags: tags
  }
}

module host 'modules/host.bicep' = {
  name: 'host'
  params: {
    location: location
    envName: envName
    appSubnetId: network.outputs.appSubnetId
    vmSize: vmSize
    adminSshPublicKey: adminSshPublicKey
    workspaceId: platform.outputs.workspaceId
    tags: tags
  }
}

module edge 'modules/edge.bicep' = {
  name: 'edge'
  params: {
    location: location
    envName: envName
    gatewaySubnetId: network.outputs.gatewaySubnetId
    backendPrivateIp: host.outputs.privateIpAddress
    baseDomain: baseDomain
    deployGateway: deployGateway
    tlsCertificateSecretId: tlsCertificateSecretId
    gatewayIdentityId: gatewayIdentity.id
    probeHostName: probeHostName
    createDnsZone: createDnsZone
    wafMode: wafMode
    workspaceId: platform.outputs.workspaceId
    enableDdosIpProtection: enableDdosIpProtection
    tags: tags
  }
  // Nothing references these grants, so without this the gateway could be created before
  // its identity can read the certificate, and fail. RBAC propagation can still lag
  // behind a completed assignment; phase 2 of the first deployment absorbs that.
  dependsOn: [
    gatewayReadsSecrets
    gatewayReadsCertificates
  ]
}

// ---------------------------------------------------------------- access
//
// Every one of these replaces a stored credential. The host pulls images, reads secrets,
// wraps keys, uses the cache and writes documents as itself; nothing here needs a
// password in a file.

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: registryName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' existing = {
  parent: keyVault
  name: dataProtectionKeyName
}

resource documentsAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: documentsAccountName
}

resource redis 'Microsoft.Cache/redisEnterprise@2025-07-01' existing = {
  name: redisName
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' existing = {
  parent: redis
  name: 'default'
}

// Assignment names are derived from the scope and a fixed purpose string, NOT from the
// principal id: a principal id comes from a module output and so is not known before
// the deployment starts, which a role assignment name has to be.
//
// Every scope below is an `existing` reference BY NAME, which creates no dependency on the
// module that creates it. Each assignment that does not already follow that module through
// `host` therefore names it in dependsOn; without it the assignment can run first and fail
// with NotFound.
resource hostPullsImages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, 'host-acr-pull')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleAcrPull)
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource hostReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, 'host-kv-secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultSecretsUser)
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource hostWritesDocuments 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: documentsAccount
  name: guid(documentsAccount.id, 'host-blob-contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      roleStorageBlobDataContributor
    )
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    data
  ]
}

// Wrap and unwrap with the data-protection key, and nothing else: scoped to THAT KEY, not
// the vault. The application reads the key id from DataProtection:KeyVaultKeyId.
resource hostWrapsWithDataProtectionKey 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: dataProtectionKey
  name: guid(dataProtectionKey.id, 'host-kv-crypto-service-encryption-user')
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      roleKeyVaultCryptoServiceEncryptionUser
    )
    principalId: host.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

// The host signs in to the cache with its managed identity (decision 20); the database has
// access keys switched off. 'default' is the built-in access policy.
resource hostUsesCache 'Microsoft.Cache/redisEnterprise/databases/accessPolicyAssignments@2025-07-01' = {
  parent: redisDatabase
  name: 'hostvm'
  properties: {
    accessPolicyName: 'default'
    user: {
      objectId: host.outputs.principalId
    }
  }
  dependsOn: [
    data
  ]
}

// The gateway needs BOTH: the certificate object and the secret behind it.
resource gatewayReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, 'gateway-kv-secrets-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultSecretsUser)
    principalId: gatewayIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    platform
  ]
}

resource gatewayReadsCertificates 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, 'gateway-kv-cert-user')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultCertificateUser)
    principalId: gatewayIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    platform
  ]
}

// ---------------------------------------------------------------- outputs

output publicIpAddress string = edge.outputs.publicIpAddress
output dnsNameServers array = edge.outputs.dnsNameServers
output natPublicIpAddress string = network.outputs.natPublicIpAddress
output sqlServerFqdn string = data.outputs.sqlServerFqdn
output elasticPoolName string = data.outputs.elasticPoolName
output hostDatabaseName string = data.outputs.hostDatabaseName
output redisHostName string = data.outputs.redisHostName
output redisPort int = data.outputs.redisPort
output dataProtectionKeyUri string = platform.outputs.dataProtectionKeyUri
output registryLoginServer string = platform.outputs.registryLoginServer
output keyVaultName string = platform.outputs.keyVaultName
output documentsAccountName string = data.outputs.documentsAccountName
output backupsAccountName string = data.outputs.backupsAccountName
output hostPrivateIpAddress string = host.outputs.privateIpAddress
output hostName string = host.outputs.vmName
