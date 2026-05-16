# Technical Requirements (MCP Server)

## TR-1

**TR-1 - Target frameworks.** SDK targets `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0`, and plain `net10.0` for Linux. Long-term support framework only; no .NET Framework support.

## TR-10

**TR-10 - Observability.** The admin plane and Distribution service shall expose Prometheus metrics. The SDK shall expose a diagnostic snapshot API for in-app debug menus.

## TR-11

**TR-11 - Dependency Injection and ILogger are non-negotiable at all layers.** Every type in every component shall obtain its collaborators by constructor injection from a `Microsoft.Extensions.DependencyInjection` container, and shall accept `ILogger<TSelf>` (or an equivalent typed logger) wherever logging is performed. Static loggers (`LogManager.GetLogger()`, `Log.ForContext<T>()`, ambient `Trace`/`Debug` calls), service locators, and `new`-ing of injectable dependencies are forbidden. Every public package shall ship `IServiceCollection` extension methods (and, where the target host is MAUI, `MauiAppBuilder` extensions) such that registration is a single call from the consumer's composition root. No public type shall be constructable outside the container; constructors of injectable types are not part of the supported API. Tests resolve subjects from a configured test container, never via direct `new`. The full implications across each layer are described in §7.

## TR-2

**TR-2 - AOT and trim safety.** The SDK and rule evaluator shall be AOT-compatible (iOS, NativeAOT) and trim-safe. This forbids `Reflection.Emit`, dynamic expression compilation, and code generation at runtime. The evaluator shall be a tree-walking interpreter over parsed AST nodes.

## TR-3

**TR-3 - Determinism across platforms.** Percentage bucketing shall use a fixed, platform-independent hash of the concatenation of `ProductId || ReleaseId || FlagKey || the bucketing-context value`. In v1 the implementation uses FNV-1a 64-bit with offset basis `14695981039346656037` and prime `1099511628211`; both constants are pinned by determinism tests in `FeatureFlagEvaluatorTests` and `ExperimentAssignerTests` to prevent silent algorithm drift. The selection of FNV-1a over the earlier SipHash-2-4 recommendation is recorded in [ADR-001](../../../adr/ADR-001-fnv1a-bucketing-hash.md); the threat model assumes all bucketing inputs are internal (build-time `ProductId`/`ReleaseId`, signed manifest `FlagKey`, SDK-controlled discriminator), so hash-flooding DoS is not in scope for v1. If a future version admits externally-supplied discriminator strings, the hash function shall be re-evaluated under ADR-001's Consequences section. Floating-point comparisons in rules shall use a documented epsilon or be forbidden entirely in v1. The v1 implementation chooses the "documented" path: `RulePredicateValidator` coerces every numeric operand (`int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`) to `System.Decimal` before any relational or equality comparison. `NaN` and `Infinity` are rejected as non-comparable. Coercion to `decimal` performs the comparison in 128-bit base-10 arithmetic, which is exact for all manifest- or context-supplied numeric literals representable in CEL and therefore eliminates the IEEE-754 epsilon problem without an explicit tolerance constant.

## TR-4

**TR-4 - Signing.** Manifests shall be signed with Ed25519. The public key shall be embedded in the SDK at build time. Key rotation requires a new SDK build; this is acceptable because builds are routine.

## TR-5

**TR-5 - Offline-first.** All public SDK calls shall be synchronous and constant-time relative to network state. No public API may block on network I/O. Fetching is a background task; evaluation reads from in-memory state.

## TR-6

**TR-6 - Cache hierarchy.** Resolution order on each `Evaluate`: (1) in-memory cached Manifest; (2) on-disk cached Manifest loaded once at startup; (3) bundled-default Manifest. Network fetching only refreshes (1) and (2); evaluation never waits on it.

## TR-7

**TR-7 - Telemetry budget.** Exposure events shall be coalesced; the SDK shall not emit more than one network request per N seconds (configurable, default 30) for telemetry under steady-state usage.

## TR-8

**TR-8 - Backward compatibility.** The Manifest schema shall be versioned. Newer SDKs shall read older Manifests. Older SDKs shall reject Manifests with a version field higher than they understand and fall back to bundled defaults.

## TR-9

**TR-9 - Security.** The Distribution API shall use product-scoped API keys plus device-attestation where the platform supports it (Play Integrity, App Attest). The admin plane shall enforce SSO and per-Product RBAC.

## TR-12

**TR-12 - Public XML documentation with requirement deeplinks.** Every public surface in every SharpNinja.FeatureFlags package shall carry XML documentation comments. Compliance has three layered duties:

1. **Summary.** Every public type, method, property, event, field, delegate, and enum member shall have a `<summary>` element. Summaries are one or two sentences, written in active voice, and describe what the member does or represents from the consumer's perspective, not how it is implemented. Single-line summaries are preferred; multi-line summaries are wrapped at a reasonable column.
2. **Detailed remarks.** Every public type and every public method whose behavior is not fully obvious from its signature shall additionally have a `<remarks>` element. Remarks cover threading and lifetime expectations, side effects, ordering or invocation constraints, error conditions, and any consumer-visible state machine. Property remarks are required when the property has non-trivial validation, setter side effects, or invalidates cached state. Trivial DTO or record properties whose name and type fully describe the member may omit `<remarks>`.
3. **Requirement deeplinks.** Every public type shall name every functional requirement (FR-N) and technical requirement (TR-N) it implements at the front of its `<summary>` (existing convention: `FR-5 TR-2: ...`). Each first-level FR/TR backreference shall additionally appear once within the type's `<remarks>` as a GitHub deeplink to the requirement section in this wiki, using the canonical URL form `https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Functional-Requirements.md#fr-N` or `https://github.com/sharpninja/FeatureFlags/blob/main/docs/Project/wiki/github/Technical-Requirements.md#tr-N`. Deeplinks shall live in a `<see href="..."/>` element so the IntelliSense view renders a clickable URL. When a public member implements a requirement that its containing type does not, the deeplink shall appear on the member instead.

Enforcement. CS1591 (missing XML doc) is escalated to error project-wide via `Directory.Build.props` and `TreatWarningsAsErrors=true`. An architecture test in `ArchitectureTests` shall assert that every public type with one or more FR/TR identifiers in its `<summary>` also contains the corresponding deeplinks in its `<remarks>`, and that every public type without FR/TR backreferences carries an explicit `<remarks>` annotation explaining why (typical exemptions: framework-required infrastructure types such as EF Migrations and design-time factories, and pure data records whose summary is fully descriptive).

Scope. TR-12 applies to packages published under the `SharpNinja.FeatureFlags*` package id family. Internal types and types in `*.Tests`, `*.Build`, samples, and tooling assemblies are out of scope. Source-generated members inherit the deeplink convention through `[GeneratedCode]` attribution rather than direct XML comments.

