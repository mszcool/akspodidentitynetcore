Param(
    [Parmeter(Mandatory=$true)]
    [String]
    $registryName
)

#
# Publish from local machine after VS Code container build
#

az acr login --name $registryName

docker tag mszcool/samples/akspodidentitynetcore/frontend:latest $registryName.azurecr.io/mszcool/samples/akspodidentitynetcore/frontend:latest
docker push $registryName.azurecr.io/mszcool/samples/akspodidentitynetcore/frontend:latest

docker tag mszcool/samples/akspodidentitynetcore/backend:latest $registryName.azurecr.io/mszcool/samples/akspodidentitynetcore/backend:latest
docker push $registryName.azurecr.io/mszcool/samples/akspodidentitynetcore/backend:latest