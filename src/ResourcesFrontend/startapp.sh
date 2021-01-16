#!/bin/bash
#
# First, see if there's a certificate crt file to copy to the SSL certs directory.
#
if [ -f /https/*.crt ]; then
    echo "Found certificate files to copy to ssl trusted certs."
    cp --force /https/*.crt /etc/ssl/certs
    echo "Copy completed!"
fi

#
# Now start the main application
#
echo "Starting the .NET application"
dotnet MszCool.Samples.PodIdentityDemo.ResourcesFrontend.dll
