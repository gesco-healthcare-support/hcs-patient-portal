// Public IP, Application Gateway (WAF v2) and the public DNS zone.
//
// ====================================================================================
// THE SINGLE MOST IMPORTANT SETTING IN THIS WHOLE TEMPLATE
// ====================================================================================
// The application decides which office a request belongs to from the HOST HEADER. If
// the gateway rewrites it, every request belongs to no office, which is not a degraded
// request - it is the abstention path that leads to host context.
//
// Application Gateway preserves the original Host header when BOTH `hostName` is unset
// and `pickHostNameFromBackendAddress` is false. Microsoft states it directly: "If you
// don't set either value, the original client Host header is passed through unchanged."
// Both are left alone below, deliberately. Do not "helpfully" set either one.
//
// This is the opposite default to an AWS Application Load Balancer, where host
// preservation is OFF unless you enable it - worth knowing if this design is ever
// ported.
//
// Health probes are the exception, and Microsoft is explicit about why: probes are sent
// outside the context of any request, so they cannot derive a host name. The probe
// below therefore sets one explicitly and turns off pickHostNameFromBackendHttpSettings.
// ====================================================================================

@description('Azure region for every resource in this module.')
param location string

@description('Short environment name used in resource names, e.g. prod or pilot.')
param envName string

@description('Subnet dedicated to the Application Gateway.')
param gatewaySubnetId string

@description('Private address of the application host, which is the only backend.')
param backendPrivateIp string

@description('Base public domain, e.g. portal.evaluators.com.')
param baseDomain string

@description('Deploy the Application Gateway, its WAF policies and its diagnostics. False on the FIRST deployment: the certificate the gateway needs is issued on the host after phase 1, into a vault that only the network can reach. The public IP and the DNS zone deploy either way, because issuance proves domain control through that zone.')
param deployGateway bool = false

@description('Key Vault secret id of the wildcard TLS certificate, in versionless form so a renewal is picked up without redeploying. Empty in phase 1; a phase-2 deployment with it empty fails at the gateway.')
@secure()
param tlsCertificateSecretId string = ''

@description('WAF mode for both policies. Detection during burn-in, while false positives are tuned out; Prevention before go-live.')
@allowed([
  'Detection'
  'Prevention'
])
param wafMode string = 'Detection'

@description('Log Analytics workspace that receives the gateway access and firewall logs.')
param workspaceId string

@description('DDoS IP Protection on the gateway public IP. On from the first deployment: its adaptive tuning learns the traffic profile over 7-14 days, so enabling it at go-live would leave the first exposed weeks untuned.')
param enableDdosIpProtection bool = true

@description('Resource id of the user-assigned identity the gateway uses to read the certificate from Key Vault.')
param gatewayIdentityId string

@description('Host name the health probe sends. Must be covered by the wildcard certificate and answered by nginx on a path that does not require an office.')
param probeHostName string

@description('Path the health probe requests. Must sit OUTSIDE the 421 catch-all server block: a server-level `return` runs in NGX_HTTP_SERVER_REWRITE_PHASE, before location selection, so a location inside that block can never answer.')
param probePath string = '/health-status'

@description('Minimum gateway instances. 0 at launch: each instance RESERVES 10 billed capacity units whether or not traffic uses them (about $105/month per instance in West US 2; see COSTS.md), and Microsoft states a v2 gateway stays highly available at 0. A minimum only removes the 3-5 minute scale-out delay on a spike, so raise it once a month of measured CapacityUnits shows the baseline.')
@minValue(0)
param minCapacity int = 0

@description('Maximum gateway instances during autoscale.')
@minValue(2)
param maxCapacity int = 4

@description('Create the public DNS zone. Set false if the zone is delegated from a parent that already exists elsewhere.')
param createDnsZone bool = true

@description('Tags applied to every resource.')
param tags object = {}

var gatewayName = 'agw-portal-${envName}'
var publicIpName = 'pip-portal-${envName}'

// An Application Gateway wildcard host name spans labels: `*.portal.example.com` also
// matches `x.api.portal.example.com` and `minio.portal.example.com`. So the main listener
// needs only the apex and one wildcard. The `*.api` and `*.auth` names are still needed in
// the CERTIFICATE, which is matched per label by TLS clients - not in the listener.
var mainListenerHostNames = [
  baseDomain
  '*.${baseDomain}'
]

// The object-storage host has its own listener and WAF policy. The partner writes whole
// files to it with S3 PUTs, which the main policy's 2 MB body enforcement would block.
// Because the main wildcard also matches this name, its routing rule MUST have a lower
// priority number than the main rule: the lower number is evaluated first.
var minioHostName = 'minio.${baseDomain}'

resource publicIp 'Microsoft.Network/publicIPAddresses@2023-11-01' = {
  name: publicIpName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Regional'
  }
  properties: {
    publicIPAllocationMethod: 'Static'
    publicIPAddressVersion: 'IPv4'
    ddosSettings: {
      protectionMode: enableDdosIpProtection ? 'Enabled' : 'VirtualNetworkInherited'
    }
  }
}

resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = if (createDnsZone) {
  name: baseDomain
  location: 'global'
  tags: tags
  properties: {
    zoneType: 'Public'
  }
}

// Wildcard A records pointing every office name at the gateway. Three records cover the
// whole layout; adding an office needs no DNS change at all, which is the property the
// architecture was built for.
resource apexRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = if (createDnsZone) {
  parent: dnsZone
  name: '@'
  properties: {
    TTL: 3600
    ARecords: [
      {
        ipv4Address: publicIp.properties.ipAddress
      }
    ]
  }
}

resource wildcardRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = if (createDnsZone) {
  parent: dnsZone
  name: '*'
  properties: {
    TTL: 3600
    ARecords: [
      {
        ipv4Address: publicIp.properties.ipAddress
      }
    ]
  }
}

resource apiWildcardRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = if (createDnsZone) {
  parent: dnsZone
  name: '*.api'
  properties: {
    TTL: 3600
    ARecords: [
      {
        ipv4Address: publicIp.properties.ipAddress
      }
    ]
  }
}

resource authWildcardRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = if (createDnsZone) {
  parent: dnsZone
  name: '*.auth'
  properties: {
    TTL: 3600
    ARecords: [
      {
        ipv4Address: publicIp.properties.ipAddress
      }
    ]
  }
}

var gatewayId = resourceId('Microsoft.Network/applicationGateways', gatewayName)

// ---------------------------------------------------------------- WAF policies
//
// Policy RESOURCES, not the gateway's inline WAF configuration: new inline configurations
// have been refused since 2025-03-15 and the form retires on 2027-03-15.
//
// Size limits, and why there are three numbers:
// - maxRequestBodySizeInKb / requestBodyInspectLimitInKB: 2000 KB, the ceiling for CRS 3.2
//   on a policy. The old 128 KB ceiling belonged to the retired inline form.
// - fileUploadLimitInMb: 15, matching nginx's `client_max_body_size 15m` and above the
//   10 MB per-file cap in AppointmentDocumentConsts. It applies to multipart/form-data
//   uploads only. If one of these three changes, change all three.
var managedRules = {
  managedRuleSets: [
    {
      ruleSetType: 'OWASP'
      ruleSetVersion: '3.2'
    }
  ]
}

resource mainWafPolicy 'Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies@2023-11-01' = if (deployGateway) {
  name: 'waf-portal-${envName}'
  location: location
  tags: tags
  properties: {
    policySettings: {
      state: 'Enabled'
      mode: wafMode
      requestBodyCheck: true
      requestBodyEnforcement: true
      requestBodyInspectLimitInKB: 2000
      maxRequestBodySizeInKb: 2000
      fileUploadEnforcement: true
      fileUploadLimitInMb: 15
    }
    managedRules: managedRules
  }
}

// A per-site policy REPLACES the main one for its listener; nothing is inherited. So any
// custom rule the main policy gains must be copied here if it should cover this host too.
// Body enforcement is off because the partner's S3 PUTs carry whole files as the raw body.
// The body is still inspected up to the limit.
resource minioWafPolicy 'Microsoft.Network/ApplicationGatewayWebApplicationFirewallPolicies@2023-11-01' = if (deployGateway) {
  name: 'waf-portal-${envName}-minio'
  location: location
  tags: tags
  properties: {
    policySettings: {
      state: 'Enabled'
      mode: wafMode
      requestBodyCheck: true
      requestBodyEnforcement: false
      requestBodyInspectLimitInKB: 2000
      maxRequestBodySizeInKb: 2000
      fileUploadEnforcement: true
      fileUploadLimitInMb: 15
    }
    managedRules: managedRules
  }
}

// ---------------------------------------------------------------- gateway

resource gateway 'Microsoft.Network/applicationGateways@2023-11-01' = if (deployGateway) {
  name: gatewayName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${gatewayIdentityId}': {}
    }
  }
  properties: {
    sku: {
      name: 'WAF_v2'
      tier: 'WAF_v2'
    }
    autoscaleConfiguration: {
      minCapacity: minCapacity
      maxCapacity: maxCapacity
    }
    enableHttp2: true
    firewallPolicy: {
      id: mainWafPolicy.id
    }
    gatewayIPConfigurations: [
      {
        name: 'gateway-ip'
        properties: {
          subnet: {
            id: gatewaySubnetId
          }
        }
      }
    ]
    frontendIPConfigurations: [
      {
        name: 'public-frontend'
        properties: {
          publicIPAddress: {
            id: publicIp.id
          }
        }
      }
    ]
    frontendPorts: [
      {
        name: 'port-80'
        properties: {
          port: 80
        }
      }
      {
        name: 'port-443'
        properties: {
          port: 443
        }
      }
    ]
    sslCertificates: [
      {
        name: 'wildcard'
        properties: {
          keyVaultSecretId: tlsCertificateSecretId
        }
      }
    ]
    sslPolicy: {
      policyType: 'Predefined'
      policyName: 'AppGwSslPolicy20220101'
    }
    backendAddressPools: [
      {
        name: 'app-host'
        properties: {
          backendAddresses: [
            {
              ipAddress: backendPrivateIp
            }
          ]
        }
      }
    ]
    probes: [
      {
        name: 'nginx-liveness'
        properties: {
          protocol: 'Https'
          // Explicit host: a probe has no request to derive one from.
          host: probeHostName
          pickHostNameFromBackendHttpSettings: false
          path: probePath
          interval: 30
          timeout: 10
          unhealthyThreshold: 3
          match: {
            statusCodes: [
              '200-399'
            ]
          }
        }
      }
    ]
    backendHttpSettingsCollection: [
      {
        name: 'to-nginx-https'
        properties: {
          // HTTPS, not HTTP. nginx's port 80 server block returns 301 to https for every
          // host, so forwarding plain HTTP to the host would produce a redirect loop.
          protocol: 'Https'
          port: 443
          cookieBasedAffinity: 'Disabled'
          requestTimeout: 60
          pickHostNameFromBackendAddress: false
          // hostName deliberately NOT set. See the banner at the top of this file.
          probe: {
            id: '${gatewayId}/probes/nginx-liveness'
          }
        }
      }
    ]
    httpListeners: [
      {
        name: 'https-listener'
        properties: {
          frontendIPConfiguration: {
            id: '${gatewayId}/frontendIPConfigurations/public-frontend'
          }
          frontendPort: {
            id: '${gatewayId}/frontendPorts/port-443'
          }
          protocol: 'Https'
          sslCertificate: {
            id: '${gatewayId}/sslCertificates/wildcard'
          }
          hostNames: mainListenerHostNames
          requireServerNameIndication: true
        }
      }
      {
        name: 'minio-https-listener'
        properties: {
          frontendIPConfiguration: {
            id: '${gatewayId}/frontendIPConfigurations/public-frontend'
          }
          frontendPort: {
            id: '${gatewayId}/frontendPorts/port-443'
          }
          protocol: 'Https'
          sslCertificate: {
            id: '${gatewayId}/sslCertificates/wildcard'
          }
          hostNames: [
            minioHostName
          ]
          requireServerNameIndication: true
          firewallPolicy: {
            id: minioWafPolicy.id
          }
        }
      }
      {
        name: 'http-listener'
        properties: {
          frontendIPConfiguration: {
            id: '${gatewayId}/frontendIPConfigurations/public-frontend'
          }
          frontendPort: {
            id: '${gatewayId}/frontendPorts/port-80'
          }
          protocol: 'Http'
          // The wildcard spans labels, so this one listener redirects every host, minio included.
          hostNames: mainListenerHostNames
        }
      }
    ]
    redirectConfigurations: [
      {
        name: 'http-to-https'
        properties: {
          redirectType: 'Permanent'
          targetListener: {
            id: '${gatewayId}/httpListeners/https-listener'
          }
          includePath: true
          includeQueryString: true
        }
      }
    ]
    requestRoutingRules: [
      {
        // 50, BELOW the main rule's 100: the main wildcard also matches this host, and the
        // lower priority number is evaluated first. Above 100 this rule would never fire.
        name: 'minio-https-rule'
        properties: {
          ruleType: 'Basic'
          priority: 50
          httpListener: {
            id: '${gatewayId}/httpListeners/minio-https-listener'
          }
          backendAddressPool: {
            id: '${gatewayId}/backendAddressPools/app-host'
          }
          backendHttpSettings: {
            id: '${gatewayId}/backendHttpSettingsCollection/to-nginx-https'
          }
        }
      }
      {
        name: 'https-rule'
        properties: {
          ruleType: 'Basic'
          priority: 100
          httpListener: {
            id: '${gatewayId}/httpListeners/https-listener'
          }
          backendAddressPool: {
            id: '${gatewayId}/backendAddressPools/app-host'
          }
          backendHttpSettings: {
            id: '${gatewayId}/backendHttpSettingsCollection/to-nginx-https'
          }
        }
      }
      {
        name: 'http-redirect-rule'
        properties: {
          ruleType: 'Basic'
          priority: 200
          httpListener: {
            id: '${gatewayId}/httpListeners/http-listener'
          }
          redirectConfiguration: {
            id: '${gatewayId}/redirectConfigurations/http-to-https'
          }
        }
      }
    ]
  }
}

// Access and firewall logs plus metrics, in RESOURCE-SPECIFIC tables ('Dedicated'), so the
// AGWAccessLogs and AGWFirewallLogs tables that WAF tuning queries are populated. Without
// 'Dedicated' the rows land in the shared AzureDiagnostics table instead. v2 has no
// separate performance log; its metrics cover that.
resource gatewayDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (deployGateway) {
  scope: gateway
  name: 'to-log-analytics'
  properties: {
    workspaceId: workspaceId
    logAnalyticsDestinationType: 'Dedicated'
    logs: [
      {
        category: 'ApplicationGatewayAccessLog'
        enabled: true
      }
      {
        category: 'ApplicationGatewayFirewallLog'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output gatewayName string = deployGateway ? gatewayName : ''
output publicIpAddress string = publicIp.properties.ipAddress
output publicIpFqdnId string = publicIp.id
// Safe dereference: when createDnsZone is false the resource does not exist, and a
// plain property access on it fails the whole deployment rather than yielding empty.
output dnsZoneName string = dnsZone.?name ?? ''
output dnsNameServers array = dnsZone.?properties.?nameServers ?? []
