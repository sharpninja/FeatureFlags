# CEL Reference

Feature flag rules use a subset of Common Expression Language (CEL). The evaluator is a tree-walking interpreter implemented in `src/SharpNinja.FeatureFlags.Evaluation/RulePredicateValidator.cs`. It is AOT-safe (no `Reflection.Emit`).

Every `when` expression must evaluate to `boolean`. Expressions that error at runtime return `false` (treated as "no match").

## Types

| CEL type | JSON / C# equivalent | Literal syntax |
|---|---|---|
| `bool` | `bool` | `true`, `false` |
| `string` | `string` | `'single-quoted'` or `"double-quoted"` |
| `number` | `double` | `42`, `3.14`, `-1` |
| `list` | `IList<object?>` | `[1, 'a', true]` |
| `map` | `IDictionary<string,object?>` | `{'key': value, 'k2': v2}` |

---

## Operators

### Comparison

| Operator | Meaning | Example |
|---|---|---|
| `==` | Equal | `context.role == 'admin'` |
| `!=` | Not equal | `context.env != 'dev'` |
| `<` | Less than | `context.age < 18` |
| `<=` | Less than or equal | `context.score <= 100` |
| `>` | Greater than | `context.count > 0` |
| `>=` | Greater than or equal | `context.version >= 2` |

### Logical

| Operator | Meaning | Example |
|---|---|---|
| `&&` | Logical AND | `a == 1 && b == 2` |
| `\|\|` | Logical OR | `a == 1 \|\| b == 2` |
| `!` | Logical NOT | `!context.disabled` |

### Arithmetic

| Operator | Meaning | Example |
|---|---|---|
| `+` | Addition / string concat | `x + 1`, `'a' + 'b'` |
| `-` | Subtraction | `x - 1` |
| `*` | Multiplication | `x * 2` |
| `/` | Division | `x / 2` |
| `%` | Modulo | `x % 3` |

### Membership

| Operator | Meaning | Example |
|---|---|---|
| `in` | Element in list or key in map | `'admin' in context.roles` |

### Ternary

```
condition ? value_if_true : value_if_false
```

Example:

```cel
context.score >= 90 ? 'A' : context.score >= 80 ? 'B' : 'C'
```

---

## Member Access

### Dot notation

```cel
context.user.role
context.app.version
```

Traverses nested map/object values. Returns `null` if any intermediate key is absent.

### Index notation

```cel
context["user.role"]
context.tags[0]
myMap["key"]
```

String keys access map entries. Integer indices access list elements. Returns `null` for missing keys or out-of-bounds indices.

---

## Macros

Macros operate on lists. They take a list and a predicate lambda.

**Iteration cap:** 512 elements per macro invocation. Lists exceeding 512 elements cause the macro to return `false` (for predicates) or an empty list (for `filter`/`map`).

### exists

Returns `true` if any element satisfies the predicate.

```cel
context.roles.exists(r, r == 'admin')
[1, 2, 3].exists(x, x > 2)
```

### all

Returns `true` if every element satisfies the predicate.

```cel
context.tags.all(t, t != '')
[2, 4, 6].all(x, x % 2 == 0)
```

### filter

Returns a new list containing only elements that satisfy the predicate.

```cel
context.roles.filter(r, r != 'guest')
[1, 2, 3, 4].filter(x, x > 2)   // [3, 4]
```

### map

Returns a new list by applying a transformation to each element.

```cel
context.tags.map(t, t + '-v2')
[1, 2, 3].map(x, x * 2)   // [2, 4, 6]
```

### exists_one

Returns `true` if exactly one element satisfies the predicate.

```cel
context.roles.exists_one(r, r == 'owner')
```

---

## Map Literals

```cel
{'key': value, 'key2': value2}
```

Map literals create a `map` value inline. Keys must be strings. Values may be any type.

```cel
{'status': 'active', 'tier': context.user.tier}
```

Access entries with dot notation (if key is a valid identifier) or index notation:

```cel
myMap.status
myMap['status']
```

---

## List Literals

```cel
['a', 'b', 'c']
[1, 2, 3]
[]
```

Access elements with zero-based integer index:

```cel
myList[0]
```

---

## Custom Functions

### semver_compare

```cel
semver_compare(version1, version2)
```

Compares two semver strings. Returns `-1`, `0`, or `1` (less, equal, greater).

Aliases: `semver_compare`, `semverCompare`.

```cel
semver_compare(context.app.version, '1.2.0') >= 0
```

### semver_satisfies

```cel
semver_satisfies(version, range)
```

Returns `true` if `version` satisfies the semver range expression.

Aliases: `semver_satisfies`, `semverSatisfies`.

```cel
semver_satisfies(context.app.version, '>=1.0.0 <2.0.0')
```

### bucket

Deterministic percentage bucketing using FNV-1a 64-bit hash.

```cel
// One-argument form: context.ProductId is used as the discriminator
bucket(0.5)

// Two-argument form: explicit discriminator value
bucket(context.user.id, 0.5)
```

Parameters:
- Argument 1 (two-arg form): `string` discriminator value. Typically a user ID, device ID, or session ID.
- Last argument: `number` threshold in range `[0.0, 1.0]`.

Returns `true` when `FNV1a64(discriminator) % 100 < threshold * 100`.

The hash is seeded with the manifest's `productId || releaseId || flagKey` concatenation. The same discriminator always produces the same bucket assignment for a given flag.

Aliases: `bucket`, `Bucket`.

```cel
// 20% rollout by user ID
bucket(context.user.id, 0.2)

// 50% rollout by device ID
bucket(context.device.id, 0.5)
```

---

## EvaluationContext Access

Inside expressions, the evaluation context is accessed via the `context` variable:

```cel
context.user.role
context.tenant.id
context.app.version
context.tags
```

The `context` variable is a map. Keys correspond to `EvaluationContext` attribute names. Nested dot notation is supported for keys that contain dots (the evaluator splits on `.`).

Well-known context keys: see [SDK Reference - EvaluationContext](SDK-Reference.md#evaluationcontext).

---

## Type Rules

- Comparison operators require matching types on both sides. Comparing `string` to `number` returns `false` rather than throwing.
- Arithmetic operators require numeric operands. Non-numeric input returns an error (expression evaluates to `false`).
- `in` requires the right operand to be a list or map.
- Macro predicates must return `bool`.
- `bucket()` threshold must be a number in `[0.0, 1.0]`.

---

## Forbidden Constructs

The following are not supported:

- Regular expression syntax (`matches`, `~=`)
- Lambda expressions outside of macros
- Function calls other than the three custom functions above and the five macros
- Multiple assignment or mutation
- Recursion or self-reference
- `null` literal (use absence-of-key checks instead)
- Comprehension syntax other than the five macros listed above

---

## Operator Precedence (highest to lowest)

1. Postfix: `.member`, `[index]`, function call
2. Unary: `!`, `-` (negation)
3. Multiplicative: `*`, `/`, `%`
4. Additive: `+`, `-`
5. Comparison: `<`, `<=`, `>`, `>=`, `==`, `!=`, `in`
6. Logical AND: `&&`
7. Logical OR: `||`
8. Ternary: `? :`

---

## Example Expressions

```cel
// Simple equality
context.user.role == 'admin'

// Multi-condition with AND
context.user.role == 'admin' && context.env == 'production'

// Membership check on a list
'superuser' in context.user.roles

// Percentage rollout
bucket(context.user.id, 0.1)

// Version gate
semver_satisfies(context.app.version, '>=2.0.0')

// Ternary conditional
context.user.tier == 'enterprise' ? true : bucket(context.user.id, 0.05)

// Macro: any role matches
context.user.roles.exists(r, r == 'admin' || r == 'moderator')

// Macro: all tags non-empty
context.item.tags.all(t, t != '')

// Map literal with computed value
{'enabled': true, 'tier': context.user.tier}.enabled

// Chained comparison via ternary
context.score >= 90 ? 'A' : context.score >= 75 ? 'B' : 'C'
```
