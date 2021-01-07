rgName=$1
aksName=$2
namePref=$3

#
# Helper functions
#
echoerr() 
{
    printf "\033[0;31m%s\n\033[0m" "$*" >&2;
}

printusage()
{
    echo "setup_podid.sh resourceGroupName aksClusterName identityNamePrefix"
}


#
# Check the parameters
#
if [ -z "$rgName" ]; then
    echoerr "The first parameter must contain a resource group name!"
    printusage
    exit 1
fi

if [ -z "$aksName" ]; then
    echoerr "The second parameter must contain the name of the AKS cluster!"
    printusage
    exit 1
fi

if [ -z "$namePref" ]; then
    echoerr "The third parameter needs to contain the name-prefix for the managed identities to be created!"
    printusage
    exit 1
fi


#
# Get the resource group ID and import the AKS cluster config
#
printf "\r\nGetting Resource Group and AKS Cluster...\r\n"
printf "=========================================\r\n"

rgId=$(az group show --name "$rgName" --query "id" --output tsv)
if [ -z "$rgId" ]; then
    echoerr "Resource group with name $rgName not found!"
    exit 1
fi

# Import the AKS cluster config
az aks get-credentials --name "$aksName" --resource-group "$rgName"
if [ $? -ne 0 ]; then
    echoerr "AKS cluster not found, cannot merge credentials!"
    exit 1
fi


#
# Enable Pod Identity on the AKS cluster
#
printf "\r\nInstalling AAD Pod Identity...\r\n"
printf "==============================\r\n"

helm repo add aad-pod-identity https://raw.githubusercontent.com/Azure/aad-pod-identity/master/charts
helm install aad-pod-identity aad-pod-identity/aad-pod-identity --namespace kube-system


#
# Now, create an identity to used by the less privileged APIs in the sample 
#
printf "\r\nCreating managed identities...\r\n"
printf "==============================\r\n"

regularIdentityName="${namePref}RegularId"
az identity create --name "$regularIdentityName" --resource-group "$rgName" --out json

privilegedIdentityName="${namePref}PrivilegedId"
az identity create --name "$privilegedIdentityName" --resource-group "$rgName" --out json

# It can take a while for identities to replicate
sleep 20


#
# Assigning permissions to the regular identity
#
printf "\r\nAssigning permissions to the regular identity...\r\n"
printf "================================================\r\n"

regularIdentityClientId="$(az identity show --name "$regularIdentityName" --resource-group "$rgName" --query "clientId" -otsv)"
regularIdentityResourceId="$(az identity show --name "$regularIdentityName" --resource-group "$rgName" --query "id" -otsv)"
# This identity will have read access to the resource group so it can list but not create/modify resources in it
az role assignment create --role Reader --assignee $regularIdentityClientId --scope "$rgId" --out jsonc


#
# Assigning permissions to the regular identity
#
printf "\r\nAssigning permissions to the privileged identity...\r\n"
printf "===================================================\r\n"

privilegedIdentityClientId="$(az identity show --name "$privilegedIdentityName" --resource-group "$rgName" --query "clientId" -otsv)"
privilegedIdentityResourceId="$(az identity show --name "$privilegedIdentityName" --resource-group "$rgName" --query "id" -otsv)"
# This identity will have contributor rights to the resource group so that it can create and read resources in the resource group
az role assignment create --role Contributor --assignee $privilegedIdentityClientId --scope "$rgId" --out jsonc


#
# The AKS Service Principal needs to have permissions on the created user assigned managed identities to enable PodIdentity in the cluster
#
printf "\r\nAssigning permissions to the AKS SP to use the identities...\r\n"
printf "============================================================\r\n"

aksServicePrincipalId=$(az aks show --name $aksName --resource-group $rgName --query "servicePrincipalProfile.clientId" --out tsv)
az role assignment create --role "Managed Identity Operator" --assignee $aksServicePrincipalId --scope "$rgId" --out jsonc


#
# Now, create a privileged identity which is allowed to provision resources in the resource group 
#
printf "\r\nCreating Pod-Identities in AKS...\r\n"
printf "=================================\r\n"

# Deploy the regular identity into the k8s cluster
cat azIdentity.yaml |
    awk -v n=$regularIdentityName '{sub(/IDENTITY_NAME/,tolower(n))}1' |
    awk -v n=$regularIdentityClientId '{sub(/IDENTITY_CLIENT_ID/,tolower(n))}1' |
    awk -v n=$regularIdentityResourceId '{sub(/IDENTITY_RESOURCE_ID/,tolower(n))}1' |
    kubectl apply -f -

# Next, deploy the privileged identity
cat azIdentity.yaml |
    awk -v n=$privilegedIdentityName '{sub(/IDENTITY_NAME/,tolower(n))}1' |
    awk -v n=$privilegedIdentityClientId '{sub(/IDENTITY_CLIENT_ID/,tolower(n))}1' |
    awk -v n=$privilegedIdentityResourceId '{sub(/IDENTITY_RESOURCE_ID/,tolower(n))}1' |
    kubectl apply -f -

# Create the identity binding that binds the identity to a selector used in pod-labels for the regular identity
# The selector matches the label used in the pod/deployment manifests, hence changing the identity name does not impact those manifests
cat azIdentityBinding.yaml |
    awk -v n=$regularIdentityName '{sub(/IDENTITY_NAME/,tolower(n))}1' |
    awk '{sub(/IDTYPE/,"regular")}1' |
    kubectl apply -f -

# Create the identity binding that binds the identity to a selector used in pod-labels for the privileged identity
# The selector matches the label used in the pod/deployment manifests, hence changing the identity name does not impact those manifests
cat azIdentityBinding.yaml |
    awk -v n=$privilegedIdentityName '{sub(/IDENTITY_NAME/,tolower(n))}1' |
    awk '{sub(/IDTYPE/,"privileged")}1' |
    kubectl apply -f -