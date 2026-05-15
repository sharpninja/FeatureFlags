# FeatureFlags v1.0.0 Release Notes

Release date: TBD
Commit: b0adf4ea95500fe02db0b47334cb128b8d10141e

---

## What's New

This is the initial production release of the SharpNinja FeatureFlags ecosystem. It delivers a full feature-flag SDK, evaluation engine, manifest system, admin plane, distribution service, and CLI tooling for .NET 10 applications targeting Android, iOS, macOS Catalyst, Windows, and Linux.

Key capabilities in v1.0:

- Compile-time product identity stamped via MSBuild source generator, immutable at runtime (FR-1).
- Bundled default manifests embedded as assembly resources for fully offline-capable startup (FR-2).
- Background remote manifest fetch with Ed25519 signature verification and three-tier cache: in-memory, on-disk, bundled-default (FR-3, TR-4, TR-6).
- Deterministic rule evaluation using a CEL (Common Expression Language) tree-walking interpreter that is AOT-safe and trim-safe for iOS and NativeAOT targets (FR-4, FR-5, TR-2, TR-3).
- N-dimensional evaluation context with ProductId and ReleaseId as required dimensions; arbitrary additional context fields supported (FR-6).
- Kill-switch support on any flag with a forced-refresh path that bypasses normal cache TTLs (FR-7).
- Exposure event tracking with offline buffering and configurable best-effort upload cadence (FR-8, TR-7).
- Admin plane (Docker-hosted Blazor Web App) with full flag CRUD, rule editor, manifest compose-and-sign workflow, environment promotion, and append-only audit log (FR-9, FR-11).
- Product-scoped flag access: flags declare permitted products; unauthorized reads return Default and emit a warning (FR-10).
- CI schema validation via `flagctl` CLI: validates flag definitions, CEL rule syntax, type safety, and product-scope correctness; fails builds on violations (FR-12).
- Full Microsoft.Extensions.DependencyInjection integration at every layer; ILogger<T> throughout; no static loggers or service locators (TR-11).
- Architecture enforced by automated ArchitectureTests that run in every CI build.

---

## Packages

Three NuGet packages are produced from this release:

| Package | Description |
| --- | --- |
| `SharpNinja.FeatureFlags.Abstractions` | Options record, ISharpNinjaFeatureFlagAdmin, diagnostic types, ManifestSource factory. Depends only on OpenFeature and Microsoft.Extensions.Logging.Abstractions. |
| `SharpNinja.FeatureFlags` | Full SDK implementation: provider, client, manifest store, CEL evaluator, signature verifier, distribution fetcher, hosted service, and AddSharpNinjaFeatureFlags extension method. |
| `SharpNinja.FeatureFlags.Build` | MSBuild integration and source generator for compile-time product identity stamping. |

Additional internal service packages (not for direct consumer reference):

- `SharpNinja.FeatureFlags.Admin` - Blazor admin plane host.
- `SharpNinja.FeatureFlags.Admin.Data` - EF Core provider-agnostic DbContext.
- `SharpNinja.FeatureFlags.Admin.Data.Postgres` - PostgreSQL migrations.
- `SharpNinja.FeatureFlags.Admin.Data.SqlServer` - SQL Server migrations.
- `SharpNinja.FeatureFlags.Admin.Data.Sqlite` - SQLite migrations (dev/test).
- `SharpNinja.FeatureFlags.Cli` - flagctl CLI tool.
- `SharpNinja.FeatureFlags.Cqrs` - CQRS command and query infrastructure.
- `SharpNinja.FeatureFlags.Distribution` - Distribution service host.
- `SharpNinja.FeatureFlags.Evaluation` - CEL rule engine (pure library, no I/O).
- `SharpNinja.FeatureFlags.Manifest` - Manifest loader and cache.

---

## Supported Target Frameworks

- `net10.0` (Linux, generic host, console, ASP.NET Core)
- `net10.0-android`
- `net10.0-ios`
- `net10.0-maccatalyst`
- `net10.0-windows10.0.19041.0`

---

## Breaking Changes

None. This is the initial release.

---

## Requirements Implemented

All 12 functional requirements from the v1 planning artifact are implemented and validated:

FR-1, FR-2, FR-3, FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-10, FR-11, FR-12.

All 11 technical requirements are implemented and verified:

TR-1, TR-2, TR-3, TR-4, TR-5, TR-6, TR-7, TR-8, TR-9, TR-10, TR-11.

Full traceability from requirements to implementation is maintained in:
`docs/Project/wiki/github/TR-per-FR-Mapping.md`

---

## Validation Summary

| Gate | Result |
| --- | --- |
| Compile (all TFMs) | PASSED - 0 warnings, 0 errors |
| Unit tests | PASSED - 111 passed, 0 failed |
| Integration tests (Avalonia12) | PASSED - 4 passed, 0 failed |
| ValidateConfig | PASSED - 6 checks |
| ValidateTraceability | PASSED - 99 source files |

See `docs/VALIDATION-EVIDENCE-V1.md` for full detail and human sign-off record.
