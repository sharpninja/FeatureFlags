# Human Validation Evidence v1

This artifact captures the manual validation required by `docs/Project/Testing-Requirements.md` for the v1 feature-flag ecosystem.

## Scope

- Admin-plane workflow usability with at least three operators.
- Rule-readability validation by a non-engineer before publish.
- Kill-switch propagation SLA capture against sample apps.
- Command log placeholders for reproducible evidence.

## Three-Operator Workflow Checklist

| Operator | Role | Product | Environment | Scenario | Result | Evidence link | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Operator 1 | Editor | TruckMate | development | Create flag draft, edit default, add rule, save reason text | Pending | Pending | Pending |
| Operator 2 | Publisher | TruckMate | staging | Review diff, approve promotion, publish manifest | Pending | Pending | Pending |
| Operator 3 | Viewer | DriverMate | custom-defined | Confirm read-only access, audit visibility, and product-scope boundaries | Pending | Pending | Pending |

Checklist:

- [ ] Each operator can sign in through the configured identity provider.
- [ ] Role assignment matches expected Product and Environment scope.
- [ ] Editor can create and update a draft without publish rights.
- [ ] Publisher can review the diff and publish with required approval.
- [ ] Viewer can inspect flags and audit history without write actions.
- [ ] Audit entries record actor, action, Product, Environment, Tenant if present, reason, and timestamp.
- [ ] Failed authorization attempts are visible to the operator and logged for audit review.

## Rule-Readability Checklist

Rule under review:

| Field | Value |
| --- | --- |
| Flag key | Pending |
| Product scope | Pending |
| Environment | Pending |
| Rule text | Pending |
| Default value | Pending |
| Candidate value | Pending |
| Reviewer name | Pending |
| Reviewer role | Non-engineer |
| Review timestamp | Pending |

Checklist:

- [ ] Reviewer can state in plain language when the rule applies.
- [ ] Reviewer can identify the Product and Environment scope.
- [ ] Reviewer can identify the default behavior when the rule does not match.
- [ ] Reviewer can identify whether the flag is killable.
- [ ] Reviewer can identify the expected operational risk of publishing the rule.
- [ ] Reviewer feedback was recorded before publish.

## Kill-Switch SLA Capture Template

| Run | Product | Release | Environment | Platform | Flag key | SLA target | Publish UTC | Client observed UTC | Elapsed | Pass/Fail | Evidence link |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | TruckMate | Pending | development | Android | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| 2 | TruckMate | Pending | staging | Windows | Pending | Pending | Pending | Pending | Pending | Pending | Pending |
| 3 | DriverMate | Pending | custom-defined | Linux | Pending | Pending | Pending | Pending | Pending | Pending | Pending |

Capture requirements:

- [ ] Record the exact manifest version or ETag published by the admin plane.
- [ ] Record the Distribution response status and ETag seen by the client.
- [ ] Record whether normal refresh or forced refresh was used.
- [ ] Record the client diagnostic snapshot before and after the kill switch is observed.
- [ ] Record pass/fail against the documented SLA target.

## Command Log Placeholders

| Timestamp UTC | Operator | Machine | Command | Exit code | Output artifact | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Pending | Pending | Pending | `./build.ps1 Test` | Pending | Pending | Pending |
| Pending | Pending | Pending | `./build.ps1 ValidateConfig` | Pending | Pending | Pending |
| Pending | Pending | Pending | `./build.ps1 ValidateTraceability` | Pending | Pending | Pending |
| Pending | Pending | Pending | `flagctl validate <manifest-path>` | Pending | Pending | Pending |
| Pending | Pending | Pending | `docker compose config` | Pending | Pending | Pending |
| Pending | Pending | Pending | `docker compose up --build` | Pending | Pending | Pending |

## Sign-Off

| Area | Approver | Date | Result | Notes |
| --- | --- | --- | --- | --- |
| Three-operator workflow | Pending | Pending | Pending | Pending |
| Rule readability | Pending | Pending | Pending | Pending |
| Kill-switch SLA | Pending | Pending | Pending | Pending |
| Command log completeness | Pending | Pending | Pending | Pending |
