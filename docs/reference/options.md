# Options

## UseTenantGuard

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));
```

`UseTenantGuard(this DbContextOptionsBuilder, Action<TenantGuardOptionsBuilder>)` turns on
the model rules and the write guard for the context. The callback is required, even if it
sets nothing.

| Method | Default | What it does |
|---|---|---|
| `TenantPropertyNames(params string[] names)` | `TenantId` | The property names that mark a tenant property by convention, tried in order; CLR and shadow properties both match |
| `TenantProperty<TEntity>(e => e.OrgId)` | none | Names the tenant property of one entity type; wins over `[TenantKey]` and the names |
| `RequireAllEntitiesScoped()` | off | Every entity type must be tenant-scoped or declared shared (TGM002) |
| `Shared<TEntity>()` | none | Declares an entity type tenant-free on purpose, such as a lookup table |
| `Enforcement(Enforcement)` | `Throw` | What a rule error does: `Throw`, or `Log` and carry on |
| `CurrentTenant(Func<DbContext, object?>)` | none | How the write guard finds the context's current tenant; required for it |
| `WhenNoCurrentTenant(NoTenantPolicy)` | `Reject` | When `CurrentTenant` returns null: `Reject` every tenant-scoped write, or `Allow` them and log |
| `StampOnAdd(bool stamp = true)` | off | An added row with no tenant gets the current tenant instead of being rejected |
| `IncludeKeysInErrors(bool include = true)` | on | Each `TenantViolation` carries the row's primary key values |

`CurrentTenant` returns a value of the tenant property's type, or null when the context
has no tenant. It runs once per `SaveChanges` that writes a tenant-scoped row.

`StampOnAdd` treats null and the property's sentinel, its type's default unless set with
`HasSentinel`, as no tenant. It sets the tenant only when the save has no violations, so a
blocked save leaves your entities as they were.

The first five settings shape the model, and EF Core builds and caches a model, and an
internal service provider, for each combination. The last four are read from each
context's own options, so contexts with different `CurrentTenant` lambdas share both.

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

Runs the rules on any model and returns a `ModelReport` with every `Finding` (rule ID,
severity, entity, property, message) and its `Errors` and `Warnings`. It never throws and
logs nothing, so a test can assert on the findings. Configure a `TenantGuardOptionsBuilder`
the way you would in `UseTenantGuard`, and pass its `Options`.

## The bypass scope

```csharp
using (context.TenantGuard().Bypass("nightly aggregation across tenants"))
{
    await context.SaveChangesAsync();
}
```

`context.TenantGuard()` returns the context's TenantGuard controls; it throws if the
context doesn't use `UseTenantGuard`. `Bypass(reason)` turns off the write guard for that
context instance until the scope is disposed, and logs a warning with the reason. An empty
reason throws `ArgumentException`. Query filters stay on. A pooled context goes back to
the pool with the guard on, even if a bypass was never disposed.

## Attributes

- `[TenantKey]` marks the tenant property of an entity type, for one whose name isn't in
  `TenantPropertyNames`.
- `[AllowCrossTenantAccess("why")]` justifies cross-tenant code to the
  [analyzers](analyzers.md#justifying-cross-tenant-code).

## Configuration files

The analyzers read two `.editorconfig` keys; see [analyzers](analyzers.md#configuration).
Everything else is set in code: there is no `appsettings.json` binding.
