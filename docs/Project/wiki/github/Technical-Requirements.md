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

