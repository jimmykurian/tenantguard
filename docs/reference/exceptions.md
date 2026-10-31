# Exceptions

## TenantGuardModelException

`TenantGuardModelException : InvalidOperationException`, thrown when EF Core builds a model
that breaks a rule with error severity, under `Enforcement.Throw`. That happens the first
time a context is used, or when the host starts with `AddTenantGuardStartupValidation`.
`Report` holds every finding; the message lists every error:

```text
TenantGuard found 1 model error:
- TGM001: Customer has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.
```

The [model rules](rules.md) give each message and its fix.

## TenantIsolationViolationException

`TenantIsolationViolationException : InvalidOperationException`, thrown by `SaveChanges`
and `SaveChangesAsync` before anything reaches the database, when a write adds, changes,
moves or deletes a row outside the current tenant:

| State | Blocked when |
|---|---|
| Added | The row's tenant isn't the current tenant (an unset one is stamped under `StampOnAdd`) |
| Modified | The row's original tenant isn't the current tenant, or the update changes it |
| Deleted | The row's original tenant isn't the current tenant |

Tenants are compared with the tenant property's `ValueComparer`. Owned types aren't
checked: their rows belong to their owner.

`Violations` lists every blocked write as a `TenantViolation`:

| Member | What it holds |
|---|---|
| `Entity` | The entity type's display name |
| `State` | `Added`, `Modified` or `Deleted` |
| `Keys` | The primary key values by property name; empty under `IncludeKeysInErrors(false)`, or for a key EF Core hasn't generated yet |
| `Expected` | The current tenant, or null when there is none |
| `Actual` | The tenant on the row: the new value for an insert, the stored or changed value for an update, the stored value for a delete |

The message lists up to ten violations and the total:

```text
TenantGuard blocked 12 writes outside the current tenant "acme":
- Added Invoice (Id: 7): TenantId is "globex"
- Modified Invoice (Id: 3): TenantId changes from "acme" to "globex"
...
- and 2 more
Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).
```

With no current tenant, under `NoTenantPolicy.Reject`:

```text
TenantGuard blocked 1 write because the context has no current tenant:
- Added Invoice (Id: 1): TenantId is "acme"
Set the current tenant before saving, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).
```

## TenantIsolationProbeException

`TenantIsolationProbeException : Exception`, thrown by `ProbeReport.AssertAllPassed()`
when a check failed or was skipped. The message is a table of those rows:

```text
TenantGuard isolation probe: 3 of 12 checks did not pass.

Entity    Check          Outcome  Detail
--------  -------------  -------  ------
Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). ...
Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. ...
Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). ...
```

## Configuration errors

These are mistakes in the setup rather than in the data, so they say what to change.

| Thrown by | Exception and message |
|---|---|
| `SaveChanges`, with no `CurrentTenant` configured | `InvalidOperationException`: `TenantGuard checks the writes of {Context} against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.` |
| `SaveChanges`, when `CurrentTenant` returns another type | `InvalidOperationException`: `CurrentTenant returned a {Type}, but {Entity}.{Property} is a {Type}. Return a value of the tenant property's type.` |
| `context.TenantGuard()`, without `UseTenantGuard` | `InvalidOperationException`: `{Context} does not use TenantGuard. Add UseTenantGuard to its options.` |
| `Bypass(reason)`, with a null, empty or whitespace reason | `ArgumentNullException` or `ArgumentException` |
| Host start, from `AddTenantGuardStartupValidation`, when the context isn't registered | `OptionsValidationException`: `AddTenantGuardStartupValidation<{Context}>() found neither {Context} nor IDbContextFactory<{Context}>. Register the context before the host starts.` |
| Host start, from `AddTenantGuardStartupValidation`, without `UseTenantGuard` | `OptionsValidationException`: `AddTenantGuardStartupValidation<{Context}>() needs {Context} configured with UseTenantGuard; without it there are no rules to run.` |
| `WithTenants`, with fewer than two tenants, a repeat or an empty one | `ArgumentException`: `Pass at least two tenants, distinct and not empty.` |
| `RunAsync`, before `WithTenants` | `InvalidOperationException`: `Call WithTenants with at least two tenants before RunAsync.` |
| `RunAsync`, when `Seed<T>` names a type the model doesn't have | `InvalidOperationException`: `Seed<{Type}> names a type that is not an entity type of {Context}.` |
| `RunAsync`, when a tenant doesn't fit a tenant property | `InvalidOperationException`: `The tenant "{Tenant}" is not a {Type}, the type of {Entity}.{Property}. Pass WithTenants values the tenant property can hold.` |
| `RunAsync`, when the write guard blocks the probe's own seeding | `InvalidOperationException`: `The write guard blocked seeding {Entity} as {Tenant}. Does the context factory make {Tenant} the context's current tenant?` |
