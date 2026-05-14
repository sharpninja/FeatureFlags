# TR per FR Mapping

| Functional Requirement | Technical Requirements | Notes |
| --- | --- | --- |
| FR-1 | TR-1, TR-2, TR-5, TR-11 | Compile-time product/release identity is stamped into SDK options and must be injected into evaluation context without allowing runtime override. |
| FR-2 | TR-4, TR-5, TR-6, TR-8, TR-11 | Bundled defaults depend on signing, offline-first load order, manifest compatibility, and DI-resident manifest services. |
| FR-3 | TR-4, TR-5, TR-6, TR-8, TR-9, TR-10, TR-11 | Remote override requires signed Distribution responses, product-scoped auth, cache semantics, and observable refresh behavior. |
| FR-4 | TR-2, TR-3, TR-5, TR-11 | Deterministic evaluation must remain AOT-safe, synchronous with respect to network state, and DI-composed. |
| FR-5 | TR-2, TR-3, TR-5, TR-11 | Rule evaluation requires a deterministic, trim-safe interpreter and no runtime code generation. |
| FR-6 | TR-3, TR-5, TR-8, TR-11 | Product and Release are mandatory addressing dimensions while additional context fields remain extensible. |
| FR-7 | TR-5, TR-6, TR-10, TR-11 | Kill-switch behavior depends on forced refresh, cache hierarchy, observability, and DI-resident admin/client services. |
| FR-8 | TR-5, TR-7, TR-9, TR-10, TR-11 | Exposure tracking must not block evaluation, must respect telemetry budgets, and must support authenticated Distribution ingestion. |
| FR-9 | TR-9, TR-10, TR-11 | Authoring and audit require policy enforcement, observability, append-only services, and typed DI/logging. |
| FR-10 | TR-5, TR-9, TR-10, TR-11 | Product scoping is enforced during evaluation and admin authoring, with warning/audit/diagnostic paths. |
| FR-11 | TR-9, TR-10, TR-11 | Multi-environment support requires audited promotion, scoped authorization metadata, and DI-composed admin services. |
| FR-12 | TR-1, TR-2, TR-8, TR-11 | CI validation must use the supported target frameworks, trim-safe validators, schema compatibility rules, and central DI registration. |
