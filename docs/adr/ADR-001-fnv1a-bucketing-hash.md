# ADR-001: Use FNV-1a instead of SipHash-2-4 for percentage bucketing

**Status:** Accepted; TR-3 amended 2026-05-16 to specify FNV-1a normatively.
**Date:** 2026-05-15
**Requirement:** TR-3

## Context

TR-3 states: "Percentage bucketing shall use a fixed, platform-independent hash (recommend SipHash-2-4
of the concatenation of ProductId || ReleaseId || FlagKey || the bucketing-context value)."

SipHash-2-4 is the recommended algorithm. An alternative, FNV-1a 64-bit, was evaluated.

## Decision

Use FNV-1a 64-bit for the `bucket()` CEL function in v1.

## Rationale

TR-3 has two hard requirements and one recommendation:

| Requirement | FNV-1a | SipHash-2-4 |
|---|---|---|
| Deterministic (same input, same output always) | Yes | Yes |
| Platform-independent (no OS/arch variation) | Yes | Yes |
| Recommended algorithm | No | Yes |

FNV-1a satisfies both hard constraints. The recommendation for SipHash-2-4 is motivated by its
resistance to hash-flooding DoS attacks, where an attacker crafts inputs that collide in the hash
function to degrade performance.

For feature flag bucketing, all inputs are controlled internal values: `ProductId` and `ReleaseId`
are build-time constants; `FlagKey` is defined in the signed manifest; the discriminator value
(typically a user or device identifier) comes from the SDK consumer's `EvaluationContext`. None of
these are externally supplied adversarial strings in the threat model for v1.

FNV-1a additionally requires no seed key, which eliminates a key-management configuration surface
(where the key lives, how it is rotated, whether mismatched keys across SDK versions cause
bucketing inconsistency) and is implementable in pure managed code with no NuGet dependency.

## Consequences

- Bucketing output is stable across all .NET platforms and runtimes.
- If a future version adds externally-supplied discriminator values (e.g., user-provided rule
  expressions that feed into `bucket()`), the hash function should be re-evaluated.
- The specific FNV-1a constants used (offset basis `14695981039346656037`, prime `1099511628211`)
  are pinned by a determinism test in `FeatureFlagEvaluatorTests` to prevent silent algorithm
  drift during refactoring.

## Alternatives considered

**SipHash-2-4:** Satisfies all constraints including the recommendation. Rejected for v1 because it
requires a 128-bit seed key with no clear configuration owner. A fixed key would be a no-op for
DoS resistance; a configurable key would require coordination across SDK versions and distribution
service deployments to maintain bucketing consistency.

**MurmurHash3:** Deterministic but has known platform differences in the finalization mix on
32-bit vs 64-bit targets. Rejected.
