## 13. Open Questions for Iteration v0.2

These are deliberately unresolved here unless explicitly marked resolved; the Byrd Process expects refinement to occur as Implementation surfaces them.

What is the canonical list of Products planned for v1? This drives admin-plane RBAC modeling and the size of the bundled-default footprint.

Resolved 2026-05-14: v1 Products are TruckMate and DriverMate.

What is the canonical Release lineage strategy — strict semver per Product, channel-and-build (canary/beta/stable + build number), or both? This is needed before the Manifest-addressing scheme is finalized in Phase 0.

Resolved 2026-05-14: use both strict semver per Product and channel-and-build lineage.

What environments exist beyond dev / staging / prod? Some product lines may require regional production environments (e.g. EU vs US data residency); this affects Distribution-service deployment topology.

Resolved 2026-05-14: environments beyond dev / staging / prod are custom-defined.

Which platforms host the admin plane and Distribution service? Cloud, on-prem, or hybrid? This is independent of the SDK design and can be answered later, but affects Phase 3 and Phase 4 effort.

Resolved 2026-05-14: host the admin plane and Distribution service with Docker.

What is the data-retention policy for exposure events? This affects Phase 5 storage design.

Resolved 2026-05-14: exposure-event data retention is user-definable.

Which database providers are required for v1? The architecture supports adding providers later; the question is which assemblies must ship in v1.

Resolved 2026-05-14: v1 database providers are PostgreSQL and SQL Server. SQLite is not a required v1 provider.

Is multi-tenant deployment in scope for v1? The `Tenant` column in the `Entities` table makes it cheap to support, but the admin-plane UX, RBAC, and key-isolation work to make multi-tenancy real is significant and may be a v2 concern.

Resolved 2026-05-14: multi-tenant deployment is in scope for v1.

What is the long-term relationship between this repository's vendored CQRS copy and the upstream `sharpninja/McpServer` repository? Options for v0.2 onward: (a) accept permanent divergence and own the code outright; (b) periodic upstream-sync with backports for non-flag-specific improvements; (c) selective contribution of flag-agnostic adaptations back to McpServer while keeping flag-aware extensions exclusive to this repository. The choice affects how the adaptation delta is structured during Phase 6 discovery — option (c) requires the delta to clearly separate flag-aware from non-flag-aware changes from the first commit.

Resolved 2026-05-14: choose option (a). This repository owns the CQRS fork permanently. The vendored CQRS code is maintained as SharpNinja feature-flag infrastructure, with no expected periodic upstream sync or contribution-back obligation to `sharpninja/McpServer`. This repository's implementation is the forward path; `McpServer.Cqrs` will be deprecated in favor of `SharpNinja.FeatureFlags.Cqrs`.

---

*End of Planning Artifact v0.1. Next iteration begins once the open questions in §12 are answered or explicitly deferred.*
