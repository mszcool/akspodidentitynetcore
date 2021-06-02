param namePrefix string = 'akspodid${uniqueString(resourceGroup().id)}'
param location string = resourceGroup().location

module privilegedIdentityModule 'managedidentity.bicep' = {
  name: '${namePrefix}PrivilegedIdentityDeployment'
  params: {
    identityName: '${namePrefix}privilegedidentity'
    identityType: 'Privileged'
    location: location
  }
}

module regularIdentityModule 'managedidentity.bicep' = {
  name: '${namePrefix}regularidentitydeployment'
  params: {
    identityName: '${namePrefix}RegularIdentity'
    identityType: 'Regular'
    location: location
  }
}

resource aksInfraVnet 'Microsoft.Network/virtualNetworks@2021-02-01' = {
  name: '${namePrefix}akspodidvnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.10.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'aksSubnet'
        properties: {
          addressPrefix: '10.10.1.0/24'
        }
      }
      {
        name: 'storageSubnet'
        properties: {
          addressPrefix: '10.10.2.0/24'
        }
      }
    ]
  }
}

resource waitForRolesInAad 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: '${namePrefix}waitforroleprovisioningsleep'
  location: location
  kind: 'AzureCLI'
  dependsOn: [
    regularIdentityModule
    privilegedIdentityModule
  ]
  properties: {
    azCliVersion: '2.24.1'
    timeout: 'PT10M'
    retentionInterval: 'PT1D'
    scriptContent: 'sleep 30s'
  }
}

resource aksCluster 'Microsoft.ContainerService/managedClusters@2021-03-01' = {
  name: '${namePrefix}akscluster'
  location: location
  dependsOn: [
    aksInfraVnet
    waitForRolesInAad
  ]
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    agentPoolProfiles: [
      {
        name: 'primaryPool'
        count: 3
        vmSize: ''
      }
    ]
  }
}
