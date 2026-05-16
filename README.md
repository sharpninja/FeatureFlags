# SharpNinja Feature Flags

Compile-time-scoped, offline-first feature flags for .NET 10. Flags are evaluated synchronously from a signed, versioned manifest embedded in the application binary. Remote refresh, exposure tracking, an Admin authoring plane, and a Distribution service are all included.

**Version:** 1.0.0 | **Frameworks:** `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0`

---

## Documentation

Full documentation is in the [GitHub Wiki](https://github.com/sharpninja/FeatureFlags/wiki):

- [Getting Started](https://github.com/sharpninja/FeatureFlags/wiki/Getting-Started) - install, register, evaluate
- [Configuration Reference](https://github.com/sharpninja/FeatureFlags/wiki/Configuration-Reference) - all SDK options
- [Flag Manifest Reference](https://github.com/sharpninja/FeatureFlags/wiki/Flag-Manifest-Reference) - JSON schema and signing
- [CEL Rules Reference](https://github.com/sharpninja/FeatureFlags/wiki/CEL-Rules-Reference) - rule syntax and functions
- [MSBuild Integration](https://github.com/sharpninja/FeatureFlags/wiki/MSBuild-Integration) - build properties and source generation
- [CLI Reference](https://github.com/sharpninja/FeatureFlags/wiki/CLI-Reference) - `flagctl validate`
- [Admin Plane Guide](https://github.com/sharpninja/FeatureFlags/wiki/Admin-Guide) - authoring, RBAC, Docker
- [Distribution Service](https://github.com/sharpninja/FeatureFlags/wiki/Distribution-Service) - REST endpoints, Docker

---

## Quick Start

```shell
dotnet add package SharpNinja.FeatureFlags
dotnet add package SharpNinja.FeatureFlags.Build
```

Set the product identity in your `.csproj`:

```xml
<PropertyGroup>
  <ProductId>truckmate</ProductId>
  <ReleaseId>truckmate-1.2.0-stable-0</ReleaseId>
</PropertyGroup>
```

Register in `Program.cs`:

```csharp
services.AddSharpNinjaFeatureFlags(
    new SharpNinjaFeatureFlagOptions(
        productId: "truckmate",
        releaseId: "truckmate-1.2.0-stable-0",
        environment: "production",
        manifestRefreshInterval: TimeSpan.FromMinutes(5),
        exposureUploadInterval: TimeSpan.FromSeconds(30)),
    File.ReadAllText("flags/flags.json"));
```

Evaluate a flag:

```csharp
ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();
bool enabled = client.Evaluate("dashboard.enabled", defaultValue: false).Value;
```

---

## Build

```shell
# Compile all TFMs
./build.ps1 Compile

# Run unit tests (111 tests)
./build.ps1 Test

# Validate manifests and config
./build.ps1 ValidateConfig

# Validate release readiness (NuGet metadata, Docker artifacts)
./build.ps1 ValidateRelease

# Run integration tests (Avalonia headless)
dotnet test tests/SharpNinja.FeatureFlags.Avalonia12.IntegrationTests
```

---

## Packages

| Package | Description |
|---|---|
| `SharpNinja.FeatureFlags` | Runtime SDK: evaluation, caching, refresh, exposure |
| `SharpNinja.FeatureFlags.Abstractions` | Public types and interfaces |
| `SharpNinja.FeatureFlags.Build` | MSBuild integration (build-transitive) |
| `SharpNinja.FeatureFlags.Manifest` | Manifest parsing and Ed25519 verification |
| `SharpNinja.FeatureFlags.Evaluation` | CEL rule evaluator |
| `SharpNinja.FeatureFlags.Admin` | Admin runtime and audit |
| `SharpNinja.FeatureFlags.Distribution` | Distribution HTTP service |
| `SharpNinja.FeatureFlags.Cli` | `flagctl` command-line tool |

---

- Validation evidence: [docs/VALIDATION-EVIDENCE-V1.md](docs/VALIDATION-EVIDENCE-V1.md)
- Release notes: [docs/RELEASE-NOTES-V1.0.md](docs/RELEASE-NOTES-V1.0.md)
- Planning spec: [docs/Feature-Flag-Ecosystem-Planning-v0.1.md](docs/Feature-Flag-Ecosystem-Planning-v0.1.md)
