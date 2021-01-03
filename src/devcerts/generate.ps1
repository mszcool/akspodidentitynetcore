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
# Note: after generating new certs, make sure to wait 1-2 min before testing as the start time might be too early.
#
rm akspodiddevcertwithservicenames.*

openssl req -x509 -nodes -days 365 -newkey rsa:2048 -subj "/CN=localhost" -config localhost.conf -keyout akspodiddevcertwithservicenames.key -out akspodiddevcertwithservicenames.crt
openssl pkcs12 -export -in .\akspodiddevcertwithservicenames.crt -inkey .\akspodiddevcertwithservicenames.key -out .\akspodiddevcertwithservicenames.pfx -passout pass:dev_pwd_1

# Note: for this to work, the extension
#       1.3.6.1.4.1.311.84.1.1 = ASN1:INTEGER:02 needs to be part of the certificate generated with openssl.
dotnet dev-certs https --clean --import .\akspodiddevcertwithservicenames.pfx -p dev_pwd_1

#
# Note: generating the certs with dotnet dev-certs https, exporting, converting to crt and using
#       in a Linux container did not work due to missing attributes / extensions in the generated certificate.
#       Hence, this way above was easier to get working than the one below.
#
# Generate dev-certs for local debugging without compose
#
#dotnet dev-certs https --clean
#dotnet dev-certs https -ep ./akspodiddevcertwithservicenames.pfx -p dev_pwd_1

# Convert the PFX so it works on Linux containers (Debian 10)
#openssl pkcs12 -in akspodiddevcertwithservicenames.pfx -clcerts -nokeys -out akspodiddevcertwithservicenames.crt -passin pass:dev_pwd_1
#openssl pkcs12 -in akspodiddevcertwithservicenames.pfx -nocerts -out akspodiddevcertwithservicenames.key -passin pass:dev_pwd_1 -passout pass:dev_pwd_1