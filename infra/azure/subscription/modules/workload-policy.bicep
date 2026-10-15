// Assigns the deny-federated-credentials policy to the WORKLOAD resource group, and only there.
//
// WHY. Every federated credential this system uses is one of the three CI identities', in the identity group, and
// only the subscription bootstrap manages them. The infra deploy identity holds Contributor on this group, which
// includes Microsoft.ManagedIdentity writes; this policy keeps federated credentials out of the group regardless.
//
// WHY THIS GROUP ONLY. The three CI identities' own federated credentials live in the IDENTITY group, created by
// modules/identities.bicep. A subscription-wide assignment would refuse the bootstrap's own deployment. The
// workload templates create no federated credentials, so nothing legitimate is refused here.
//
// WHY CONTRIBUTOR CANNOT UNDO IT. Microsoft.Authorization/*/Write and */Delete are in Contributor's NotActions, and
// they cover policy assignments and exemptions. Only a subscription Owner, through this bootstrap, can change it.

@description('Short environment name, used in the assignment name.')
param envName string

@description('Resource id of the deny-federated-credentials policy definition (subscription scope).')
param policyDefinitionId string

resource denyFederatedCredentials 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'deny-federated-credentials-${envName}'
  properties: {
    displayName: 'Portal: no federated identity credentials in the workload group (${envName})'
    description: 'Federated identity credentials are managed only in the identity group, by the subscription bootstrap.'
    policyDefinitionId: policyDefinitionId
    enforcementMode: 'Default'
    nonComplianceMessages: [
      {
        message: 'Federated identity credentials are not allowed in the workload group. CI identities live in the identity group and are managed only by the subscription bootstrap.'
      }
    ]
  }
}

output assignmentId string = denyFederatedCredentials.id
