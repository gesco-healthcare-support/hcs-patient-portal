// The infra deploy identity's rights on the workload resource group, and (when the preview identity is scoped to
// the group instead of the subscription) the preview identity's.

@description('Short environment name, used in assignment names.')
param envName string

@description('Principal id of the infra deploy identity.')
param infraPrincipalId string

@description('Principal ids of ALL THREE CI identities. The infra identity may not assign any role to any of them, itself included.')
param ciPrincipalIds array

@description('Role definition ids (GUIDs) the infra identity may assign. From assignable-roles.json.')
param assignableRoleIds array

@description('Resource id of the Portal Lock Writer custom role.')
param lockWriterRoleId string

@description('Principal id of the preview identity, only when its rights are scoped to this group. Empty otherwise.')
param whatIfPrincipalId string = ''

@description('Resource id of the Portal Infra What-If custom role.')
param whatIfRoleId string

var roleReader = 'acdd72a7-3385-48ef-bd42-f606fba81ae7'
var roleContributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var roleAssignmentAdministrator = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'

var roleSet = join(assignableRoleIds, ', ')
var ciPrincipalSet = join(ciPrincipalIds, ', ')

// Microsoft's "Constrain roles and principal types" condition, plus one clause of our own on writes: the principal
// must not be a CI identity. Without it the infra identity could grant itself, or the preview identity, any role in
// the set, for example Secrets Officer on a vault, which Contributor alone does not give.
//
// WHAT THIS DOES NOT DO: Contributor includes virtualMachines/runCommands/write, and a run command executes as the
// VM, with the VM identity's data rights. Inside this group the infra identity can reach what the VM can reach. The
// condition stops escalation beyond the group and to roles outside the set; the environment's branch policy and
// required reviewer are the control on the identity itself.
var writeClause = '@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${roleSet}} AND @Request[Microsoft.Authorization/roleAssignments:PrincipalType] ForAnyOfAnyValues:StringEqualsIgnoreCase {\'ServicePrincipal\'} AND @Request[Microsoft.Authorization/roleAssignments:PrincipalId] ForAnyOfAllValues:GuidNotEquals {${ciPrincipalSet}}'
var deleteClause = '@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${roleSet}} AND @Resource[Microsoft.Authorization/roleAssignments:PrincipalType] ForAnyOfAnyValues:StringEqualsIgnoreCase {\'ServicePrincipal\'}'
var assignmentCondition = '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (${writeClause})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (${deleteClause}))'

resource infraContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'gh-infra-contributor', envName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleContributor)
    principalId: infraPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI infrastructure deploy.'
  }
}

resource infraAssignsRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'gh-infra-assignment-administrator', envName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleAssignmentAdministrator)
    principalId: infraPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI infrastructure deploy. Constrained to assignable-roles.json, service principals, and never a CI identity.'
    conditionVersion: '2.0'
    condition: assignmentCondition
  }
}

resource infraWritesLocks 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, 'gh-infra-lock-writer', envName)
  properties: {
    roleDefinitionId: lockWriterRoleId
    principalId: infraPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI infrastructure deploy. Creates and updates locks; cannot delete them.'
  }
}

resource whatIfReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(whatIfPrincipalId)) {
  name: guid(resourceGroup().id, 'gh-whatif-reader', envName)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleReader)
    principalId: whatIfPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI pull-request preview. Read only.'
  }
}

resource whatIfActions 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(whatIfPrincipalId)) {
  name: guid(resourceGroup().id, 'gh-whatif-actions', envName)
  properties: {
    roleDefinitionId: whatIfRoleId
    principalId: whatIfPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI pull-request preview. what-if and validate only.'
  }
}

output assignmentCondition string = assignmentCondition
