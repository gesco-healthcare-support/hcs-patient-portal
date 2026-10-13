// Records in the EXISTING public DNS zone for the base domain. The zone itself is created by hand first, because
// its four name servers have to be handed to whoever runs the parent domain before anything resolves, and that
// exchange starts before any VM exists. This module adds records and never creates or deletes the zone.
//
//   @        the base domain itself
//   *        every office's application and the single-label service hosts (admin, minio, health, ...)
//   *.api    every office's API
//   *.auth   every office's sign-in server
//   CAA      only Let's Encrypt may issue certificates for this name

@description('Name of the existing DNS zone (the base domain).')
param zoneName string

@description('Address every name points at: the VM public IP.')
param ipAddress string

@description('Optional address for CAA violation reports. Empty = none.')
param caaReportEmail string

@description('TTL in seconds for the address records. Short, so a changed address takes effect quickly.')
param addressTtl int = 300

var addressRecordNames = [
  '@'
  '*'
  '*.api'
  '*.auth'
]

var caaIssuers = [
  {
    flags: 0
    tag: 'issue'
    value: 'letsencrypt.org'
  }
  {
    flags: 0
    tag: 'issuewild'
    value: 'letsencrypt.org'
  }
]

var caaReports = empty(caaReportEmail)
  ? []
  : [
      {
        flags: 0
        tag: 'iodef'
        value: 'mailto:${caaReportEmail}'
      }
    ]

resource zone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: zoneName
}

resource addressRecords 'Microsoft.Network/dnsZones/A@2018-05-01' = [
  for recordName in addressRecordNames: {
    parent: zone
    name: recordName
    properties: {
      TTL: addressTtl
      ARecords: [
        {
          ipv4Address: ipAddress
        }
      ]
    }
  }
]

resource caa 'Microsoft.Network/dnsZones/CAA@2018-05-01' = {
  parent: zone
  name: '@'
  properties: {
    TTL: 3600
    caaRecords: concat(caaIssuers, caaReports)
  }
}

output nameServers array = zone.properties.nameServers
