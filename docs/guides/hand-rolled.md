# Hand-rolled query filters

Most multi-tenant EF Core apps isolate tenants by hand: each entity has a `TenantId`, the
context knows the current tenant, and `OnModelCreating` gives each entity a query filter.
TenantGuard checks that setup without changing it. The
[HandRolled.Api sample](../../samples/HandRolled.Api) is this setup, running.

## The app

```csharp
public sealed class Invoice
{
    public int Id { get; set; }

    public required string TenantId { get; set; }

    public required string Number { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    // The tenant this context reads and writes as.
    public string? TenantId => tenant.TenantId;

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
}
```

`ITenantContext` stands for however your app knows the request's tenant, such as a scoped
service that middleware sets from a header, a claim or the host name.

## Turn TenantGuard on

```sh
dotnet add package TenantGuard.EntityFrameworkCore
```

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();
```

- `UseTenantGuard` turns on the model rules and the write guard.
- `CurrentTenant` tells the write guard which tenant a context runs as. It gets the context,
  and returns a value of the tenant property's type, or null when there is no tenant.
- `AddTenantGuardStartupValidation` builds the model when the host starts, so a model that
  breaks a rule stops the app before it serves a request.

TenantGuard finds tenant properties by name, `TenantId` by default, CLR or shadow. If yours
has another name, say so, or mark the property with `[TenantKey]`:

```csharp
.UseTenantGuard(tg => tg
    .TenantPropertyNames("OrgId")                                  // every entity
    .TenantProperty<LegacyInvoice>(invoice => invoice.CompanyCode) // one entity
    .CurrentTenant(context => ((AppDbContext)context).TenantId))
```

`CurrentTenant` is what the write guard checks each row against, so a running app needs it:
without it, every tenant-scoped write throws `InvalidOperationException`, even under
`Enforcement.Log` ([write guard](../reference/write-guard.md#the-current-tenant)).

If the tenant property isn't named `TenantId`, as with `TenantPropertyNames("CompanyId")`,
name it for the analyzers as well, or every raw SQL statement is TG002:

```ini
[*.cs]
tenantguard.tenant_column = CompanyId
```

## What startup catches

`TenantGuardModelException` lists every error the model has. The two that matter most:

- An entity with a `TenantId` and no filter that reads it (TGM001), such as the entity
  added last month whose configuration forgot `HasQueryFilter`.
- A filter frozen when the model was built (TGM003, TGM004). EF Core builds the model once
  per context type and caches it, so a filter that reads a local variable, or compares with
  a constant, gives every tenant the first tenant's rows:

```csharp
// TGM003: tenant is read once, by the first context, and kept in the cached model.
var tenant = TenantId;
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == tenant);

// Correct: TenantId is a member of the context, read again for each query.
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
```

A tenant kept in a field of an `IEntityTypeConfiguration`, or of any object that isn't the
context, is TGM007: a warning, because TenantGuard can't tell whether it is read per query.
Read the tenant from the context instead.

To make every entity type take a side, add `RequireAllEntitiesScoped()`: each one then needs
a tenant property, or a `Shared<T>()` declaring it tenant-free on purpose (TGM002).

```csharp
.UseTenantGuard(tg => tg
    .RequireAllEntitiesScoped()
    .Shared<Country>()
    .CurrentTenant(context => ((AppDbContext)context).TenantId))
```

## Writes

`SaveChanges` throws `TenantIsolationViolationException`, before anything reaches the
database, when it would insert a row stamped with another tenant, update or delete another
tenant's row, or move a row to another tenant. The query filter keeps most of those rows out
of reach; the guard catches the ones that arrive anyway: attached from a request body, read
with `IgnoreQueryFilters()`, or built by hand.

If your app sets `TenantId` itself, when it creates the entity or in a `SaveChanges`
override, the guard checks the value it set. Or let TenantGuard set it: with `StampOnAdd()`,
an added row with no tenant gets the current tenant.

A save with no current tenant, from a seeder or a background job, is rejected by default.
Give it a tenant, or run a job that crosses tenants on purpose in a bypass scope:

```csharp
using (db.TenantGuard().Bypass("nightly archive of paid invoices, every tenant"))
{
    await db.SaveChangesAsync();
}
```

The bypass is logged as a warning with its reason, and covers that context until it is
disposed. `WhenNoCurrentTenant(NoTenantPolicy.Allow)` lets saves with no current tenant
through instead, and logs each one at Information.

## Raw SQL and IgnoreQueryFilters

The analyzers come with the package and run as you build:

- `IgnoreQueryFilters()` is TG001, a warning.
- Raw SQL that doesn't compare `TenantId` with a value, such as `TenantId = @tenantId`, is
  TG002, a warning, whether it goes through EF Core, Dapper or an ADO.NET command, and
  whether it's written inline or in a local first.
- Raw SQL built at runtime, which can't be checked, is TG004, a warning.

Fix the query, or say why it crosses tenants:

```csharp
[AllowCrossTenantAccess("support dashboard: invoice count across tenants")]
public Task<int> CountAcrossTenantsAsync(AppDbContext db)
    => db.Invoices.IgnoreQueryFilters().CountAsync();
```

If your tenant column has another name, tell TG002 in `.editorconfig`:

```ini
[*.cs]
tenantguard.tenant_column = OrgId
```

If every tenant property is a property of its entity class, not a shadow property, say so,
and TG001 skips `IgnoreQueryFilters()` on a type with no tenant column, such as a tenant
registry with only a soft-delete filter. A tenant property named with
`TenantProperty<TEntity>(...)` needs its name in `tenantguard.tenant_column` too;
`[TenantKey]` is seen as it is:

```ini
[*.cs]
tenantguard.tenant_members_only = true
```

If your own data access helpers take SQL, list them so TG002 and TG004 check it too:

```ini
[*.cs]
tenantguard.raw_sql_methods = Acme.Data.Sql.Query, Acme.Data.Sql.Execute
```

## Prove it

A probe test checks, entity by entity, that one tenant can't read, find, bulk-delete,
update, insert over or delete another tenant's rows:

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => new AppDbContext(options, new TenantContext { TenantId = tenant }))
    .WithTenants("acme", "globex")
    .RunAsync();

report.AssertAllPassed();
```

[Testing](testing.md) covers the setup.
