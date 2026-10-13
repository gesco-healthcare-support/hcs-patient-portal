// The off-server backup: one storage account, two containers, kept seven years.
//
//   db-backups/<run stamp>/   every database dump of one night, plus MANIFEST.sha256 (written last)
//   documents/<bucket>/       every document in MinIO, copied incrementally
//
// Filled nightly by scripts/hosting/backup-azure-blob.sh, which only ever COPIES. Retention lives here:
//
//   - versioning: an overwrite keeps the previous version, so a changed or replaced object is not lost;
//   - soft delete on blobs and containers: a delete is reversible for softDeleteDays;
//   - lifecycle: objects move to cheaper tiers as they age and are NEVER deleted by a rule. Seven years is
//     the floor, not a timer; anything removed after that is a deliberate act.
//
// DOCUMENTS STOP AT COLD; DATABASE DUMPS GO ON TO ARCHIVE. Cold is online: if the VM's disk were lost,
// documents restore immediately. Archive is offline, and reading back takes hours. That is fine for an old
// dump and wrong for the copy of every document that a restore would need all at once.
//
// NOTHING HERE IS PUBLIC. No shared key (sign-in is Entra only, as the VM's identity), no anonymous access,
// and the network admits only the VM subnet's service endpoint plus any addresses listed explicitly.

@description('Azure region.')
param location string

@description('Storage account name.')
param accountName string

@description('Redundancy SKU: LRS, GRS or RA-GRS (the ones that support the Archive tier).')
param sku string

@description('Soft-delete window for blobs and containers, in days.')
param softDeleteDays int

@description('Age in days at which a database dump moves to Archive. 0 = never.')
param dbBackupArchiveAfterDays int

@description('Enable version-level immutability support at creation (no policy is set).')
param enableImmutabilitySupport bool

@description('Encrypt a second time at the storage infrastructure layer, with a separate key. Creation only.')
param requireInfrastructureEncryption bool

@description('The VM subnet, admitted through its Microsoft.Storage service endpoint.')
param vmSubnetId string

@description('Extra address prefixes admitted, if any.')
param allowedIpRanges array

@description('Put a CanNotDelete lock on the account.')
param lockAccount bool

@description('Tags applied to every resource.')
param tags object

var coolAfterDays = 30
var coldAfterDays = 90

var onlineTiering = {
  tierToCool: {
    daysAfterModificationGreaterThan: coolAfterDays
  }
  tierToCold: {
    daysAfterModificationGreaterThan: coldAfterDays
  }
}

var onlineVersionTiering = {
  tierToCool: {
    daysAfterCreationGreaterThan: coolAfterDays
  }
  tierToCold: {
    daysAfterCreationGreaterThan: coldAfterDays
  }
}

var dbArchive = dbBackupArchiveAfterDays > coldAfterDays
  ? {
      tierToArchive: {
        daysAfterModificationGreaterThan: dbBackupArchiveAfterDays
      }
    }
  : {}

var dbVersionArchive = dbBackupArchiveAfterDays > coldAfterDays
  ? {
      tierToArchive: {
        daysAfterCreationGreaterThan: dbBackupArchiveAfterDays
      }
    }
  : {}

// No managed identity on the account: one is needed only for customer-managed keys, and this account uses
// Microsoft-managed keys. Nothing here signs in AS the account; the VM signs in TO it, with its own identity.
resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: accountName
  location: location
  sku: {
    name: sku
  }
  kind: 'StorageV2'
  tags: tags
  properties: {
    // New blobs land Hot and the lifecycle rules below move them down; Archive cannot be an account default.
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    allowCrossTenantReplication: false
    // Enabled because the VM reaches it through a service endpoint on the public endpoint; networkAcls below
    // refuse everything else. The full design uses private endpoints instead.
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
      virtualNetworkRules: [
        {
          id: vmSubnetId
          action: 'Allow'
        }
      ]
      ipRules: [
        for range in allowedIpRanges: {
          value: range
          action: 'Allow'
        }
      ]
    }
    immutableStorageWithVersioning: {
      enabled: enableImmutabilitySupport
    }
    // Azure Storage always encrypts at rest (AES-256, Microsoft-managed keys); this block states it and adds the
    // second, infrastructure-level layer when requested. Like immutability support, that layer can be chosen only
    // when the account is created, never afterwards.
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: requireInfrastructureEncryption
      services: {
        blob: {
          enabled: true
          keyType: 'Account'
        }
      }
    }
  }
}

resource accountLock 'Microsoft.Authorization/locks@2020-05-01' = if (lockAccount) {
  scope: account
  name: 'keep-portal-backups'
  properties: {
    level: 'CanNotDelete'
    notes: 'The only off-server copy of the portal databases and documents, kept seven years. Remove deliberately.'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: softDeleteDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: softDeleteDays
    }
  }
}

resource dbBackups 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'db-backups'
  properties: {
    publicAccess: 'None'
  }
}

resource documents 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'documents'
  properties: {
    publicAccess: 'None'
  }
}

resource lifecycle 'Microsoft.Storage/storageAccounts/managementPolicies@2023-05-01' = {
  parent: account
  name: 'default'
  dependsOn: [
    dbBackups
    documents
  ]
  properties: {
    policy: {
      rules: [
        {
          enabled: true
          name: 'documents-to-cooler-tiers'
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'documents/'
              ]
            }
            actions: {
              baseBlob: onlineTiering
              version: onlineVersionTiering
            }
          }
        }
        {
          enabled: true
          name: 'db-backups-to-cooler-tiers'
          type: 'Lifecycle'
          definition: {
            filters: {
              blobTypes: [
                'blockBlob'
              ]
              prefixMatch: [
                'db-backups/'
              ]
            }
            actions: {
              baseBlob: union(onlineTiering, dbArchive)
              version: union(onlineVersionTiering, dbVersionArchive)
            }
          }
        }
      ]
    }
  }
}

output accountName string = account.name
output accountId string = account.id
