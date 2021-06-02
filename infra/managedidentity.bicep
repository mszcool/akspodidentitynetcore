@minLength(5)
@maxLength(20)
param identityName string
@allowed([
  'Regular'
  'Privileged'
])
param identityType string
param location string = resourceGroup().location

var ownerRoleId = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var roleDefinitionId = (identityType == 'Regular' ? storageBlobDataOwnerRoleId : ownerRoleId)

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: identityName
  location: location
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2020-10-01-preview' = {
  name: guid('${identityName}-${roleDefinitionId}${location}assignment')
  dependsOn: [
    managedIdentity
  ]
  scope: resourceGroup()
  properties: {
    principalId: managedIdentity.id
    principalType: 'MSI'
    roleDefinitionId: roleDefinitionId
  }
}

output identityDetails object = {
  clientId: managedIdentity.properties.clientId
  principalId: managedIdentity.properties.principalId
  tenantId: managedIdentity.properties.tenantId
}
