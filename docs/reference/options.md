# Options

## UseTenantGuard

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));
```

`UseTenantGuard` turns on the model rules and the write guard for the context. It extends
both `DbContextOptionsBuilder` and `DbContextOptionsBuilder<TContext>`, and takes an
`Action<TenantGuardOptionsBuilder>`, which is required even if it sets nothing. A second
call builds on the settings of the first.

| Method | Default | What it does |
|---|---|---|
| `TenantPropertyNames(params string[] names)` | `TenantId` | The property names that mark a tenant property by convention, tried in order; CLR and shadow properties both match |
| `TenantProperty<TEntity>(e => e.OrgId)` | none | Names the tenant property of one entity type; wins over `[TenantKey]` and the names |
| `RequireAllEntitiesScoped()` | off | Every entity type must be tenant-scoped or declared shared (TGM002) |
| `Shared<TEntity>()` | none | Declares an entity type tenant-free on purpose, such as a lookup table or a view (TGM002, TGM011, TGM014) |
| `Enforcement(Enforcement)` | `Throw` | What a rule error does: `Throw`, or `Log` and carry on |
| `AllowCapturedValues()` | off | A tenant filter may capture the tenant when the model cache is keyed by tenant (TGM003); see [per-tenant models](#per-tenant-models) |
| `CurrentTenant(Func<DbContext, object?>)` | none | How the write guard finds the context's current tenant; required for it |
| `WhenNoCurrentTenant(NoTenantPolicy)` | `Reject` | When `CurrentTenant` returns null: `Reject` every tenant-scoped write, or `Allow` them and log |
| `StampOnAdd(bool stamp = true)` | off | An added row with no tenant gets the current tenant instead of being rejected |
| `IncludeKeysInErrors(bool include = true)` | on | Each `TenantViolation` carries the row's primary key values |
| `Unfiltered<TEntity>(string reason)` | none | Declares a tenant-scoped entity type unfiltered on purpose; see [cross-tenant data](#cross-tenant-data) |
| `CrossTenant(string reason)` | off | Declares the whole context cross-tenant on purpose: no rules and no write guard |
| `CrossTenantWhenNoTenant(string reason)` | off | Declares the model of a context with no current tenant cross-tenant, in a [per-tenant model](#per-tenant-models) |
| `NullTenantIsHost()` | off | A row with a null tenant belongs to the host, the context with no current tenant |
| `Suppress(string ruleId, string reason)` | none | Leaves a warning rule out for every entity type |
| `Suppress<TEntity>(string ruleId, string reason)` | none | Leaves a rule out for one entity type |

`CurrentTenant` returns a value of the tenant property's type, or null when the context
has no tenant. It runs once per `SaveChanges` that writes a tenant-scoped row, unless a
bypass is open.

`StampOnAdd` treats null and the property's sentinel, its type's default unless set with
`HasSentinel`, as no tenant. It stamps only when there is a current tenant, and only once
the save has no violations, so a blocked save leaves your entities as they were.

The settings that the rules read shape the model: `TenantPropertyNames`, `TenantProperty`,
`RequireAllEntitiesScoped`, `Shared`, `Enforcement`, `AllowCapturedValues`, `Unfiltered`,
`CrossTenant`, `CrossTenantWhenNoTenant`, `NullTenantIsHost` and `Suppress`. EF Core builds
and caches a model, and an internal service provider, for each combination of them.
`CurrentTenant`, `WhenNoCurrentTenant`, `StampOnAdd` and `IncludeKeysInErrors` are read from
each context's own options, so contexts with different `CurrentTenant` lambdas share both.

## Cross-tenant data

Some tenant data is cross-tenant on purpose. Each declaration below takes a reason, which
TenantGuard logs, so the exemption is visible where it is used. A null, empty or whitespace
reason throws: `ArgumentNullException` for null, `ArgumentException` otherwise. A
declaration for a type the model doesn't map is ignored, as `TenantProperty<TEntity>` is,
so contexts can share one configuration.

### An unfiltered entity type

```csharp
.UseTenantGuard(tg => tg
    .Unfiltered<Membership>("maps users to their tenants, read before the tenant is known")
    .Unfiltered<OutboxMessage>("dispatched for every tenant by one worker")
    .CurrentTenant(context => ((AppDbContext)context).TenantId))
```

A membership table, an outbox, or a tenant store's own tables keep their tenant column but
aren't filtered by it. `Unfiltered<TEntity>` keeps the type tenant-scoped, so the rules
about its column and indexes (TGM005, TGM009, TGM010) still apply, and leaves out the rules
about its filter (TGM001, TGM003, TGM004, TGM007, TGM012 and TGM013). The write guard doesn't check its
rows, and the [isolation probe](probe.md#entity-types-it-leaves-out) leaves it out. Each
declaration is logged once per model, at Information level
([logging](logging.md)).

It applies to the root of a hierarchy, where EF Core puts query filters. Naming a type
that the model maps but that has no tenant property, or a derived or owned type, throws
when the model is built ([exceptions](exceptions.md#configuration-errors)): a tenant-free
type is declared with `Shared<TEntity>()`.

### A cross-tenant context

```csharp
services.AddDbContext<ReportingDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg.CrossTenant("reports and data deletion across tenants")));
```

A context that maps the tenant entities without filters, for reports, deletions or admin
work, is cross-tenant on purpose. `CrossTenant` skips every rule for its model, turns the
write guard off for every context of it, and logs the declaration once per model. The
[validate tool](validate-tool.md#output) reports it and exits 0,
`AddTenantGuardStartupValidation` passes, and the probe refuses to run on it. The other
settings are ignored, and `CurrentTenant` isn't needed.

### The no-tenant model of a per-tenant model

```csharp
.UseTenantGuard(tg => tg
    .AllowCapturedValues()
    .CrossTenantWhenNoTenant("background services work across tenants")
    .CurrentTenant(context => ((AppDbContext)context).TenantId))
```

With a [model per tenant](#per-tenant-models), the model for a context with no current
tenant can have no filters on purpose, for background work across tenants.
`CrossTenantWhenNoTenant` makes that model cross-tenant, as `CrossTenant` does, when the
context building it has no current tenant and its `IModelCacheKeyFactory` isn't EF Core's
own `ModelCacheKeyFactory`. With EF Core's factory every tenant would get that model, so
the rules run on it as usual. A model built with a current tenant is checked as before.

The no-tenant model remembers that it was built with no tenant. A save on it with a current
tenant is blocked, as for [a model built for another tenant](write-guard.md#a-model-built-for-another-tenant);
a save with no current tenant isn't checked.

### Host rows

```csharp
.UseTenantGuard(tg => tg
    .NullTenantIsHost()
    .CurrentTenant(context => ((AbpDbContext)context).CurrentTenantId))
```

In some frameworks, ABP among them, a row with a null tenant is host data, which the host
reads and writes: the host is the context with no current tenant. `NullTenantIsHost` says
so. TGM005 isn't reported, and the write guard treats the host as one more tenant, whose
value is null: with no current tenant, a row may be written when its tenant is null, and
`WhenNoCurrentTenant` doesn't apply. A tenant's context still can't write a host row, and
the host still can't write a tenant's row ([write guard](write-guard.md#host-rows)).

### Suppressing a rule

```csharp
.UseTenantGuard(tg => tg
    .Suppress<Country>("TGM009", "a lookup table, read by key")
    .Suppress<RefreshToken>("TGM010", "the token hash is unique across tenants on purpose")
    .Suppress("TGM008", "owned types hold no tenant data in this app"))
```

`Suppress<TEntity>(ruleId, reason)` leaves one rule out for one entity type.
`Suppress(ruleId, reason)` leaves it out for every type, and takes only a warning rule:
an error is suppressed one type at a time, with its reason. TGM001 and TGM002 aren't
suppressed: declare the type with `Unfiltered<TEntity>` or `Shared<TEntity>` instead. A
suppressed finding isn't in `ModelReport.Findings`, `Errors` or `Warnings`, so it
doesn't throw or fail the validate tool; it's in `ModelReport.Suppressed`, and logged at
Debug level with its reason. An unknown rule ID throws
([exceptions](exceptions.md#configuration-errors)).

### A tenant registry

The table of tenants, filtered on its own key, needs no declaration. Name its key as its
tenant property, and it is checked like any tenant-scoped type: TGM001 checks its filter,
and the write guard lets each tenant write only its own row.

```csharp
.UseTenantGuard(tg => tg
    .RequireAllEntitiesScoped()
    .TenantProperty<Club>(club => club.Id))
```

The [probe](probe.md#a-tenant-registry) seeds one row per tenant, with the tenant as its key.

## Per-tenant models

An app can build one model per tenant: an `IModelCacheKeyFactory` puts the tenant in EF
Core's model cache key, and the query filter captures the tenant when the model is built.
TGM003 reports that capture, because with EF Core's own cache key every tenant would get the
first tenant's model. `AllowCapturedValues()` says the model is per tenant on purpose:

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
    .UseTenantGuard(tg => tg
        .AllowCapturedValues()
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));

internal sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => (context.GetType(), ((AppDbContext)context).TenantId, designTime);
}
```

When EF Core builds a model under `AllowCapturedValues()`, TenantGuard checks the context
that is building it, and leaves TGM003 out only when all of these hold:

1. `CurrentTenant` is set, and returns a tenant for that context.
2. The context's `IModelCacheKeyFactory` isn't EF Core's own `ModelCacheKeyFactory`, which
   keys by context type alone.
3. When the factory's key for the context is a tuple, one of its items equals the current
   tenant.

Otherwise TGM003 is reported as without the option. The option changes nothing else: a
captured constant is still TGM004, and the other rules run as before.

A model that passes the check remembers the tenant it was built for. The check can't
create a second tenant's context, so it can't see two keys, but two more checks can:

- The write guard compares that tenant with the current tenant on every save that writes a
  tenant-scoped row, and blocks the save when they differ: the context was handed another
  tenant's model, whose filters read that tenant's rows
  ([write guard](write-guard.md#a-model-built-for-another-tenant)).
- The [isolation probe](probe.md) creates a context per tenant, so with a key that doesn't
  change with the tenant, the second tenant gets the first tenant's model. The write guard
  then blocks the probe's seeding as that tenant, and `RunAsync` throws, saying so
  ([exceptions](exceptions.md)). With a model per tenant, the probe runs as usual.

A bypass turns the write guard's check off with the rest of the guard.

`TenantGuardModel.Validate` has no context to check, so it still reports TGM003 with the
option. The [validate tool](validate-tool.md) creates the context and runs the check.

## Startup validation

```csharp
services.AddTenantGuardStartupValidation<AppDbContext>();
```

EF Core builds a model the first time a context of its type is used. This builds it when
the host starts, through `ValidateOnStart`, so a model that breaks a rule stops the host
before it serves a request. It resolves the context, or an
`IDbContextFactory<AppDbContext>`, in a scope. A context that isn't registered, or doesn't
use `UseTenantGuard`, fails the check with a message that says so.

## TenantGuardModel.Validate

```csharp
var options = new TenantGuardOptionsBuilder().TenantPropertyNames("OrgId").Options;
ModelReport report = TenantGuardModel.Validate(context.Model, options);
```

Runs the rules on any model and returns a `ModelReport`. Its `Findings` lists every
`ModelFinding` (rule ID, severity, entity, property, message), and `Errors` and `Warnings`
split them by severity. `Suppressed` lists the findings that `Suppress` left out. Under
`CrossTenant` it returns an empty report: it has no context, so `CrossTenantWhenNoTenant`
doesn't apply. Apart from null arguments, it never throws, and it logs nothing,
so a test can assert on the findings. Configure a `TenantGuardOptionsBuilder` the way you
would in `UseTenantGuard`, and pass its `Options`.

## The bypass scope

```csharp
using (context.TenantGuard().Bypass("nightly aggregation across tenants"))
{
    await context.SaveChangesAsync();
}
```

`context.TenantGuard()` returns the context's TenantGuard controls; it throws if the
context doesn't use `UseTenantGuard`. `Bypass(reason)` turns off the write guard for that
context instance until the scope is disposed, and logs a warning with the reason. A null,
empty or whitespace reason throws: `ArgumentNullException` for null, `ArgumentException`
otherwise. Query filters stay on. A pooled context goes back to
the pool with the guard on, even if a bypass was never disposed.

## Attributes

- `[TenantKey]` marks the tenant property of an entity type, for one whose name isn't in
  `TenantPropertyNames`.
- `[AllowCrossTenantAccess("why")]` justifies cross-tenant code to the
  [analyzers](analyzers.md#justifying-cross-tenant-code).

## Configuration files

The analyzers read two `.editorconfig` keys; see [analyzers](analyzers.md#configuration).
Everything else is set in code: there is no `appsettings.json` binding.
