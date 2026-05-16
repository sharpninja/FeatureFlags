# CEL Rules Reference

Each flag rule carries a `when` field that contains a **CEL expression**. CEL (Common Expression Language) is a formally specified, sandboxed expression language originally designed for Google's policy and admission-control systems (Kubernetes admission webhooks, Envoy, Cloud Armor, Cerbos). The SharpNinja FeatureFlags SDK implements a supported subset of CEL tailored for flag rule evaluation.

---

## Why CEL

CEL was chosen over alternatives such as JSONLogic for the following reasons, documented in the planning artifact:

- **Formal grammar** - CEL has a complete published grammar. The parser produces a well-defined AST with deterministic precedence rules.
- **Deterministic evaluation** - Given the same input, CEL always produces the same output. There is no implicit type coercion that can silently change outcomes between versions.
- **Sandbox safety** - CEL expressions cannot perform I/O, allocate unbounded memory, or call arbitrary functions. The supported function set is closed and declared explicitly.
- **AOT compatible** - The evaluator is a tree-walking interpreter over parsed AST nodes. It uses no `Reflection.Emit`, no dynamic code generation, and no runtime expression compilation. It is fully compatible with iOS AoT compilation and NativeAOT.
- **Sub-millisecond compile-and-cache** - Each expression is parsed once and cached by text. Subsequent evaluations skip parsing entirely.
- **Proven scale** - CEL is production-proven in Kubernetes, Envoy, and Cloud Armor.

---

## Supported Types

| CEL type | JSON manifest source | Examples |
|---|---|---|
| `boolean` | `true`, `false` | `true`, `false` |
| `string` | Quoted string literal | `'us'`, `"alpha"` |
| `number` | Integer or decimal literal | `42`, `3.14`, `-1` |
| `list` | List literal or context value | `[1, 2, 3]`, `user.roles` |
| `map` | Map literal or context value | `{"key": value}`, `context.metadata` |
| `null` | `null` literal | `null` |

All arithmetic uses decimal arithmetic internally. There is no distinction between integer and floating-point at the expression level; both JSON `integer` and `number` flag values map to the CEL `number` type.

---

## Operators

### Comparison

| Operator | Description | Example |
|---|---|---|
| `==` | Equal | `user.region == 'us'` |
| `!=` | Not equal | `tier != 'free'` |
| `<` | Less than | `score < 50` |
| `<=` | Less than or equal | `score <= 100` |
| `>` | Greater than | `version > 5` |
| `>=` | Greater than or equal | `score >= 80` |

String comparison uses ordinal byte-order comparison. Number comparison uses decimal arithmetic. Comparing a string to a number is a type error.

### Logical

| Operator | Description | Example |
|---|---|---|
| `&&` | Logical AND | `user.region == 'us' && score >= 50` |
| `\|\|` | Logical OR | `tier == 'gold' \|\| tier == 'platinum'` |
| `!` | Logical NOT | `!feature.disabled` |

`&&` and `||` require both operands to be boolean. `!` requires a boolean operand.

### Arithmetic

| Operator | Description | Example |
|---|---|---|
| `+` | Addition or string concatenation | `score + 5`, `user.first + '-' + user.last` |
| `-` | Subtraction or numeric negation | `total - discount`, `-1` |
| `*` | Multiplication | `quantity * price` |
| `/` | Division | `total / count` |
| `%` | Modulo | `index % 2` |

Division and modulo by zero throw an evaluation error. `+` concatenates strings when either operand is a string.

### Membership

| Operator | Description | Example |
|---|---|---|
| `in` | Tests whether a value appears in a list | `user.role in ['driver', 'dispatcher']` |

The right-hand side of `in` must be a list or a context value that holds a list. Strings are not enumerable and cannot be used as the right-hand side of `in`.

### Ternary

```
condition ? thenValue : elseValue
```

The condition must be a boolean expression. The then and else branches may return any type, but both branches should return the same type when used inside a boolean predicate. Ternary is right-associative.

```
score >= 50 ? 'high' : 'low'
(score >= 80 ? (tier == 'gold' ? true : false) : false) == true
```

---

## Member Access

### Dot notation

Access a field of a context value or nested map using `.`:

```
user.region
user.role
context.metadata.tenantId
```

If `user` is set in the evaluation context as a dictionary, `user.region` reads the value at key `"region"`.

### Index notation

Access a map or list value by key or index using `[...]`:

```
user["region"]
tags[0]
{"min": 1, "max": 10}["min"]
```

String keys look up map entries. Integer indexes access list elements (zero-based). Out-of-range integer indexes throw an evaluation error.

---

## Literals

### Map literals

```
{"key": value, "other": value2}
```

Map keys must be string expressions. Values may be any type.

```
{"region": "us", "tier": "gold"}["region"] == "us"
[1, 2, 3].filter(n, {"min": 1, "max": 2}["min"] <= n) .exists(n, n == 1)
```

### List literals

```
[value1, value2, value3]
```

Elements may be of any type. Lists are the operand for macros and the right-hand side of `in`.

```
["driver", "admin"]
[1, 2, 3]
```

---

## Built-in Macros

Macros operate on lists. They use the syntax `list.macroName(variable, expression)` where `variable` is a locally-scoped loop variable bound to each element.

Macros are limited to 512 iterations per evaluation. Exceeding this limit throws an evaluation error.

### `exists`

Returns `true` if the predicate is true for at least one element.

```
user.roles.exists(role, role == 'admin')
tags.exists(t, t == 'beta-tester')
```

### `all`

Returns `true` if the predicate is true for every element. Returns `true` for an empty list.

```
permissions.all(p, p != 'root')
```

### `filter`

Returns a new list containing only elements for which the predicate is true.

```
scores.filter(s, s > 50)
tags.filter(t, t != 'internal')
```

`filter` returns a list and is typically chained with another macro:

```
scores.filter(s, s > 50).exists(s, s > 80)
```

### `map`

Returns a new list by applying the expression to each element.

```
tags.map(t, t)
roles.map(r, r == 'admin')
```

`map` returns a list. It is typically chained with another macro.

### `exists_one`

Returns `true` if the predicate is true for exactly one element.

```
assignments.exists_one(a, a.primary == true)
```

---

## Custom Functions

The following functions are defined by the SharpNinja FeatureFlags SDK. No other function names are accepted; calling an unknown function is a syntax error.

### `semver_compare(a, b)`

Compares two semantic version strings. Returns a negative integer if `a < b`, zero if `a == b`, and a positive integer if `a > b`. Both arguments must be non-empty strings parseable as semantic versions (e.g. `"2.3.4"`, `"v1.0.0-beta.1"`).

```
semver_compare(SemanticVersion, '2.3.0') >= 0
semver_compare(SemanticVersion, '3.0.0') < 0
```

Aliases accepted: `version_compare`, `semverCompare`.

### `semver_satisfies(version, constraint)`

Returns `true` if `version` satisfies the given `constraint`. The constraint is a comparison operator followed by a version string. Supported operators: `>=`, `<=`, `==`, `!=`, `>`, `<`. A bare version without an operator defaults to `==`.

```
semver_satisfies(SemanticVersion, '>=2.3.0')
semver_satisfies(SemanticVersion, '<3.0.0')
semver_satisfies(SemanticVersion, '!=2.0.0')
```

Alias accepted: `semverSatisfies`.

### `bucket(discriminator, threshold)`

Assigns the current evaluation to a deterministic numeric bucket between 0 and 100 (exclusive) and returns `true` if the bucket value is less than `threshold`.

The bucket is computed by hashing the concatenation of `ProductId`, `ReleaseId`, `FlagKey`, and the string representation of `discriminator` using FNV-1a 64-bit. Because all four inputs are fixed for a given (flag, user), the assignment is stable across repeated evaluations. See [ADR-001](../adr/ADR-001-fnv1a-bucketing-hash.md) for the hash algorithm decision record.

`threshold` must be between 0 and 100 inclusive. A threshold of `0` means no one is included; `100` means everyone is included.

```
bucket(UserId, 10)     -- 10% rollout keyed on UserId
bucket(TenantId, 50)   -- 50% rollout keyed on TenantId
bucket(UserId, 100)    -- all users
```

Calling `bucket(discriminator)` with one argument returns the raw numeric bucket value (a decimal between 0 and 100). This is useful for debugging or for constructing range-based rules:

```
bucket(UserId) >= 25 && bucket(UserId) < 75
```

Aliases accepted: `percentage`, `percentage_bucket`, `percentageBucket`.

---

## Evaluation Context Variables

Context variables are supplied by the calling application using `EvaluationContext.Builder()`. Any string key may be used. The following well-known keys are recognized by the SDK and resolved automatically.

| Key | Source | Type | Description |
|---|---|---|---|
| `ProductId` | Manifest + build | string | The product identifier from the manifest. Also available as `productId`. |
| `ReleaseId` | Manifest + build | string | The release identifier from the manifest. Also available as `releaseId`. |
| `FlagKey` | Runtime | string | The key of the flag currently being evaluated. Also available as `flagKey`. |
| `SemanticVersion` | Application | string | The semantic version of the running build. Used with `semver_compare` and `semver_satisfies`. |
| `ReleaseChannel` | Application | string | The release channel (e.g. `canary`, `beta`, `stable`). |
| `ReleaseBuild` | Application | string | The build identifier from CI. |
| `Environment` | Application | string | The deployment environment name. |
| `TenantId` | Application | string | The tenant identifier for multi-tenant deployments. |

Any additional key-value pair set via `EvaluationContext.Builder().Set(name, value)` is available in rules by name. Nested context values (e.g. a `user` object containing a dictionary) are accessible via dot notation or index notation.

```csharp
EvaluationContext context = EvaluationContext.Builder()
    .Set("user", new Dictionary<string, object?> {
        ["region"] = "us",
        ["role"]   = "dispatcher",
        ["roles"]  = new[] { "driver", "admin" },
    })
    .Set("SemanticVersion", "2.4.1")
    .Set("TenantId", "acme-corp")
    .Build();
```

---

## Type Safety Rules

The validator enforces type consistency at manifest validation time:

- The `when` expression must evaluate to `boolean`. Expressions that statically infer to a non-boolean type (e.g. a bare string literal `'not-a-boolean'`) are rejected with error code `FFMANIFEST_RULE_WHEN_TYPE`.
- The rule `value` must match the flag's declared `type`. A flag of type `boolean` cannot have a rule value of `"yes"`.
- Logical operators (`&&`, `||`, `!`) require boolean operands.
- Arithmetic operators (`-`, `*`, `/`, `%`) require numeric operands.
- The `+` operator is valid for number + number (addition) or any combination involving a string (concatenation).
- Comparison operators (`<`, `<=`, `>`, `>=`) require both operands to be the same type (both numeric or both string).
- Macro predicates (`exists`, `all`, `exists_one`) must evaluate to boolean.
- Map literal keys must be string expressions.

When a sub-expression's type cannot be determined statically (because it reads from a context variable), the validator defers the check to runtime.

---

## Forbidden Constructs

The following are not supported in v1 and are rejected at validation time:

- **User-defined functions** - Only `semver_compare`, `semver_satisfies`, and `bucket` are recognized. Any other function call is a syntax error.
- **I/O or side effects** - CEL expressions are pure functions over their inputs. There is no file access, network access, or mutable state.
- **String enumeration** - Strings cannot be used on the right-hand side of `in` or as the target of a macro. Only lists are enumerable.
- **Division or modulo by zero** - These throw an evaluation error at runtime.
- **Index out of bounds** - Accessing a list element beyond its length throws an evaluation error.

---

## Example Rule Expressions

### Simple equality check

```json
{ "when": "user.region == 'us'", "value": false }
```

### Multi-condition AND

```json
{
  "when": "user.region == 'us' && tier == 'enterprise'",
  "value": true
}
```

### Role membership check

```json
{ "when": "user.role in ['driver', 'dispatcher']", "value": true }
```

### List membership macro (user has a specific role)

```json
{ "when": "user.roles.exists(r, r == 'admin')", "value": true }
```

### Percentage rollout (50% of users by UserId)

```json
{ "when": "bucket(UserId, 50)", "value": "on" }
```

### Semantic version gate (enable for builds >= 2.3.0)

```json
{ "when": "semver_satisfies(SemanticVersion, '>=2.3.0')", "value": true }
```

### Semantic version comparison returning an integer

```json
{ "when": "semver_compare(SemanticVersion, '2.3.0') >= 0", "value": true }
```

### Platform targeting by environment

```json
{ "when": "Environment == 'Production'", "value": false }
```

### Tenant targeting

```json
{ "when": "TenantId == 'acme-corp'", "value": "custom-theme" }
```

### Ternary for conditional string selection (used inside a boolean comparison)

```json
{ "when": "(score >= 50 ? 'high' : 'low') == 'high'", "value": "high" }
```

### Combined semver + role + score rule

```json
{
  "when": "user.region == 'us' && score + 5 >= 15 && user.role in ['driver', 'dispatcher'] && user.roles.exists(role, role == 'admin') && semver_satisfies(SemanticVersion, '>=2.3.0')",
  "value": true
}
```

### Filter and exists chain

```json
{ "when": "scores.filter(s, s > 50).exists(s, s > 80)", "value": true }
```

### Map literal index access

```json
{ "when": "{\"a\": 1, \"b\": 2}[\"a\"] == 1", "value": true }
```
