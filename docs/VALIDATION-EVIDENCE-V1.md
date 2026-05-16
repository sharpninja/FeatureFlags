# FeatureFlags v1.0 Validation Evidence

## v1.0.2 close-out (RELEASE-V1-AUDIT-001)

Date: 2026-05-16
Tag: v1.0.2
Tracker: RELEASE-V1-AUDIT-001 (26 implementation tasks, all complete)

### Headline

`dotnet build` 0 warnings, 0 errors across the full solution. `dotnet test --no-build` 296 passed, 0 failed, 0 skipped, across 18 test assemblies. Up from the v1.0.1 baseline of 271 (+25 net: +9 from test-gap closeout, +6 audit-domain hardening, +1 architecture deeplink rule, +5 Distribution OIDC, +4 Admin.IdentityServer).

### Audit gap closeouts landed in v1.0.2

| Task | Workstream | Outcome |
| --- | --- | --- |
| TR-3 spec drift | docs | `Technical-Requirements.md` amended to normatively specify FNV-1a 64-bit with pinned constants and link ADR-001. |
| TR-3 float-comparison policy | tests | `RulePredicateValidator` decimal-coercion + NaN/Infinity rejection covered by `RulePredicateValidatorTests`. |
| TR-9 device attestation | docs | Path forward documented in ADR-003 / `Admin-IdentityServer.md`; stub validator remains for v1; Play Integrity / App Attest deferred. |
| TR-9 SSO OIDC | code + tests | Embedded Duende IdentityServer 7.4.7 in `SharpNinja.FeatureFlags.Admin.IdentityServer`; Admin host + Blazor OIDC client + Distribution JwtBearer + new `/admin/diagnostics`. |
| TR-12 (new) | docs + tests | XML doc + GitHub deeplink contract on every public type; 131 `<remarks>` additions, 377 deeplinks (169 FR + 208 TR); `RequirementDeeplinkTests` enforces. |
| FR-5 UDF rejection | tests | `RulePredicateValidatorTests` asserts FFCEL_SYNTAX/FFCEL_TYPE rejection. |
| FR-9 audit immutability + Reason field | code + tests | `EfCoreAdminRuntimeStore.AppendAuditEntryAsync` now validates `Reason`; `AuditImmutabilityTests` covers reason validation, row immutability, and reflection guard. |
| FR-9 / FR-11 Promotion audit | tests | `PromotionAuditTests` asserts `Promoted` audit row carries source + target env. |
| FR-10 unauthorized warning | tests | `SharpNinjaFeatureClientAuditTests` captures `ProductScopeDenied` Warning via `CapturingTypedLogger`. |
| FR-11 wording | docs | `Functional-Requirements.md` clarifies custom envs live in draft only; normalized to Dev/Staging/Prod at publish. |
| TR-8 SDK schemaVersion gate | code + tests | `SharpNinjaActiveManifestStore.ParseAndValidate` now rejects `schemaVersion > MaxSupportedSchemaVersion`; bundled manifest preserved; covered in `SharpNinjaFeatureClientAuditTests`. |
| Backreference cleanup | docs | 30 public-type summaries tagged: 2 FR/TR additions, 28 `out-of-v1` markers. |
| Testing-Requirements.md | docs | 11 TEST-* sections populated from TR-per-FR-Mapping. |
| Phase 5 DiagnosticSnapshot test | tests | `DiagnosticSnapshot` content assertions in `SharpNinjaFeatureClientAuditTests`. |
| Duende license + signing | docs | Community-license + dev/prod signing-credential story in `Admin-IdentityServer.md` and ADR-003. |

### Workstream-by-workstream test deltas

| Test Assembly | v1.0.1 | v1.0.2 | Delta |
| --- | --- | --- | --- |
| SharpNinja.FeatureFlags.Abstractions.Tests | 22 | 22 | 0 |
| SharpNinja.FeatureFlags.Admin.Tests | 10 | 11 | +1 (Promotion audit) |
| SharpNinja.FeatureFlags.Admin.Data.Tests | 3 | 8 | +5 (Audit immutability + Reason validation) |
| SharpNinja.FeatureFlags.Admin.Blazor.Tests | 14 | 14 | 0 |
| SharpNinja.FeatureFlags.Admin.IdentityServer.Tests | n/a | 4 | +4 (Discovery + Token issuance) |
| ArchitectureTests | 2 | 3 | +1 (RequirementDeeplinkTests) |
| SharpNinja.FeatureFlags.Avalonia12.IntegrationTests | 4 | 4 | 0 |
| SharpNinja.FeatureFlags.Build.Tests | 73 | 73 | 0 |
| SharpNinja.FeatureFlags.Cli.Tests | 11 | 11 | 0 |
| SharpNinja.FeatureFlags.Cqrs.Tests | 42 | 42 | 0 |
| SharpNinja.FeatureFlags.Distribution.Tests | 15 | 20 | +5 (AdminDiagnostics auth) |
| SharpNinja.FeatureFlags.Evaluation.Tests | 19 | 25 | +6 (TR-3 decimal + FR-5 UDF rejection) |
| SharpNinja.FeatureFlags.Experimentation.Tests | 8 | 8 | 0 |
| SharpNinja.FeatureFlags.Generators.Tests | 17 | 17 | 0 |
| SharpNinja.FeatureFlags.Manifest.Tests | 10 | 10 | 0 |
| SharpNinja.FeatureFlags.MediatR.Tests | 5 | 5 | 0 |
| SharpNinja.FeatureFlags.Tests | 12 | 15 | +3 (FR-10 warn + TR-8 schemaVersion + DiagnosticSnapshot) |
| SharpNinja.FeatureFlags.Wolverine.Tests | 4 | 4 | 0 |
| **Total** | **271** | **296** | **+25** |

### Architecture decisions captured

- ADR-001 status updated: TR-3 amended 2026-05-16 to specify FNV-1a normatively.
- ADR-003 (new): embedded Duende IS as sole admin OIDC provider; Duende 7.4.7 (8.0 only alpha); separate `AdminIdentityDbContext`; JwtBearer audience validation skipped due to Duende `client_credentials` token shape (per-scope authorization enforced via `scope` claim); Distribution gets new `/admin/diagnostics` (Prometheus `/metrics` stays anonymous per TR-10).

### Known deferrals

- TR-9 device attestation (Play Integrity / App Attest) still stub-only; documented as v2 extension point.
- Production OIDC token-roundtrip test simplified to anonymous-reject; full TestServer + JwtBearer backchannel deferred to deployment smoke.
- Duende 8.x upgrade pending stable NuGet release.

---

## v1.0 (initial release)

Date: 2026-05-15
Build commit: b0adf4ea95500fe02db0b47334cb128b8d10141e
Build message: test: use v1 products in Avalonia sample

---

## Build Gates

| Gate | Result | Details |
| --- | --- | --- |
| Compile | PASSED | 0 warnings, 0 errors. All TFMs built: net10.0, net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows10.0.19041.0 |
| Test (unit, non-integration) | PASSED | 111 tests passed, 0 failed, 0 skipped |
| Avalonia12 integration tests | PASSED | 4 tests passed, 0 failed, 0 skipped |
| ValidateConfig | PASSED | 6 configuration checks passed |
| ValidateTraceability | PASSED | 99 source files verified |

---

## Test Suite Breakdown

| Test Assembly | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| ArchitectureTests | 2 | 0 | 0 |
| SharpNinja.FeatureFlags.Abstractions.Tests | 10 | 0 | 0 |
| SharpNinja.FeatureFlags.Admin.Data.Tests | 3 | 0 | 0 |
| SharpNinja.FeatureFlags.Admin.Tests | 4 | 0 | 0 |
| SharpNinja.FeatureFlags.Build.Tests | 12 | 0 | 0 |
| SharpNinja.FeatureFlags.Cli.Tests | 11 | 0 | 0 |
| SharpNinja.FeatureFlags.Cqrs.Tests | 30 | 0 | 0 |
| SharpNinja.FeatureFlags.Distribution.Tests | 10 | 0 | 0 |
| SharpNinja.FeatureFlags.Evaluation.Tests | 8 | 0 | 0 |
| SharpNinja.FeatureFlags.Manifest.Tests | 10 | 0 | 0 |
| SharpNinja.FeatureFlags.Tests | 11 | 0 | 0 |
| SharpNinja.FeatureFlags.Avalonia12.IntegrationTests | 4 | 0 | 0 |

**Test Summary: Total 115 | Passed 115 | Failed 0 | Skipped 0**

(111 non-integration tests run via `build.ps1 Test`; 4 integration tests run separately via `dotnet test`)

---

## Functional Requirements Coverage

| FR | Title | Status | Implementing TODOs |
| --- | --- | --- | --- |
| FR-1 | Compile-time product identity | Implemented | IMPL-SDK-RUNTIME-001; IMPL-ABSTRACTIONS-V1-001; IMPL-BUILD-PIPELINE-001 |
| FR-2 | Bundled defaults | Implemented | IMPL-BUILD-PIPELINE-001; IMPL-MANIFEST-VALIDATION-001 |
| FR-3 | Remote override | Implemented | IMPL-SDK-RUNTIME-001; IMPL-DISTRIBUTION-RUNTIME-001 |
| FR-4 | Deterministic evaluation | Implemented | IMPL-SDK-RUNTIME-001; IMPL-EVALUATION-RULES-001; TEST-AVALONIA-SAMPLE-001 |
| FR-5 | Full ruleset evaluation (CEL) | Implemented | IMPL-MANIFEST-VALIDATION-001; IMPL-EVALUATION-RULES-001 |
| FR-6 | N-dimensional resolution | Implemented | IMPL-SDK-RUNTIME-001; IMPL-DISTRIBUTION-RUNTIME-001 |
| FR-7 | Kill switch | Implemented | IMPL-MANIFEST-VALIDATION-001; IMPL-EVALUATION-RULES-001 |
| FR-8 | Exposure tracking | Implemented | IMPL-SDK-RUNTIME-001; IMPL-DISTRIBUTION-RUNTIME-001 |
| FR-9 | Authoring and audit | Implemented | IMPL-ADMIN-RUNTIME-001; IMPL-ADMIN-DATA-PROVIDERS-001; IMPL-DOCKER-HOSTING-001 |
| FR-10 | Product-scoping | Implemented | IMPL-ADMIN-RUNTIME-001; IMPL-DISTRIBUTION-RUNTIME-001; IMPL-MANIFEST-VALIDATION-001 |
| FR-11 | Multi-environment | Implemented | IMPL-ABSTRACTIONS-V1-001; IMPL-ADMIN-DATA-PROVIDERS-001; IMPL-DOCKER-HOSTING-001. Design note: The admin plane accepts custom environment names in draft workflows; however, the admin normalizes them to Development, Staging, or Production before publishing a manifest. Published manifests always use one of these three canonical values. This is intentional - the manifest validator enforces the three-value restriction as the contract for all consumer-facing manifests. |
| FR-12 | Schema validation in CI | Implemented | IMPL-BUILD-PIPELINE-001; IMPL-CLI-VALIDATION-001; IMPL-MANIFEST-VALIDATION-001 |

All 12 FRs: Implemented.

---

## Technical Requirements Gate Checks

| TR | Title | Evidence |
| --- | --- | --- |
| TR-1 | Target frameworks | Compile succeeded for all 5 TFMs (net10.0, android, ios, maccatalyst, windows) |
| TR-2 | AOT and trim safety | CEL evaluator is a tree-walking interpreter; no Reflection.Emit or expression compilation; validated by ArchitectureTests |
| TR-3 | Determinism across platforms | SipHash-2-4 bucketing; cross-platform test in Avalonia12 integration tests (4 passed) |
| TR-4 | Signing | Ed25519 manifest signing implemented; covered by SharpNinja.FeatureFlags.Manifest.Tests (10 passed) |
| TR-5 | Offline-first | All public SDK APIs synchronous; no blocking network I/O in evaluation path |
| TR-6 | Cache hierarchy | Three-tier cache (in-memory, on-disk, bundled-default) implemented; covered by Manifest.Tests |
| TR-7 | Telemetry budget | Exposure event coalescing with configurable flush interval implemented |
| TR-8 | Backward compatibility | Manifest schema versioned; version-field rejection logic implemented |
| TR-9 | Security | Product-scoped API key auth implemented in Distribution; Admin RBAC modeled |
| TR-10 | Observability | Diagnostic snapshot API on ISharpNinjaFeatureFlagAdmin; Distribution service metrics surface |
| TR-11 | DI and ILogger non-negotiable | Enforced by ArchitectureTests (2 passed); all types constructor-injected |

---

## Sign-off

[x] Human reviewer: plbyrd

Date of review: 2026-05-15

Notes: All gates confirmed green. FNV-1a bucketing deviation accepted per ADR-001. FR-2/TR-8 traceability annotations corrected. All TEST-* IDs registered in MCP. Approved for v1.0.0 release.
