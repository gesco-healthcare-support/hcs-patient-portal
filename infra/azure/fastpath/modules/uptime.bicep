// The outside "site is down" check. Azure probes the portal's health URL from several regions and emails when it
// fails from enough of them. Everything that can fail on the VM -- the VM itself, Docker, nginx, the API, SQL Server,
// DNS, an expired or nearly expired TLS certificate -- shows up here, because the probe comes from the internet.
// The backup and certificate alerts on the VM cannot report the VM being down; this can.
//
// What it probes: https://health.<base>/health-status. nginx answers only that path on the health host and passes it
// to the API, whose health check includes a database query. A 200 means DNS, TLS, nginx, the API and SQL all work.
//
// Cost (COSTS.md): $0.0005 per test run. Three regions every 15 minutes is 8,760 runs, about $4.38 a month. The
// alert rule, the emails and a few MB of results a month sit inside free tiers.

@description('Azure region for the workspace and Application Insights. The probes themselves run from testLocations.')
param location string

@description('Short environment name, used in resource names.')
param envName string

@description('The URL to probe. It must answer 200 when the portal is healthy.')
param healthUrl string

@description('Who is emailed when the check fails.')
@minLength(6)
param alertEmail string

@description('Seconds between runs from each region. 900 costs about $4.38 a month; 300 costs about $13.14.')
@allowed([
  300
  900
])
param frequencySeconds int = 900

@description('Probe regions (Azure availability-test location ids). Three US regions by default.')
@minLength(2)
param testLocations array = [
  'us-ca-sjc-azr' // West US
  'us-tx-sn1-azr' // South Central US
  'us-va-ash-azr' // East US
]

@description('How many regions must fail before the email goes out. Two of three ignores one region\'s own blip.')
@minValue(1)
param failedLocationThreshold int = 2

@description('The check fails when the TLS certificate has fewer days than this left. renew-tls-cert.sh renews at 30 days, so this fires only when renewal has been failing for weeks.')
@minValue(1)
param certificateWarningDays int = 14

@description('Tags applied to every resource.')
param tags object

var webTestName = 'webtest-portal-${envName}-health'

// Availability results need a workspace-based Application Insights resource. The daily cap is a ceiling against a
// misconfiguration, not a budget: the check writes well under 1 MB a day, and 0.1 GB a day stays inside the
// monthly free allowance.
resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-portal-${envName}-uptime'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      dailyQuotaGb: json('0.1')
    }
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-portal-${envName}-uptime'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
    IngestionMode: 'LogAnalytics'
    RetentionInDays: 30
  }
}

// A web test must carry the hidden-link tag to its Application Insights resource, or the portal cannot show it.
resource webTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: webTestName
  location: location
  kind: 'standard'
  tags: union(tags, {
    'hidden-link:${appInsights.id}': 'Resource'
  })
  properties: {
    SyntheticMonitorId: webTestName
    Name: 'Portal health (${envName})'
    Description: 'GET ${healthUrl} from ${length(testLocations)} regions; 200 expected; TLS certificate valid with at least ${certificateWarningDays} days left.'
    Enabled: true
    Frequency: frequencySeconds
    Timeout: 30
    Kind: 'standard'
    RetryEnabled: true
    Locations: [for id in testLocations: {
      Id: id
    }]
    Request: {
      RequestUrl: healthUrl
      HttpVerb: 'GET'
      ParseDependentRequests: false
      FollowRedirects: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      IgnoreHttpStatusCode: false
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: certificateWarningDays
    }
  }
}

resource alertGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-portal-${envName}-uptime'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'portaldown'
    enabled: true
    emailReceivers: [
      {
        name: 'portal-ops'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// The window matches the run interval, so each evaluation sees one run from every region.
resource siteDownAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-portal-${envName}-site-down'
  location: 'global'
  tags: tags
  properties: {
    description: 'The portal failed its health check from ${failedLocationThreshold} or more of ${length(testLocations)} outside regions (down, unreachable, or a TLS certificate near expiry).'
    severity: 1
    enabled: true
    scopes: [
      webTest.id
      appInsights.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: frequencySeconds == 300 ? 'PT5M' : 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: webTest.id
      componentId: appInsights.id
      failedLocationCount: failedLocationThreshold
    }
    actions: [
      {
        actionGroupId: alertGroup.id
      }
    ]
  }
}

output webTestName string = webTest.name
output appInsightsName string = appInsights.name
