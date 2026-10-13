// OPTIONAL: Azure Relay for the Case Tracker's document reads (docs/integration/case-tracker-relay.md).
//
// The Case Tracker has no fixed address, so the MinIO vhost cannot be allow-listed to it. Instead the VM runs a
// LISTENER (scripts/hosting/relay-listener.sh) and the Case Tracker a SENDER, both connecting OUT to this relay over
// 443; nothing on either side accepts inbound connections, and the MinIO vhost stays closed to the internet.
//
// Two independent credentials, each able to do one thing:
//   - the VM listens as its MANAGED IDENTITY, granted "Azure Relay Listener" on this hybrid connection only: no key
//     on the VM. The listen-only SAS rule exists as a fallback and its keys are not handed out unless needed;
//   - the Case Tracker, outside Azure, sends with the SEND-ONLY SAS rule. Its key cannot listen, so a leaked copy
//     cannot impersonate the portal.
// No key is ever an output of this template: an Owner reads them with `az relay hyco authorization-rule keys list`.

@description('Azure region.')
param location string

@description('Relay namespace name: globally unique, 6-50 characters, letters, digits and hyphens, starting with a letter.')
@minLength(6)
@maxLength(50)
param namespaceName string

@description('Hybrid connection the Case Tracker uses to reach MinIO through nginx.')
param hybridConnectionName string = 'case-tracker-documents'

@description('Principal id of the VM\'s system-assigned identity: it is granted Azure Relay Listener on the hybrid connection.')
param vmPrincipalId string

@description('Address emailed when the hybrid connection has had no listener for 15 minutes. Empty = no alert.')
param alertEmail string

@description('Tags applied to every resource.')
param tags object

// Built-in role "Azure Relay Listener" (stable across tenants).
var relayListenerRoleId = '26e0b698-aa6d-4085-9386-aadae190014d'

resource namespace 'Microsoft.Relay/namespaces@2021-11-01' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  tags: tags
  properties: {}
}

resource hybridConnection 'Microsoft.Relay/namespaces/hybridConnections@2021-11-01' = {
  parent: namespace
  name: hybridConnectionName
  properties: {
    // Every listener and sender must present a token: an anonymous sender could otherwise reach nginx.
    requiresClientAuthorization: true
    userMetadata: 'Case Tracker -> portal MinIO (through nginx, TLS end to end). See docs/integration/case-tracker-relay.md.'
  }
}

resource listenRule 'Microsoft.Relay/namespaces/hybridConnections/authorizationRules@2021-11-01' = {
  parent: hybridConnection
  name: 'vm-listen'
  properties: {
    rights: [
      'Listen'
    ]
  }
}

resource sendRule 'Microsoft.Relay/namespaces/hybridConnections/authorizationRules@2021-11-01' = {
  parent: hybridConnection
  name: 'case-tracker-send'
  properties: {
    rights: [
      'Send'
    ]
  }
}

resource vmListener 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: hybridConnection
  name: guid(hybridConnection.id, vmPrincipalId, relayListenerRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', relayListenerRoleId)
    principalId: vmPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource alertGroup 'Microsoft.Insights/actionGroups@2023-01-01' = if (!empty(alertEmail)) {
  name: 'ag-portal-relay'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'portalrelay'
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

// ActiveListeners is a per-minute point-in-time count; a TOTAL of zero over 15 minutes means not one minute with a
// listener. It catches every way the listener can be gone: the script, the container, the identity, or the VM.
resource noListenerAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (!empty(alertEmail)) {
  name: 'alert-portal-relay-no-listener'
  location: 'global'
  tags: tags
  properties: {
    description: 'The Case Tracker cannot read portal documents: no Azure Relay listener for 15 minutes.'
    severity: 2
    enabled: true
    scopes: [
      namespace.id
    ]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'no-listener'
          metricNamespace: 'Microsoft.Relay/namespaces'
          metricName: 'ActiveListeners'
          dimensions: [
            {
              name: 'EntityName'
              operator: 'Include'
              values: [
                hybridConnectionName
              ]
            }
          ]
          operator: 'LessThan'
          threshold: 1
          timeAggregation: 'Total'
        }
      ]
    }
    actions: [
      {
        actionGroupId: alertGroup.id
      }
    ]
  }
}

output namespaceName string = namespace.name
output hybridConnectionName string = hybridConnection.name
output listenRuleName string = listenRule.name
output sendRuleName string = sendRule.name
