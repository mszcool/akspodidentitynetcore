Param (
    [String]
    [Parameter(Mandatory=$true)]
    $devCertPwd,

    [Switch]
    $updateSecrets
)

#
# Generates certificates for local testing with docker compose and containers.
# and includes the "service names" as alternative DNS names supported by the certificate which
# was the main reason for following this approach.
#
# Required hints for making this happen:
# - https://stackoverflow.com/questions/27745161/openssl-self-signed-root-ca-certificate-set-a-start-date
# - https://fearofoblivion.com/setting-up-asp-net-dev-certs-for-wsl
# Both hints combined where needed, the 2nd article alone above did not help.
#
# Note: after generating new certs, you need to wait until the cert becomes valid as the container runs in UTC (not your time zone, eventually).
#
Remove-Item -Force akspodiddevcertwithservicenames.*

openssl req -x509 -nodes -days 365 -newkey rsa:2048 -subj "/CN=localhost" -config localhost.conf -keyout akspodiddevcertwithservicenames.key -out akspodiddevcertwithservicenames.crt
openssl pkcs12 -export -in .\akspodiddevcertwithservicenames.crt -inkey .\akspodiddevcertwithservicenames.key -out .\akspodiddevcertwithservicenames.pfx -passout pass:$devCertPwd

#
# Convert to base64 for adding to a k8s secret and generate test configmaps and secrets manifests for a deployment
#
$pfxContent = Get-Content -AsByteStream -Path .\akspodiddevcertwithservicenames.pfx
$pfxBase64 = [System.Convert]::ToBase64String($pfxContent)
$certPwdBase64 = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($devCertPwd))
$crtContent = Get-Content -AsByteStream -Path .\akspodiddevcertwithservicenames.crt
$crtBase64 = [System.Convert]::ToBase64String($crtContent)

$secretsContent = Get-Content -Path ..\..\infra\app\resources_configsecrets.yaml
$secretsTestContent = $secretsContent.Replace("BASE64CERTPWD", $certPwdBase64)
$secretsTestContent = $secretsTestContent.Replace("BASE64CERTPFX", $pfxBase64)
$secretsTestContent = $secretsTestContent.Replace("BASE64CERTCRT", $crtBase64)
Out-File -InputObject $secretsTestContent -FilePath ..\..\infra\app\resources_configsecrets.test.yaml

# Note: for this to work, the extension
#       1.3.6.1.4.1.311.84.1.1 = ASN1:INTEGER:02 needs to be part of the certificate generated with openssl.
dotnet dev-certs https --clean --import .\akspodiddevcertwithservicenames.pfx -p $devCertPwd
dotnet dev-certs https --trust

#
# Update user secrets
#
if ( $updateSecrets ) {

    Copy-Item -Force ./akspodiddevcertwithservicenames.pfx ~/AppData/Roaming/ASP.NET/Https/MszCool.Samples.PodIdentityDemo.ResourcesFrontend.pfx
    Copy-Item -Force ./akspodiddevcertwithservicenames.pfx ~/AppData/Roaming/ASP.NET/Https/MszCool.Samples.PodIdentityDemo.ResourcesBackend.pfx

    Set-Location ..\ResourcesBackend
    dotnet user-secrets set Kestrel:Certificates:Default:Password $devCertPwd
    dotnet user-secrets set Kestrel:Certificates:Development:Password $devCertPwd

    Set-Location ..\ResourcesFrontend
    dotnet user-secrets set Kestrel:Certificates:Default:Password $devCertPwd
    dotnet user-secrets set Kestrel:Certificates:Development:Password $devCertPwd

    Set-Location ..\devcerts

}

#
# Note: generating the certs with dotnet dev-certs https, exporting, converting to crt and using
#       in a Linux container did not work due to missing attributes / extensions in the generated certificate.
#       Hence, this way above was easier to get working than the one below.
#
# Generate dev-certs for local debugging without compose
#
#dotnet dev-certs https --clean
#dotnet dev-certs https -ep ./akspodiddevcertwithservicenames.pfx -p $devCertPwd

# Convert the PFX so it works on Linux containers (Debian 10)
#openssl pkcs12 -in akspodiddevcertwithservicenames.pfx -clcerts -nokeys -out akspodiddevcertwithservicenames.crt -passin pass:$devCertPwd
#openssl pkcs12 -in akspodiddevcertwithservicenames.pfx -nocerts -out akspodiddevcertwithservicenames.key -passin pass:$devCertPwd -passout pass:$devCertPwd