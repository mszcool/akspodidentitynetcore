FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 5243

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["src/ResourcesBackend/ResourcesBackend.csproj", "src/ResourcesBackend/"]
RUN dotnet restore "src/ResourcesBackend/ResourcesBackend.csproj"
COPY . .
WORKDIR "/src/src/ResourcesBackend"
RUN dotnet build "ResourcesBackend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ResourcesBackend.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MszCool.Samples.PodIdentityDemo.ResourcesBackend.dll"]
