// Phase 2 of the bootstrap: the app deploy identity's rights. Deployed only once the infrastructure deploy's phase 1
// has created the registry and the VM. Kept out of the resource-group template on purpose, so that no CI identity is
// ever granted anything by another CI identity.

@description('Principal id of the app deploy identity.')
param appPrincipalId string

@description('Name of the container registry in this resource group.')
param registryName string

@description('Name of the application VM in this resource group.')
param vmName string

var roleAcrPush = '8311e382-0749-4cb8-b61a-304f252e45ec'
var roleVirtualMachineContributor = '9980e02c-c2be-4d73-94e8-173b1dc7cf3c'

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' existing = {
  name: registryName
}

resource vm 'Microsoft.Compute/virtualMachines@2024-07-01' existing = {
  name: vmName
}

// AcrPush is honoured only while the registry stays in LegacyRegistryPermissions mode, which platform.bicep pins.
resource appPushesImages 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, 'gh-app-acr-push')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleAcrPush)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI app deploy. Pushes images.'
  }
}

// Managed Run Command needs Microsoft.Compute/virtualMachines/runCommands/write, which this role holds. Scoped to
// the one VM, not the group.
resource appRunsDeployOnHost 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vm
  name: guid(vm.id, 'gh-app-vm-contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleVirtualMachineContributor)
    principalId: appPrincipalId
    principalType: 'ServicePrincipal'
    description: 'CI app deploy. Runs the deploy script on the host.'
  }
}
