// Patient Portal - subscription bootstrap: the resource groups, the three CI identities and their rights.
//
// RUN BY A HUMAN WITH OWNER ON THE SUBSCRIPTION. NEVER BY CI. This template grants CI its rights, so a CI
// identity able to run it could widen its own. One invariant follows, and the whole design leans on it:
// a CI identity's own rights are only ever changed by a subscription Owner, through this file.
//
//   az deployment sub what-if -l westus2 -f infra/azure/subscription/bootstrap.bicep -p @bootstrap.parameters.json
//   az deployment sub create  -l westus2 -f infra/azure/subscription/bootstrap.bicep -p @bootstrap.parameters.json \
//     -n portal-bootstrap
//
// TWO RUNS. Phase 1 before the first infrastructure deploy: everything except the app identity's grants, whose
// targets (the registry and the VM) do not exist yet. Phase 2 after the infrastructure deploy's phase 1: the same
// command with appDeployTargets filled in. A re-run with unchanged parameters changes nothing.
//
// THREE IDENTITIES, NOT ONE. A federated credential authenticates AS its identity, and role assignments attach to
// the identity, so one identity with three credentials would hand the pull-request preview the deploy's rights.
//
//   id-gh-whatif-<env>  pull requests            Reader + what-if/validate. No write.
//   id-gh-infra-<env>   environment ...-infra    Contributor, a constrained RBAC administrator, lock writer.
//   id-gh-app-<env>     environment ...-app      AcrPush on the registry, Virtual Machine Contributor on the VM.
//
// They are user-assigned managed identities rather than app registrations: no Entra directory role is needed to
// create them, no client secret can exist, and they are declared here instead of clicked together.
//
// They live in their own resource group. In the workload group, the infra identity's Contributor would let it
// rewrite its own federated credential, or the what-if identity's.
//
// FEDERATED CREDENTIALS BELONG IN THE IDENTITY GROUP ONLY, where only this bootstrap manages them. A deny policy,
// assigned below at the workload group, keeps it that way even though Contributor there includes
// Microsoft.ManagedIdentity writes. Contributor cannot remove a policy assignment.

targetScope = 'subscription'

// ---------------------------------------------------------------- parameters

@description('Region for the two resource groups and the identities.')
param location string = 'westus2'

@description('Short environment name, the SAME value the infrastructure deploy uses. 7 characters at most: the Key Vault name the resource-group template derives from it is capped at 24.')
@minLength(2)
@maxLength(7)
param envName string = 'pilot'

@description('Prefix of every GitHub OIDC subject. The default is the IMMUTABLE form (owner and repository ids), which the repository must be opted into BEFORE this runs; the legacy form is repo:gesco-healthcare-support/hcs-patient-portal. Record the subject GitHub actually presents on the first run.')
param githubSubjectPrefix string = 'repo:gesco-healthcare-support@274625791/hcs-patient-portal@1205316583'

@description('Where the what-if identity\'s Reader and what-if rights apply. resourceGroup by default, because that is everything CI previews: infra.yml runs only `az deployment group what-if` against main.bicep (targetScope resourceGroup), and its parameter read-back and write probe are group-scoped too. subscription grants the same rights across the whole subscription; choose it only together with a job that previews a subscription-scope template, and only on a subscription dedicated to the portal.')
@allowed([
  'subscription'
  'resourceGroup'
])
param whatIfScope string = 'resourceGroup'

@description('Phase 2 only: names, in the workload resource group, of the registry and the VM the app deploy identity acts on. Leave both empty in phase 1.')
param appDeployTargets object = {
  registryName: ''
  vmName: ''
}

@description('Tags applied to both resource groups and the identities.')
param tags object = {
  application: 'patient-portal'
  environment: envName
  managedBy: 'bicep-bootstrap'
}

// ---------------------------------------------------------------- names

var workloadGroupName = 'rg-portal-${envName}'
var identityGroupName = 'rg-portal-${envName}-identity'

// Exact names, case included: the federated subjects embed them, and the workflow names them.
var infraEnvironment = 'azure-production-infra'
var appEnvironment = 'azure-production-app'

// Built-in roles. Reader, then the custom what-if role, for the preview identity.
var roleReader = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'

// ---------------------------------------------------------------- resource groups

resource workloadGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: workloadGroupName
  location: location
  tags: tags
}

resource identityGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: identityGroupName
  location: location
  tags: tags
}

// ---------------------------------------------------------------- custom roles
//
// Role names are unique per TENANT, not per subscription, so they carry the environment name: a second
// environment in another subscription of the same tenant would otherwise collide.

resource whatIfRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'portal-infra-what-if', envName)
  properties: {
    roleName: 'Portal Infra What-If (${envName})'
    description: 'Preview and validate template deployments. Paired with Reader. Grants no write on any resource.'
    type: 'CustomRole'
    permissions: [
      {
        actions: [
          'Microsoft.Resources/deployments/whatIf/action'
          'Microsoft.Resources/deployments/validate/action'
        ]
      }
    ]
    assignableScopes: [
      subscription().id
    ]
  }
}

// Contributor excludes Microsoft.Authorization/*/Write, and Role Based Access Control Administrator grants only
// role assignments, so neither can create a management lock. Write and NO delete: CI re-asserts every lock on every
// deploy and cannot remove one. Lifting a lock is a subscription Owner's action.
resource lockWriterRole 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(subscription().id, 'portal-lock-writer', envName)
  properties: {
    roleName: 'Portal Lock Writer (${envName})'
    description: 'Create and update management locks. Cannot delete them.'
    type: 'CustomRole'
    permissions: [
      {
        actions: [
          'Microsoft.Authorization/locks/read'
          'Microsoft.Authorization/locks/write'
        ]
      }
    ]
    assignableScopes: [
      subscription().id
    ]
  }
}

// ---------------------------------------------------------------- no federated credentials in the workload group
//
// Microsoft's documented control for federated identity credentials ("Important considerations and restrictions for
// federated identity credentials", section "Azure policy"). The DEFINITION lives at subscription scope; it is
// ASSIGNED at the workload group only, by modules/workload-policy.bicep, because the CI identities' own credentials
// are created in the identity group and a subscription-wide assignment would refuse this very deployment.

resource denyFederatedCredentialsPolicy 'Microsoft.Authorization/policyDefinitions@2023-04-01' = {
  name: guid(subscription().id, 'portal-deny-federated-credentials', envName)
  properties: {
    displayName: 'Portal: deny federated identity credentials (${envName})'
    description: 'Refuses the creation or update of any federated identity credential where assigned.'
    policyType: 'Custom'
    mode: 'All'
    policyRule: {
      if: {
        field: 'type'
        equals: 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials'
      }
      then: {
        effect: 'deny'
      }
    }
  }
}

// ---------------------------------------------------------------- the preview identity, at subscription scope

resource whatIfReaderOnSubscription 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (whatIfScope == 'subscription') {
  name: guid(subscription().id, 'gh-whatif-reader', envName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleReader)
    principalId: identities.outputs.whatIfPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI pull-request preview. Read only.'
  }
}

resource whatIfActionsOnSubscription 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (whatIfScope == 'subscription') {
  name: guid(subscription().id, 'gh-whatif-actions', envName)
  properties: {
    roleDefinitionId: whatIfRole.id
    principalId: identities.outputs.whatIfPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI pull-request preview. what-if and validate only.'
  }
}

// ---------------------------------------------------------------- identities

module identities 'modules/identities.bicep' = {
  name: 'portal-bootstrap-identities'
  scope: identityGroup
  params: {
    location: location
    envName: envName
    pullRequestSubject: '${githubSubjectPrefix}:pull_request'
    infraSubject: '${githubSubjectPrefix}:environment:${infraEnvironment}'
    appSubject: '${githubSubjectPrefix}:environment:${appEnvironment}'
    tags: tags
  }
}

// ---------------------------------------------------------------- the workload group

module workloadAccess 'modules/workload-access.bicep' = {
  name: 'portal-bootstrap-workload-access'
  scope: workloadGroup
  params: {
    envName: envName
    infraPrincipalId: identities.outputs.infraPrincipalId
    ciPrincipalIds: [
      identities.outputs.whatIfPrincipalId
      identities.outputs.infraPrincipalId
      identities.outputs.appPrincipalId
    ]
    assignableRoleIds: objectKeys(loadJsonContent('assignable-roles.json').roles)
    lockWriterRoleId: lockWriterRole.id
    whatIfPrincipalId: whatIfScope == 'resourceGroup' ? identities.outputs.whatIfPrincipalId : ''
    whatIfRoleId: whatIfRole.id
  }
}

module appAccess 'modules/app-access.bicep' = if (!empty(appDeployTargets.registryName) && !empty(appDeployTargets.vmName)) {
  name: 'portal-bootstrap-app-access'
  scope: workloadGroup
  params: {
    appPrincipalId: identities.outputs.appPrincipalId
    registryName: appDeployTargets.registryName
    vmName: appDeployTargets.vmName
  }
}

module workloadPolicy 'modules/workload-policy.bicep' = {
  name: 'portal-bootstrap-workload-policy'
  scope: workloadGroup
  params: {
    envName: envName
    policyDefinitionId: denyFederatedCredentialsPolicy.id
  }
}

// ---------------------------------------------------------------- outputs
//
// Copied by hand into GitHub: the what-if client id as a REPOSITORY secret (the preview job names no environment),
// the other two as secrets of their environments. None is a credential; they are secrets so public logs mask them.

output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
output workloadResourceGroup string = workloadGroup.name
output whatIfClientId string = identities.outputs.whatIfClientId
output infraClientId string = identities.outputs.infraClientId
output appClientId string = identities.outputs.appClientId
output federatedSubjects array = identities.outputs.subjects
