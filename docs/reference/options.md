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
| `Shared<TEntity>()` | none | Declares an entity type tenant-free on purpose, such as a lookup table |
| `Enforcement(Enforcement)` | `Throw` | What a rule error does: `Throw`, or `Log` and carry on |
| `AllowCapturedValues()` | off | A tenant filter may capture the tenant when the model cache is keyed by tenant (TGM003); see [per-tenant models](#per-tenant-models) |
| `CurrentTenant(Func<DbContext, object?>)` | none | How the write guard finds the context's current tenant; required for it |
| `WhenNoCurrentTenant(NoTenantPolicy)` | `Reject` | When `CurrentTenant` returns null: `Reject` every tenant-scoped write, or `Allow` them and log |
| `StampOnAdd(bool stamp = true)` | off | An added row with no tenant gets the current tenant instead of being rejected |
| `IncludeKeysInErrors(bool include = true)` | on | Each `TenantViolation` carries the row's primary key values |

`CurrentTenant` returns a value of the tenant property's type, or null when the context
has no tenant. It runs once per `SaveChanges` that writes a tenant-scoped row, unless a
bypass is open.

`StampOnAdd` treats null and the property's sentinel, its type's default unless set with
`HasSentinel`, as no tenant. It stamps only when there is a current tenant, and only once
the save has no violations, so a blocked save leaves your entities as they were.

The first six settings shape the model, and EF Core builds and caches a model, and an
internal service provider, for each combination. The last four are read from each
context's own options, so contexts with different `CurrentTenant` lambdas share both.

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
split them by severity. Apart from null arguments, it never throws, and it logs nothing,
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
