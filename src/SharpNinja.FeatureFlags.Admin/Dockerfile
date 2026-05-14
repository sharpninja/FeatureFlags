# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY NuGet.config ./
COPY src/SharpNinja.FeatureFlags.Abstractions/SharpNinja.FeatureFlags.Abstractions.csproj src/SharpNinja.FeatureFlags.Abstractions/
COPY src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj src/SharpNinja.FeatureFlags.Admin/
RUN dotnet restore src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0

COPY src/SharpNinja.FeatureFlags.Abstractions/ src/SharpNinja.FeatureFlags.Abstractions/
COPY src/SharpNinja.FeatureFlags.Admin/ src/SharpNinja.FeatureFlags.Admin/
RUN find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
RUN dotnet publish src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj --configuration Release --framework net10.0 --output /app/publish /p:TargetFrameworks=net10.0 /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharpNinja.FeatureFlags.Admin.dll"]
