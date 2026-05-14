# Functional Requirements

Seeded from `docs/Feature-Flag-Ecosystem-Planning-v0.1.md`.

## **FR-1 — Compile-time product identity.**

**FR-1 — Compile-time product identity.** The build system shall stamp each produced binary with an immutable ProductId and ReleaseId such that no runtime configuration can change them. Attempting to override these values at runtime shall fail loudly.

## **FR-2 — Bundled defaults.**

**FR-2 — Bundled defaults.** Every build shall include a signed default Manifest for its (ProductId, ReleaseId) tuple, embedded as a resource. The application shall be fully functional with no network connectivity using only this manifest.

## **FR-3 — Remote override.**

**FR-3 — Remote override.** The SDK shall fetch newer Manifests from the Distribution service on a configurable cadence, verify their signature against an embedded public key, and persist them to a local cache. If verification fails, the new Manifest shall be discarded and the prior valid Manifest retained.

## **FR-4 — Deterministic evaluation.**

**FR-4 — Deterministic evaluation.** Given identical inputs (Manifest, Evaluation Context), the evaluator shall produce identical outputs across all five target platforms. Determinism is testable and tested.

## **FR-5 — Full ruleset evaluation.**

**FR-5 — Full ruleset evaluation.** The rule engine shall support a CEL (Common Expression Language) dialect over the Evaluation Context, including equality, comparison, set membership, semantic-version comparison via a custom function, and percentage bucketing via a custom function. CEL is chosen over JSONLogic for its formal grammar, deterministic evaluation semantics, sandbox safety (no implicit type coercion ambiguities), sub-millisecond compile-and-cache cost, and proven scale (Kubernetes admission control, Envoy, Cloud Armor, Cerbos). A documented subset of CEL is supported; user-defined functions are forbidden in v1.

## **FR-6 — N-dimensional resolution with Product + Release mandatory.**

**FR-6 — N-dimensional resolution with Product + Release mandatory.** The evaluator shall accept arbitrary context fields. Manifests shall be addressable by (ProductId, ReleaseId) and *may* be further filtered by additional dimensions within rules.

## **FR-7 — Kill switch.**

**FR-7 — Kill switch.** Every flag shall be marked killable: true | false at definition time. Killable flags shall support a forced-refresh path that bypasses normal cache TTLs.

## **FR-8 — Exposure tracking.**

**FR-8 — Exposure tracking.** Each evaluation shall (optionally per flag) emit an exposure event recording flag key, resolved value, the index of the rule that matched, a context fingerprint, and a timestamp. Exposure events shall be buffered offline and uploaded best-effort.

## **FR-9 — Authoring and audit.**

**FR-9 — Authoring and audit.** All flag creation, rule changes, and manifest publishes shall be auditable: who, what, when, why. The system shall preserve full history; no destructive edits.

## **FR-10 — Product-scoping.**

**FR-10 — Product-scoping.** Every flag shall declare which Products are permitted to evaluate it. Attempts to read a flag from a non-scoped Product shall return Default and emit a warning.

## **FR-11 — Multi-environment.**

**FR-11 — Multi-environment.** The admin plane shall support at least dev / staging / production environments per the Byrd deployment model. Promotion between environments shall be an explicit, audited action.

## **FR-12 — Schema validation in CI.**

**FR-12 — Schema validation in CI.** The CLI shall validate flag definitions, rule syntax, type safety, and Product-scope correctness in CI, failing builds on violations before any binary ships.

