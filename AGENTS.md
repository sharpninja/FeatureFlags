# Agent Instructions

## System Documentation

Before starting any implementation task, read the agent-facing documentation in `docs/agent/`:

| Document | When to read |
|---|---|
| [Architecture](docs/agent/Architecture.md) | Before any task - all components, data flow, identity model |
| [Admin Service](docs/agent/Admin-Service.md) | Flag authoring, drafts, RBAC, audit trail |
| [Distribution Service](docs/agent/Distribution-Service.md) | HTTP API, auth, endpoints, ETag protocol |
| [SDK Reference](docs/agent/SDK-Reference.md) | Evaluation, context, registration, startup |
| [Manifest Schema](docs/agent/Manifest-Schema.md) | JSON schema, signing, validation codes |
| [CEL Reference](docs/agent/CEL-Reference.md) | Rule expression language, operators, macros, custom functions |
| [Workflows](docs/agent/Workflows.md) | Step-by-step sequences for create/publish/promote/evaluate |

### Critical constraints (read before acting)

- **ProductId catalog**: valid v1 values are `truckmate` and `drivermate` only.
- **Published environment values**: exactly `development`, `staging`, `production`.
- **Evaluation is synchronous and offline-first**: `Evaluate<T>()` never touches the network.
- **Ed25519 signatures required for production**: `flagctl validate` enforces this.
- **No em-dashes** (`—`, U+2014) anywhere - code, docs, commits, or comments.
- **Tests first**: Byrd Development Process requires tests before implementation.
- **Constructor-injected DI with `ILogger<T>`**: enforced by `ArchitectureTests`.

---

## Session Start

1. Read `AGENTS-README-FIRST.yaml` in the repository root for the current MCP marker, endpoints, plugin contract, and authentication details.
2. Codex agents must use `mcpserver-codex-plugin` for session log, TODO, requirements, import/export, and traceability operations.
3. Follow the rendered marker file for session start, turn creation, and persistence cadence.

On every subsequent user message:

1. Start or update the current session-log turn through the required plugin before doing MCP-backed work.
2. Complete the user's request.

## Rules

1. Complete the user's request.
2. Do not fabricate information. If you made a mistake, acknowledge it. Distinguish facts from speculation.
3. Prioritize correctness over speed. Do not ship code you have not verified compiles and is logically sound.
4. Use only `pwsh.exe` for workspace scripts and shell-oriented repository commands. Plugin-owned hooks may use the runner declared in `AGENTS-README-FIRST.yaml`.
5. Do not make raw HTTP calls to MCP endpoints for normal session log, TODO, requirements, import/export, or traceability work when the Codex plugin path is available.
6. Persist session log updates after meaningful changes: turn creation, action append, design decision, requirement, blocker, file/context update, verification result, and commit.
7. Use the Byrd Development Process: small gated slices, validation before expansion, V2 gates, and explicit surfaced questions for unresolved decisions.
8. XML documentation is required on every public API. `CS1591` is suppressed only for Phase 0 scaffolding and must be re-enabled in Phase 1.
9. Every public source file that implements an FR/TR must carry the requirement ID in the XML doc summary once implementation begins.
10. Use constructor-injection DI and typed `ILogger<T>` at every layer. Static loggers, service locators, and consumer-callable injectable constructors are forbidden.
11. Central package management is mandatory through `Directory.Packages.props`; project files must not carry package `Version` attributes.
12. Do not use `OpenFeature.Api.Instance`; OpenFeature types may be consumed, but all wiring is DI-resident.
13. Do not answer the open questions in `docs/Project/Open-Questions-v0.2.md` without Payton's decision.

## Where Things Live

- `AGENTS-README-FIRST.yaml` - runtime MCP marker and plugin contract.
- `docs/Feature-Flag-Ecosystem-Planning-v0.1.md` - normative project specification.
- `docs/Development-Process-draft-v3.md` - Byrd Development Process.
- `docs/Project/` - canonical requirements, mappings, tests, and open questions.
- `docs/context/` - MCP context documents added by later phases.
- `src/` - production projects.
- `tests/` - unit and architecture tests.
- `build/` - build orchestration.

## Build And Test

- `./build.ps1 Compile`
- `./build.ps1 Test`
- `./build.ps1 ValidateConfig`
- `./build.ps1 ValidateTraceability`

The base target framework is .NET 10. SDK-facing libraries multi-target `net10.0`, `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, and `net10.0-windows10.0.19041.0`.

## Requirements Tracking

When a new requirement is discovered:

1. Record it in `docs/Project/`.
2. Include the requirement ID in the session log turn tags.
3. Add or update traceability mappings when implementation begins.

## Design Decision Logging

Log every material decision as:

1. A session-log dialog item with category `decision`.
2. A session-log action with type `design_decision`.
3. A requirements or docs update when the decision changes project contract.

## Glossary

- MCP - Model Context Protocol.
- Marker File - `AGENTS-README-FIRST.yaml`; contains workspace connection details and plugin requirements.
- Session Log - audited record of agent turns, decisions, actions, and verification.
- Byrd Development Process - the planning, implementation, and validation method used by this repository.
