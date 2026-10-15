// The three CI identities, each with exactly ONE federated credential. Deployed into the identity resource group.

@description('Region for the identities.')
param location string

@description('Short environment name used in the identity names.')
param envName string

@description('OIDC subject for pull-request runs that name no environment.')
param pullRequestSubject string

@description('OIDC subject for jobs that name the infra environment.')
param infraSubject string

@description('OIDC subject for jobs that name the app environment.')
param appSubject string

@description('Tags applied to every identity.')
param tags object = {}

var issuer = 'https://token.actions.githubusercontent.com'
var audiences = [
  'api://AzureADTokenExchange'
]

resource whatIf 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-gh-whatif-${envName}'
  location: location
  tags: tags
}

resource whatIfFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: whatIf
  name: 'github-pull-request'
  properties: {
    issuer: issuer
    subject: pullRequestSubject
    audiences: audiences
  }
}

resource infra 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-gh-infra-${envName}'
  location: location
  tags: tags
}

resource infraFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: infra
  name: 'github-environment-infra'
  properties: {
    issuer: issuer
    subject: infraSubject
    audiences: audiences
  }
}

resource app 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-gh-app-${envName}'
  location: location
  tags: tags
}

resource appFederation 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: app
  name: 'github-environment-app'
  properties: {
    issuer: issuer
    subject: appSubject
    audiences: audiences
  }
}

output whatIfPrincipalId string = whatIf.properties.principalId
output whatIfClientId string = whatIf.properties.clientId
output infraPrincipalId string = infra.properties.principalId
output infraClientId string = infra.properties.clientId
output appPrincipalId string = app.properties.principalId
output appClientId string = app.properties.clientId
output subjects array = [
  whatIfFederation.properties.subject
  infraFederation.properties.subject
  appFederation.properties.subject
]
