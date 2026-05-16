# SharpNinja Feature Flags

SharpNinja Feature Flags is a compile-time-scoped, offline-first feature flag SDK for .NET 10 applications. Flags are evaluated synchronously from a signed, versioned manifest embedded in the application binary. Remote refresh, exposure tracking, an Admin authoring plane, and a Distribution service are all included.

**Current version:** 1.0.0 | **Target framework:** net10.0 (Android, iOS, Mac Catalyst, Windows)

---

## Documentation

### Getting Started

- [Getting Started](Getting-Started) - Install packages, register services, evaluate your first flag.

### SDK Reference

- [Configuration Reference](Configuration-Reference) - All `SharpNinjaFeatureFlagOptions` properties and sub-types.
- [Flag Manifest Reference](Flag-Manifest-Reference) - JSON manifest schema, signing, and environment rules.
- [CEL Rules Reference](CEL-Rules-Reference) - Operators, macros, custom functions, and example expressions.

### Build and Tooling

- [MSBuild Integration](MSBuild-Integration) - Properties, targets, source generation, and CI setup.
- [CLI Reference](CLI-Reference) - `flagctl validate` options, exit codes, and diagnostic codes.

### Infrastructure

- [Admin Plane Guide](Admin-Guide) - Flag authoring, RBAC, publish workflow, Docker setup.
- [Distribution Service](Distribution-Service) - REST endpoints, authentication, ETag protocol, Docker setup.

---

## Quick Example

```csharp
// 1. Register (minimal)
services.AddSharpNinjaFeatureFlags(options, manifestJson);

// 2. Evaluate
ISharpNinjaFeatureClient client = provider.GetRequiredService<ISharpNinjaFeatureClient>();
bool enabled = client.Evaluate("dashboard.enabled", defaultValue: false).Value;

// 3. Evaluate with context
EvaluationContext ctx = EvaluationContext.Builder().Set("user.role", "admin").Build();
string title = client.Evaluate("reports.title", defaultValue: "Reports", context: ctx).Value;
```

---

## Architecture Overview

```
Application binary
  └── Embedded manifest (signed, product-scoped)
        └── ISharpNinjaFeatureClient  ← synchronous evaluation, zero network I/O
              ├── In-memory cache       (latest verified manifest)
              ├── On-disk cache         (%LOCALAPPDATA%/SharpNinja/FeatureFlags/...)
              └── Bundled default       (embedded resource, always available)

Background coordinators
  ├── SharpNinjaRemoteFetchCoordinator    ← polls Distribution service on interval
  └── SharpNinjaExposureUploadCoordinator ← drains exposure outbox on interval

Distribution service   (Docker, port 18081)
  └── /v1/manifest/{productId}/{releaseId}  ← fetched by SDK

Admin plane            (Docker, port 18080)
  └── Flag CRUD, sign-and-publish, environment promotion, audit trail
```

---

## Package Inventory

| Package | Purpose |
|---|---|
| `SharpNinja.FeatureFlags` | Runtime SDK: evaluation, caching, refresh, exposure upload |
| `SharpNinja.FeatureFlags.Abstractions` | Public types, interfaces, options |
| `SharpNinja.FeatureFlags.Build` | MSBuild integration: manifest embedding, identity stamping, source generation, build-time validation |
| `SharpNinja.FeatureFlags.Manifest` | Manifest parsing, Ed25519 signature verification |
| `SharpNinja.FeatureFlags.Evaluation` | CEL rule evaluator |
| `SharpNinja.FeatureFlags.Admin` | Admin runtime service, RBAC, audit |
| `SharpNinja.FeatureFlags.Distribution` | Distribution HTTP service |
| `SharpNinja.FeatureFlags.Cli` | `flagctl` tool |
