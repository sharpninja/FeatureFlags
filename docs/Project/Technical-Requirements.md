# Technical Requirements

Seeded from `docs/Feature-Flag-Ecosystem-Planning-v0.1.md`.

## **TR-1 — Target frameworks.**

**TR-1 — Target frameworks.** SDK targets `net8.0`, `net8.0-android`, `net8.0-ios`, `net8.0-maccatalyst`, `net8.0-windows10.0.19041.0`, and plain `net8.0` for Linux. Long-term support framework only; no .NET Framework support.

## **TR-2 — AOT and trim safety.**

**TR-2 — AOT and trim safety.** The SDK and rule evaluator shall be AOT-compatible (iOS, NativeAOT) and trim-safe. This forbids `Reflection.Emit`, dynamic expression compilation, and code generation at runtime. The evaluator shall be a tree-walking interpreter over parsed AST nodes.

## **TR-3 — Determinism across platforms.**

**TR-3 — Determinism across platforms.** Percentage bucketing shall use a fixed, platform-independent hash (recommend SipHash-2-4 of the concatenation of ProductId || ReleaseId || FlagKey || the bucketing-context value). Floating-point comparisons in rules shall use a documented epsilon or be forbidden entirely in v1.

## **TR-4 — Signing.**

**TR-4 — Signing.** Manifests shall be signed with Ed25519. The public key shall be embedded in the SDK at build time. Key rotation requires a new SDK build; this is acceptable because builds are routine.

## **TR-5 — Offline-first.**

**TR-5 — Offline-first.** All public SDK calls shall be synchronous and constant-time relative to network state. No public API may block on network I/O. Fetching is a background task; evaluation reads from in-memory state.

## **TR-6 — Cache hierarchy.**

**TR-6 — Cache hierarchy.** Resolution order on each `Evaluate`: (1) in-memory cached Manifest; (2) on-disk cached Manifest loaded once at startup; (3) bundled-default Manifest. Network fetching only refreshes (1) and (2); evaluation never waits on it.

## **TR-7 — Telemetry budget.**

**TR-7 — Telemetry budget.** Exposure events shall be coalesced; the SDK shall not emit more than one network request per N seconds (configurable, default 30) for telemetry under steady-state usage.

## **TR-8 — Backward compatibility.**

**TR-8 — Backward compatibility.** The Manifest schema shall be versioned. Newer SDKs shall read older Manifests. Older SDKs shall reject Manifests with a version field higher than they understand and fall back to bundled defaults.

## **TR-9 — Security.**

**TR-9 — Security.** The Distribution API shall use product-scoped API keys plus device-attestation where the platform supports it (Play Integrity, App Attest). The admin plane shall enforce SSO and per-Product RBAC.

## **TR-10 — Observability.**

**TR-10 — Observability.** The admin plane and Distribution service shall expose Prometheus metrics. The SDK shall expose a diagnostic snapshot API for in-app debug menus.

## **TR-11 — Dependency Injection and ILogger are non-negotiable at all layers.**

**TR-11 — Dependency Injection and ILogger are non-negotiable at all layers.** Every type in every component shall obtain its collaborators by constructor injection from a `Microsoft.Extensions.DependencyInjection` container, and shall accept `ILogger<TSelf>` (or an equivalent typed logger) wherever logging is performed. Static loggers (`LogManager.GetLogger()`, `Log.ForContext<T>()`, ambient `Trace`/`Debug` calls), service locators, and `new`-ing of injectable dependencies are forbidden. Every public package shall ship `IServiceCollection` extension methods (and, where the target host is MAUI, `MauiAppBuilder` extensions) such that registration is a single call from the consumer's composition root. No public type shall be constructable outside the container; constructors of injectable types are not part of the supported API. Tests resolve subjects from a configured test container, never via direct `new`. The full implications across each layer are described in §7.

