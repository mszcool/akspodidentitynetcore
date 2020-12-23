Param(
    [String]
    [ValidateSet("FrontendConfig", "BackendConfig")]
    $SetForConfig = "FrontendConfig"
)
#
# Use this script to set the user secrets.
# I suggest to change in-line, run the script and then undo changes.
# - this way you don't need to clear your Powershell command history which would keep the secrets.
# - undo avoids to check-in the secrets into the repo, of course
#

$project = [System.IO.Path]::Combine($PWD.Path, "ResourcesFrontend")
if($SetForConfig -eq "BackendConfig") {
    $project = [System.IO.Path]::Combine($PWD.Path, "ResourcesBackend")
}

$currentLocation = $PWD.Path

Set-Location $project
dotnet user-secrets init
dotnet user-secrets set "${SetForConfig}:ResourcesConfig:SubscriptionId" "SUBSCRIPTION_ID" --project "$project"
dotnet user-secrets set "${SetForConfig}:ResourcesConfig:ResourceGroupName" "RESOURCE_GROUP_NAME"  --project "$project"
dotnet user-secrets set "${SetForConfig}:SecurityConfig:TenantId" "AAD_TENANT_ID"  --project "$project"
dotnet user-secrets set "${SetForConfig}:SecurityConfig:ClientId" "AAD_SERVICE_PRINCIPAL_CLIENT_ID"  --project "$project"
dotnet user-secrets set "${SetForConfig}:SecurityConfig:ClientSecret" "AAD_SERVICE_PRINCIPAL_SECRET"  --project "$project"

Set-Location $currentLocation