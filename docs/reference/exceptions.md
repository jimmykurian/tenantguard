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

Tenants are compared with the tenant property's `ValueComparer`. Entries of owned types
are skipped, so a save that changes only the owned part of another tenant's row isn't
blocked yet.

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

When the context's model was built for another tenant, under
[`AllowCapturedValues()`](options.md#per-tenant-models), `Violations` is empty and the
message names both tenants:

```text
TenantGuard blocked a save to AppDbContext: its model was built for tenant "acme", so its query filters read "acme"'s rows, but the current tenant is "globex". Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.
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

These are mistakes in the setup rather than in the data, so they say what to change. The
message of an argument exception ends with the parameter's name, as in
`(Parameter 'tenants')`.

| Thrown by | Exception and message |
|---|---|
| `TenantPropertyNames()` with no names | `ArgumentException`: `Pass at least one tenant property name.` A null or blank name throws `ArgumentNullException` or `ArgumentException`. |
| `TenantProperty<TEntity>(...)`, with anything but a property or field of `TEntity` itself | `ArgumentException`: `Pass a property of {Entity} itself, such as e => e.TenantId.` |
| `Enforcement(...)` or `WhenNoCurrentTenant(...)`, with a value the enum doesn't define | `ArgumentOutOfRangeException`: `Unknown enforcement.` or `Unknown policy.` |
| `SaveChanges` that writes a tenant-scoped row, with no `CurrentTenant` configured | `InvalidOperationException`: `TenantGuard checks the writes of {Context} against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.` |
| `SaveChanges`, when `CurrentTenant` returns another type | `InvalidOperationException`: `CurrentTenant returned a {ReturnedType}, but {Entity}.{Property} is a {PropertyType}. Return a value of the tenant property's type.` |
| `Unfiltered<TEntity>`, `CrossTenant`, `CrossTenantWhenNoTenant`, `Suppress` or `Suppress<TEntity>`, with a null, empty or whitespace reason | `ArgumentNullException` for null, `ArgumentException` otherwise |
| `Suppress` or `Suppress<TEntity>`, with an unknown rule ID | `ArgumentException`: `Unknown rule '{RuleId}'. Pass a rule ID from TGM001 to TGM010.` |
| `Suppress`, with an error rule | `ArgumentException`: `{RuleId} is an error, so it is suppressed one entity type at a time: Suppress<TEntity>("{RuleId}", reason).` |
| `Suppress<TEntity>("TGM001", ...)` | `ArgumentException`: `Declare {Entity} unfiltered with Unfiltered<{Entity}>(reason) rather than suppressing TGM001.` |
| `Suppress<TEntity>("TGM002", ...)` | `ArgumentException`: `Declare {Entity} shared with Shared<{Entity}>() rather than suppressing TGM002.` |
| Building the model, when `Unfiltered<TEntity>` names a type with no tenant property | `InvalidOperationException`: `Unfiltered<{Entity}>() names a type with no tenant property. Declare a tenant-free type with Shared<{Entity}>().` |
| Building the model, when `Unfiltered<TEntity>` names a derived or owned type | `InvalidOperationException`: `Unfiltered<{Entity}>() names a derived or owned type. Declare the root of its hierarchy, or its owner, {Root}.` |
| `RunAsync`, on a context declared `CrossTenant`, or on the no-tenant model under `CrossTenantWhenNoTenant` | `InvalidOperationException`: `{Context} is declared cross-tenant, so there is no tenant isolation to probe.` |
| `context.TenantGuard()`, without `UseTenantGuard` | `InvalidOperationException`: `{Context} does not use TenantGuard. Add UseTenantGuard to its options.` |
| `Bypass(reason)`, with a null, empty or whitespace reason | `ArgumentNullException` for null, `ArgumentException` otherwise |
| Host start, from `AddTenantGuardStartupValidation`, when the context isn't registered | `OptionsValidationException`: `AddTenantGuardStartupValidation<{Context}>() found neither {Context} nor IDbContextFactory<{Context}>. Register the context before the host starts.` |
| Host start, from `AddTenantGuardStartupValidation`, without `UseTenantGuard` | `OptionsValidationException`: `AddTenantGuardStartupValidation<{Context}>() needs {Context} configured with UseTenantGuard; without it there are no rules to run.` |
| `WithTenants`, with fewer than two tenants, a repeat, or a null, empty or whitespace one | `ArgumentException`: `Pass at least two tenants, distinct and not empty.` |
| `RunAsync`, before `WithTenants` | `InvalidOperationException`: `Call WithTenants with at least two tenants before RunAsync.` |
| `RunAsync`, when `Seed<T>` names a type the model doesn't have | `InvalidOperationException`: `Seed<{Type}> names a type that is not an entity type of {Context}.` |
| `RunAsync`, when a tenant doesn't fit a tenant property | `InvalidOperationException`: `The tenant "{Tenant}" is not a {Type}, the type of {Entity}.{Property}. Pass WithTenants values the tenant property can hold.` |
| `RunAsync`, when the write guard blocks the probe's own seeding | `InvalidOperationException`: `The write guard blocked seeding {Entity} as {Tenant}. Does the context factory make {Tenant} the context's current tenant?` |
| `RunAsync`, when the write guard blocks seeding because the context's model was built for another tenant ([per-tenant models](options.md#per-tenant-models)) | `InvalidOperationException`: `The write guard blocked seeding {Entity} as {Tenant}, because the context's model was built for another tenant. Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.` |

A null argument to `UseTenantGuard`, `CurrentTenant`, `TenantGuardModel.Validate`, the
probe's constructor, `WithTenants` or `Seed<T>` throws `ArgumentNullException`.
