FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 5143
ENV ASPNETCORE_URLS=https://+:5143
# For testing with docker-compose, trust the self-signed developer certificate
# TODO: refactor this to copy the file through a script running at the entry point so it
#       can be taken from a k8s configmap / secret in production.
ADD ["src/devcerts/akspodiddevcertwithservicenames.crt", "/etc/ssl/certs/akspodiddevcertwithservicenames.crt"]

#
# Build stage
#
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
# Now build and release the app itself
COPY ["src/ResourcesFrontend/ResourcesFrontend.csproj", "src/ResourcesFrontend/"]
RUN dotnet restore "src/ResourcesFrontend/ResourcesFrontend.csproj"
COPY . .
WORKDIR "/src/src/ResourcesFrontend"
RUN dotnet build "ResourcesFrontend.csproj" -c Release -o /app/build

#
# Publishing stage
#
FROM build AS publish
RUN dotnet publish "ResourcesFrontend.csproj" -c Release -o /app/publish

#
# Release stage
#
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MszCool.Samples.PodIdentityDemo.ResourcesFrontend.dll"]