// Key Vault, container registry and the Log Analytics workspace.
//
// These are the resources the host and the pipeline authenticate against. None of them
// holds PHI: the vault holds secrets, the registry holds images, the workspace holds
// logs - and the logs are the reason PII logging has to be fixed before anything is
// shipped to it. See the note on the workspace below.

@description('Azure region for every resource in this module.')
param location string

@description('Short environment name used in resource names, e.g. prod or pilot.')
param envName string

@description('Globally unique suffix so vault and registry names do not collide.')
param uniqueSuffix string

@description('Subnet that holds private endpoints.')
param privateEndpointSubnetId string

@description('Private DNS zone for Key Vault.')
param keyVaultPrivateDnsZoneId string

@description('Container registry SKU. Basic at the Small tier, Standard above it.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param registrySku string = 'Basic'

@description('Days Log Analytics keeps data before it is billed as long-term retention.')
@minValue(30)
@maxValue(730)
param logRetentionDays int = 90

@description('Tags applied to every resource.')
param tags object = {}

// Names are PARAMETERS rather than locals so the caller can compute them too. Role
// assignments need their scope resolvable before the deployment starts, which rules out
// reading a name back out of a module output.
@description('Key Vault name. Must be globally unique.')
param keyVaultName string = 'kv-portal-${envName}-${uniqueSuffix}'

@description('Container registry name. Must be globally unique, alphanumeric only.')
param registryName string = 'acrportal${envName}${uniqueSuffix}'

@description('Name of the Key Vault key that wraps the application data-protection key ring.')
param dataProtectionKeyName string = 'dataprotection'

var workspaceName = 'log-portal-${envName}'

// ---------------------------------------------------------------- Key Vault
//
// RBAC rather than access policies: access policies are per-object and drift, whereas
// role assignments are visible in one place and auditable. purgeProtection is ON and
// cannot be turned off again - that is the point, and it is what stops a deleted vault
// taking the sign-in certificate with it.

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

// The key that wraps the application's data-protection key ring (the ring itself lives in the
// host database). A key created through ARM is create-only: on a redeploy the existing key is
// retrieved and nothing is written, so a deployment can never rotate it underneath the
// application. ARM goes through the control plane, which the vault's firewall does not cover,
// so the private-only vault is not an obstacle. Wrap and unwrap are the only operations allowed.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: dataProtectionKeyName
  properties: {
    kty: 'RSA'
    keySize: 3072
    keyOps: [
      'wrapKey'
      'unwrapKey'
    ]
  }
}

resource keyVaultEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-keyvault-${envName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'keyvault'
        properties: {
          privateLinkServiceId: keyVault.id
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

resource keyVaultEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: keyVaultEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZoneId
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- registry
//
// adminUserEnabled stays FALSE. The admin user is a shared password that cannot be
// attributed to anyone and cannot be rotated without breaking every puller at once;
// the host uses its managed identity and an AcrPull role assignment instead.
//
// roleAssignmentMode is PINNED to the legacy registry permissions. In the ABAC repository
// mode the AcrPull and AcrPush roles are not honoured, and Microsoft says ABAC will become
// the default - so leaving it unset would one day make the host's pulls and the pipeline's
// pushes fail with insufficient_scope while every role assignment still looked correct.

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: registrySku
  }
  properties: {
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    roleAssignmentMode: 'LegacyRegistryPermissions'
  }
}

// ---------------------------------------------------------------- logs
//
// WARNING carried from the design pack, and it is a gate not a footnote: the
// application currently writes full user claims - including email and names - into its
// logs. Until that is fixed, shipping application logs to this workspace moves PHI into
// it, and the workspace then inherits every obligation the database has. Ship platform
// and gateway logs first; add application logs only after the PII logging is fixed.

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ---------------------------------------------------------------- outputs

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
// Versionless, for DataProtection:KeyVaultKeyId: new wraps use the current version, and
// older versions stay available to unwrap what they wrapped.
output dataProtectionKeyUri string = dataProtectionKey.properties.keyUri
output registryId string = registry.id
output registryName string = registry.name
output registryLoginServer string = registry.properties.loginServer
output workspaceId string = workspace.id
output workspaceCustomerId string = workspace.properties.customerId
