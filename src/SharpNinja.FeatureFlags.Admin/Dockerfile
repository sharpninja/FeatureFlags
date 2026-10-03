# Use Docker's bundled Dockerfile frontend.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY NuGet.config ./
COPY src/SharpNinja.FeatureFlags.Abstractions/SharpNinja.FeatureFlags.Abstractions.csproj src/SharpNinja.FeatureFlags.Abstractions/
COPY src/SharpNinja.FeatureFlags.Admin.IdentityServer/SharpNinja.FeatureFlags.Admin.IdentityServer.csproj src/SharpNinja.FeatureFlags.Admin.IdentityServer/
COPY src/SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres/SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres.csproj src/SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres/
COPY src/SharpNinja.FeatureFlags.Admin/SharpNinja.FeatureFlags.Admin.csproj src/SharpNinja.FeatureFlags.Admin/
COPY src/SharpNinja.FeatureFlags.Admin.Data/SharpNinja.FeatureFlags.Admin.Data.csproj src/SharpNinja.FeatureFlags.Admin.Data/
COPY src/SharpNinja.FeatureFlags.Admin.Data.Postgres/SharpNinja.FeatureFlags.Admin.Data.Postgres.csproj src/SharpNinja.FeatureFlags.Admin.Data.Postgres/
COPY src/SharpNinja.FeatureFlags.Admin.Host/SharpNinja.FeatureFlags.Admin.Host.csproj src/SharpNinja.FeatureFlags.Admin.Host/
RUN dotnet restore src/SharpNinja.FeatureFlags.Admin.Host/SharpNinja.FeatureFlags.Admin.Host.csproj -p:TargetFramework=net10.0 -p:TargetFrameworks=net10.0

COPY src/SharpNinja.FeatureFlags.Abstractions/ src/SharpNinja.FeatureFlags.Abstractions/
COPY src/SharpNinja.FeatureFlags.Admin.IdentityServer/ src/SharpNinja.FeatureFlags.Admin.IdentityServer/
COPY src/SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres/ src/SharpNinja.FeatureFlags.Admin.IdentityServer.Postgres/
COPY src/SharpNinja.FeatureFlags.Admin/ src/SharpNinja.FeatureFlags.Admin/
COPY src/SharpNinja.FeatureFlags.Admin.Data/ src/SharpNinja.FeatureFlags.Admin.Data/
COPY src/SharpNinja.FeatureFlags.Admin.Data.Postgres/ src/SharpNinja.FeatureFlags.Admin.Data.Postgres/
COPY src/SharpNinja.FeatureFlags.Admin.Host/ src/SharpNinja.FeatureFlags.Admin.Host/
RUN find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
RUN dotnet publish src/SharpNinja.FeatureFlags.Admin.Host/SharpNinja.FeatureFlags.Admin.Host.csproj --configuration Release --framework net10.0 --output /app/publish /p:TargetFrameworks=net10.0 /p:UseAppHost=false /p:DisableGitVersionTask=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:1fa23fc4872d95fd71c2833ebe65d7e84a43b2d51a31d119516852f13d9505a7 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SharpNinja.FeatureFlags.Admin.Host.dll"]
