// The stateful services: Azure SQL (logical server plus elastic pool), the managed
// cache, and the storage accounts for documents and for backups.
//
// Every one of these is reachable ONLY over a private endpoint. Public network access
// is disabled on each, which is what makes the "nothing but the gateway is public"
// claim in the design true rather than aspirational.
//
// NOTE ON WHAT IS DELIBERATELY ABSENT: no OFFICE databases are created here. Adding an
// office is a portal action (decision D22) - the application issues
// CREATE DATABASE ... (SERVICE_OBJECTIVE = ELASTIC_POOL(name = ...)) so the new database
// lands INSIDE this pool rather than as a standalone database billed separately. This
// template creates the pool and the HOST database; the application adds the offices.

@description('Azure region for every resource in this module.')
param location string

@description('Short environment name used in resource names, e.g. prod or pilot.')
param envName string

@description('Globally unique suffix so server and storage names do not collide.')
param uniqueSuffix string

@description('Subnet that holds private endpoints.')
param privateEndpointSubnetId string

@description('Private DNS zone for Azure SQL.')
param sqlPrivateDnsZoneId string

@description('Private DNS zone for Azure Managed Redis.')
param redisPrivateDnsZoneId string

@description('Private DNS zone for blob storage.')
param blobPrivateDnsZoneId string

@description('SQL administrator login name.')
param sqlAdminLogin string

@description('SQL administrator password. Supply from Key Vault or a pipeline secret, never a file.')
@secure()
param sqlAdminPassword string

@description('Object id of the Entra group that administers the SQL server. REQUIRED: only an Entra admin can create the host identity\'s Entra login, and a SQL login cannot.')
@minLength(36)
@maxLength(36)
param sqlEntraAdminObjectId string

@description('Display name of the Entra SQL administrator group.')
@minLength(1)
param sqlEntraAdminName string

@description('Name of the host database. The application\'s connection string points at it; office databases are added by the application.')
param hostDatabaseName string = 'CaseEvaluation'

@description('Elastic pool eDTU capacity. 100 at launch; 200 is the planned step.')
@allowed([
  50
  100
  200
])
param elasticPoolCapacity int = 100

@description('Maximum eDTU any single office database may consume, so one office cannot starve the rest.')
param perDatabaseMaxCapacity int = 50

@description('Elastic pool storage in megabytes.')
param elasticPoolStorageMb int = 102400

@description('Days of point-in-time restore retention for the host database. D10 requires recovery within this window. Office databases get the same policy from the deploy workflow.')
@minValue(1)
@maxValue(35)
param pitrRetentionDays int = 14

@description('Azure Managed Redis size. Balanced_B0 (0.5 GB) is the launch size. AMR can scale UP but not down, so start small.')
@allowed([
  'Balanced_B0'
  'Balanced_B1'
  'Balanced_B3'
  'Balanced_B5'
])
param redisSkuName string = 'Balanced_B0'

@description('Azure Managed Redis cluster name. A parameter so the caller can reference the database for its access policy.')
param redisName string = 'redis-portal-${envName}-${uniqueSuffix}'

@description('Tags applied to every resource.')
param tags object = {}

// Names are PARAMETERS rather than locals so the caller can compute them too. Role
// assignments need their scope resolvable before the deployment starts, which rules out
// reading a name back out of a module output.
@description('Storage account for documents. Globally unique, lower-case alphanumeric, 3-24 characters.')
param documentsAccountName string = 'stdocs${envName}${uniqueSuffix}'

@description('Storage account for backups. Globally unique, lower-case alphanumeric, 3-24 characters.')
param backupsAccountName string = 'stbackup${envName}${uniqueSuffix}'

var sqlServerName = 'sql-portal-${envName}-${uniqueSuffix}'
var elasticPoolName = 'pool-portal-${envName}'

// ---------------------------------------------------------------- SQL

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

// Entra admin in addition to the SQL login. Required, not optional: the host's managed
// identity signs in to SQL as an Entra login, and only the Entra admin can create the
// first Entra login - a SQL login cannot. It also means a human never needs the SQL
// password to administer the server.
resource sqlEntraAdmin 'Microsoft.Sql/servers/administrators@2023-08-01-preview' = {
  parent: sqlServer
  name: 'ActiveDirectory'
  properties: {
    administratorType: 'ActiveDirectory'
    login: sqlEntraAdminName
    sid: sqlEntraAdminObjectId
    tenantId: subscription().tenantId
  }
}

// Standard DTU elastic pool. Note for anyone pricing a reservation later: DTU-based
// databases CANNOT be reserved - Azure reservations are vCore-only - so a commitment
// buys nothing on this line. Moving to vCore General Purpose is also what zone
// redundancy would require, since it is not offered on Basic or Standard DTU.
resource elasticPool 'Microsoft.Sql/servers/elasticPools@2023-08-01-preview' = {
  parent: sqlServer
  name: elasticPoolName
  location: location
  tags: tags
  sku: {
    name: 'StandardPool'
    tier: 'Standard'
    capacity: elasticPoolCapacity
  }
  properties: {
    perDatabaseSettings: {
      minCapacity: 0
      maxCapacity: perDatabaseMaxCapacity
    }
    maxSizeBytes: elasticPoolStorageMb * 1024 * 1024
    zoneRedundant: false
  }
}

// The HOST database, declared here so it is created INSIDE the pool. Left to the
// migrator, EF would issue a plain CREATE DATABASE and make a standalone database, billed
// separately. Backups are geo-redundant so a geo-restore is possible; GEOZONE is not
// offered on the DTU model.
resource hostDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: hostDatabaseName
  location: location
  tags: tags
  properties: {
    elasticPoolId: elasticPool.id
    requestedBackupStorageRedundancy: 'Geo'
  }
}

// Point-in-time retention and differential frequency. Without this child the database
// runs on the 7-day / 24-hour defaults, whatever pitrRetentionDays says.
resource hostDatabaseRetention 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01-preview' = {
  parent: hostDatabase
  name: 'default'
  properties: {
    retentionDays: pitrRetentionDays
    diffBackupIntervalInHours: 12
  }
}

resource sqlEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-sql-${envName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'sql'
        properties: {
          privateLinkServiceId: sqlServer.id
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

resource sqlEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: sqlEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql'
        properties: {
          privateDnsZoneId: sqlPrivateDnsZoneId
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- cache
//
// Azure Managed Redis (decision 11): Azure Cache for Redis no longer accepts new caches.
// It holds the framework cache and distributed locks. The application's key ring moves to
// the host database (plan item C2); until then the cache is load-bearing for sign-in.
//
// - High availability ON: it is what the SLA covers, and it spreads the two nodes across
//   zones in a zone-enabled region.
// - Public network access OFF (the property is mandatory from API 2025-07-01).
// - ACCESS KEYS OFF on the database: the host signs in with its managed identity through
//   an Entra access policy (decision 20), which main.bicep assigns.
// - Enterprise clustering: one endpoint, so the client needs no cluster awareness, and
//   multi-key commands work across slots. It is fixed at creation.
// - Port 10000 is set explicitly; the property otherwise defaults to "an available port".

resource redis 'Microsoft.Cache/redisEnterprise@2025-07-01' = {
  name: redisName
  location: location
  tags: tags
  sku: {
    name: redisSkuName
  }
  properties: {
    highAvailability: 'Enabled'
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-07-01' = {
  parent: redis
  name: 'default'
  properties: {
    clientProtocol: 'Encrypted'
    port: 10000
    clusteringPolicy: 'EnterpriseCluster'
    evictionPolicy: 'VolatileLRU'
    accessKeysAuthentication: 'Disabled'
  }
}

resource redisEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-redis-${envName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'redis'
        properties: {
          privateLinkServiceId: redis.id
          groupIds: [
            'redisEnterprise'
          ]
        }
      }
    ]
  }
}

resource redisEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: redisEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'redis'
        properties: {
          privateDnsZoneId: redisPrivateDnsZoneId
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- storage
//
// Two accounts on purpose. Documents hold PHI and want versioning and a short delete
// window so a mistaken delete is recoverable. Backups want the opposite shape: write
// once, keep for years, and resist deletion. Putting both in one account forces one
// lifecycle policy onto two different jobs.
//
// Both are GZRS (decision 15): three zones in this region plus a copy in the paired
// region. The documents account receives the continuous mirror of the MinIO bucket
// (decision 16), so it is the recovery copy - one datacenter is not enough for that.

resource documentsAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: documentsAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_GZRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

resource documentsBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: documentsAccount
  name: 'default'
  properties: {
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: 30
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 30
    }
  }
}

resource documentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: documentsBlobService
  name: 'case-evaluation-documents'
  properties: {
    publicAccess: 'None'
  }
}

resource backupsAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: backupsAccountName
  location: location
  tags: tags
  sku: {
    name: 'Standard_GZRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Cool'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Deny'
    }
  }
}

resource backupsBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: backupsAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 90
    }
  }
}

resource backupsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: backupsBlobService
  name: 'database-backups'
  properties: {
    publicAccess: 'None'
  }
}

resource documentsEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-docs-${envName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'documents'
        properties: {
          privateLinkServiceId: documentsAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource documentsEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: documentsEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob'
        properties: {
          privateDnsZoneId: blobPrivateDnsZoneId
        }
      }
    ]
  }
}

resource backupsEndpoint 'Microsoft.Network/privateEndpoints@2023-11-01' = {
  name: 'pe-backups-${envName}'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: 'backups'
        properties: {
          privateLinkServiceId: backupsAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource backupsEndpointDns 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-11-01' = {
  parent: backupsEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob'
        properties: {
          privateDnsZoneId: blobPrivateDnsZoneId
        }
      }
    ]
  }
}

// ---------------------------------------------------------------- outputs

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output elasticPoolName string = elasticPool.name
output hostDatabaseName string = hostDatabase.name
output redisName string = redis.name
// Clients connect to this name on port 10000, not to the privatelink name.
output redisHostName string = redis.properties.hostName
output redisPort int = redisDatabase.properties.port!
output documentsAccountName string = documentsAccount.name
output documentsAccountId string = documentsAccount.id
output backupsAccountName string = backupsAccount.name
output backupsAccountId string = backupsAccount.id
