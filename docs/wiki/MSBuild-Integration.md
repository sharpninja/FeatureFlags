# MSBuild Integration

`SharpNinja.FeatureFlags.Build` ships a build-transitive targets file that runs automatically for every project that references the package. The targets validate required properties, embed the manifest and public key as assembly resources, stamp identity metadata into the assembly, and optionally generate a zero-argument `AddSharpNinjaFeatureFlags()` overload.

## How the Targets Are Applied

Because the package uses `buildTransitive`, the targets propagate to all downstream projects in your solution automatically - you do not need to import anything manually. The file is located at:

```
buildTransitive/SharpNinja.FeatureFlags.Build.targets
```

All targets run `BeforeTargets="CoreCompile"` so the manifest is embedded before the compiler sees the generated source.

---

## Required MSBuild Properties

These properties must be set when a manifest file is present. If they are missing, `ValidateSharpNinjaFeatureFlagBuildProperties` emits a build error and compilation stops.

| Property | Required | Description |
|---|---|---|
| `ProductId` | Yes (when manifest exists) | Identifies which product the manifest belongs to. Injected into generated source and assembly metadata. Must match the manifest's `productId` field. |
| `ReleaseId` | Yes (when manifest exists) | Identifies the exact release. Injected into generated source and assembly metadata. Must match the manifest's `releaseId` field. |

```xml
<PropertyGroup>
  <ProductId>truckmate</ProductId>
  <ReleaseId>truckmate-1.2.0-stable-0</ReleaseId>
</PropertyGroup>
```

---

## Optional Properties and Their Defaults

| Property | Default | Description |
|---|---|---|
| `SharpNinjaFeatureFlagsManifest` | `$(MSBuildProjectDirectory)\flags\flags.json` | Path to the feature flag manifest JSON file. The targets skip all embedding and validation when this file does not exist. |
| `SharpNinjaFeatureFlagsManifestResourceName` | `SharpNinja.FeatureFlags.BundledManifest.json` | Logical name of the embedded manifest resource. Used by generated source to locate the resource at runtime. |
| `SharpNinjaFeatureFlagsPublicKey` | `$(MSBuildProjectDirectory)\flags\public-key.ed25519` | Path to the Ed25519 public key file. Embedded alongside the manifest when present. |
| `SharpNinjaFeatureFlagsPublicKeyResourceName` | `SharpNinja.FeatureFlags.ManifestPublicKey.ed25519` | Logical name of the embedded public key resource. |
| `SharpNinjaFeatureFlagsValidateOnBuild` | `true` | When `true`, runs `ValidateSharpNinjaFeatureFlagBuildProperties` and `ValidateSharpNinjaFeatureFlagManifest` before compilation. Set to `false` to disable build-time validation (not recommended for production builds). |
| `SharpNinjaFeatureFlagsRequirePublicKey` | `true` | When `true`, a build error is emitted if the public key file does not exist. Set to `false` for local development without a signing key. |
| `SharpNinjaFeatureFlagsEmitBuildIdentity` | `true` | When `true`, stamps `AssemblyMetadata` attributes for `ProductId`, `ReleaseId`, `ManifestResourceName`, and `PublicKeyResourceName` into the assembly. |
| `SharpNinjaFeatureFlagsGenerateRegistrationSource` | `false` | When `true`, generates a partial class with a zero-argument `AddSharpNinjaFeatureFlags()` extension method. See [Generated Registration Overload](#generated-registration-overload). |
| `SharpNinjaFeatureFlagsGeneratedSource` | `$(IntermediateOutputPath)SharpNinjaFeatureFlagsBuild.g.cs` | Output path for the generated C# file. |
| `SharpNinjaFeatureFlagsCliCommand` | `flagctl` | CLI tool used for manifest validation. Must be on `PATH` or an absolute path. |
| `SharpNinjaFeatureFlagsSchemaVersion` | `1` | Manifest schema version passed to the validation CLI. |
| `SharpNinjaFeatureFlagsEnvironment` | `Development` | Environment name passed to the generated registration overload. |

---

## What the Build Target Does

### 1. Validate properties (`ValidateSharpNinjaFeatureFlagBuildProperties`)

Runs before `CoreCompile` when `SharpNinjaFeatureFlagsValidateOnBuild` is `true` and the manifest file exists.

Emits MSBuild errors for:
- `ProductId` being empty.
- `ReleaseId` being empty.
- The public key file being missing when `SharpNinjaFeatureFlagsRequirePublicKey` is `true`.

### 2. Embed the manifest and public key

When the manifest file exists, it is added to `EmbeddedResource` with the logical name from `SharpNinjaFeatureFlagsManifestResourceName`. When the public key file exists, it is similarly embedded.

Both files are also added to `AdditionalFiles` with metadata that identifies them to any Roslyn analyzers or source generators that inspect additional files.

### 3. Stamp assembly metadata (`SharpNinjaFeatureFlagsEmitBuildIdentity`)

When `SharpNinjaFeatureFlagsEmitBuildIdentity` is `true`, four `AssemblyMetadata` attributes are injected:

| Attribute key | Value |
|---|---|
| `SharpNinja.FeatureFlags.ProductId` | `$(ProductId)` |
| `SharpNinja.FeatureFlags.ReleaseId` | `$(ReleaseId)` |
| `SharpNinja.FeatureFlags.ManifestResourceName` | `$(SharpNinjaFeatureFlagsManifestResourceName)` |
| `SharpNinja.FeatureFlags.PublicKeyResourceName` | `$(SharpNinjaFeatureFlagsPublicKeyResourceName)` |

These attributes are readable at runtime via `Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` and allow tooling to discover build identity without parsing the manifest JSON.

### 4. Validate the manifest (`ValidateSharpNinjaFeatureFlagManifest`)

Runs `flagctl validate` against the manifest file, passing `--product-id`, `--release-id`, `--schema-version`, and `--public-key` (when present). The build fails if the CLI exits with a non-zero code.

### 5. Generate registration source (`GenerateSharpNinjaFeatureFlagsRegistrationSource`)

When `SharpNinjaFeatureFlagsGenerateRegistrationSource` is `true`, writes a `.g.cs` file and compiles it into the project. See the next section.

---

## Generated Registration Overload

Enable source generation by setting one property:

```xml
<PropertyGroup>
  <SharpNinjaFeatureFlagsGenerateRegistrationSource>true</SharpNinjaFeatureFlagsGenerateRegistrationSource>
</PropertyGroup>
```

The target writes a file equivalent to:

```csharp
// <auto-generated />
namespace SharpNinja.FeatureFlags;

/// <summary>Generated registration overload for build-stamped SharpNinja Feature Flags defaults.</summary>
public static partial class SharpNinjaFeatureFlagsGeneratedServiceCollectionExtensions
{
    private const string ProductId = "truckmate";          // from $(ProductId)
    private const string ReleaseId = "truckmate-1.2.0-stable-0"; // from $(ReleaseId)
    private const string EnvironmentName = "Development";  // from $(SharpNinjaFeatureFlagsEnvironment)
    private const string ManifestResourceName = "SharpNinja.FeatureFlags.BundledManifest.json";

    /// <summary>Registers SharpNinja Feature Flags with build-stamped identity and bundled manifest resource.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSharpNinjaFeatureFlags(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new SharpNinjaFeatureFlagOptions(
            ProductId,
            ReleaseId,
            EnvironmentName,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(1));

        return SharpNinjaFeatureFlagServiceCollectionExtensions.AddSharpNinjaFeatureFlags(
            services,
            options.Validate(),
            ReadEmbeddedResource(ManifestResourceName));
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        var assembly = typeof(SharpNinjaFeatureFlagsGeneratedServiceCollectionExtensions).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded feature flag resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
```

With generation enabled, registration in your startup code becomes a single call:

```csharp
services.AddSharpNinjaFeatureFlags();
```

The generated overload uses `TimeSpan.FromMinutes(5)` for `ManifestRefreshInterval` and `TimeSpan.FromMinutes(1)` for `ExposureUploadInterval`. Override these by calling the explicit overloads instead.

---

## Diagnostic Codes

The build targets emit plain MSBuild errors rather than Roslyn diagnostic codes. The following codes are reserved in the SDK specification for future Roslyn analyzer support:

| Code | Severity | Description |
|---|---|---|
| `SNFF0001` | Error | `ProductId` MSBuild property is missing or blank when a manifest file is present. |
| `SNFF0002` | Error | `ReleaseId` MSBuild property is missing or blank when a manifest file is present. |
| `SNFF0003` | Error | Public key file is missing and `SharpNinjaFeatureFlagsRequirePublicKey` is `true`. |

---

## CI Integration Examples

### GitHub Actions

```yaml
name: build

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install flagctl
        run: dotnet tool install --global SharpNinja.FeatureFlags.Cli

      - name: Build
        run: >
          dotnet build MyApp/MyApp.csproj
          -c Release
          /p:ProductId=truckmate
          /p:ReleaseId=truckmate-${{ github.run_number }}-stable-0
          /p:SharpNinjaFeatureFlagsEnvironment=Production

      - name: Test
        run: dotnet test MyApp.Tests/MyApp.Tests.csproj -c Release
```

### Azure Pipelines

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: windows-latest

steps:
  - task: UseDotNet@2
    inputs:
      version: '10.0.x'

  - script: dotnet tool install --global SharpNinja.FeatureFlags.Cli
    displayName: Install flagctl

  - task: DotNetCoreCLI@2
    displayName: Build
    inputs:
      command: build
      projects: 'MyApp/MyApp.csproj'
      arguments: >
        -c Release
        /p:ProductId=truckmate
        /p:ReleaseId=truckmate-$(Build.BuildNumber)-stable-0
        /p:SharpNinjaFeatureFlagsEnvironment=Production

  - task: DotNetCoreCLI@2
    displayName: Test
    inputs:
      command: test
      projects: 'MyApp.Tests/MyApp.Tests.csproj'
      arguments: '-c Release'
```

### Per-platform builds (multi-target)

The `SharpNinja.FeatureFlags` runtime SDK targets all five platforms. Build each framework separately in CI to verify platform-specific compilation:

```yaml
strategy:
  matrix:
    include:
      - platform: linux
        os: ubuntu-latest
        framework: net10.0
        workloads: ''
      - platform: android
        os: ubuntu-latest
        framework: net10.0-android
        workloads: android
      - platform: ios
        os: macos-latest
        framework: net10.0-ios
        workloads: ios
      - platform: maccatalyst
        os: macos-latest
        framework: net10.0-maccatalyst
        workloads: maccatalyst
      - platform: windows
        os: windows-latest
        framework: net10.0-windows10.0.19041.0
        workloads: ''

steps:
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '10.0.x'

  - name: Install workloads
    if: matrix.workloads != ''
    run: dotnet workload install ${{ matrix.workloads }}

  - name: Build
    run: >
      dotnet build src/SharpNinja.FeatureFlags/SharpNinja.FeatureFlags.csproj
      -c Release
      -f ${{ matrix.framework }}
```

---

## Typical Project File Layout

```
MyApp/
  flags/
    flags.json              # manifest (SharpNinjaFeatureFlagsManifest)
    public-key.ed25519      # signing key (SharpNinjaFeatureFlagsPublicKey)
  MyApp.csproj
```

`MyApp.csproj` minimal setup:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <ProductId>truckmate</ProductId>
    <ReleaseId>truckmate-1.2.0-stable-0</ReleaseId>
    <SharpNinjaFeatureFlagsGenerateRegistrationSource>true</SharpNinjaFeatureFlagsGenerateRegistrationSource>
    <SharpNinjaFeatureFlagsEnvironment>Production</SharpNinjaFeatureFlagsEnvironment>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="SharpNinja.FeatureFlags" Version="1.0.0" />
    <PackageReference Include="SharpNinja.FeatureFlags.Build" Version="1.0.0" />
  </ItemGroup>
</Project>
```

With this setup, the build:
1. Validates that `ProductId` and `ReleaseId` are set.
2. Embeds `flags/flags.json` as `SharpNinja.FeatureFlags.BundledManifest.json`.
3. Embeds `flags/public-key.ed25519` as `SharpNinja.FeatureFlags.ManifestPublicKey.ed25519`.
4. Stamps four `AssemblyMetadata` attributes.
5. Runs `flagctl validate` against the manifest.
6. Generates `SharpNinjaFeatureFlagsGeneratedServiceCollectionExtensions` with a zero-arg `AddSharpNinjaFeatureFlags()`.
