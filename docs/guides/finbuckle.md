# Finbuckle.MultiTenant

Finbuckle resolves the tenant and, with `IsMultiTenant()`, gives each entity a `TenantId`
property and a query filter. TenantGuard checks that what Finbuckle set up holds, and
catches the entities, queries and writes it doesn't cover. The
[Finbuckle.Api sample](../../samples/Finbuckle.Api) runs the code below on
Finbuckle.MultiTenant 10.

## Turn TenantGuard on

```sh
dotnet add package TenantGuard.EntityFrameworkCore --prerelease
```

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantInfo?.Id)));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();
```

`AppDbContext` implements `IMultiTenantDbContext`, so its `TenantInfo` is the request's
tenant. Finbuckle stores `TenantInfo.Id` in each row's `TenantId`, so that is what
`CurrentTenant` returns.

## What TenantGuard reads

- Every entity type `IsMultiTenant()` or `[MultiTenant]` marks is tenant-scoped, with
  Finbuckle's `TenantId` as its tenant property. TenantGuard finds them through Finbuckle's
  `Finbuckle:MultiTenant` annotation, so there is nothing to configure.
- Finbuckle's filter compares `TenantId` with the context's `TenantInfo.Id`, which EF Core
  reads from the current context for each query. TenantGuard knows that and doesn't report
  it.
- An entity with a `TenantId` that `IsMultiTenant()` missed has no filter: TGM001.
- `IsMultiTenant()` adds no index on `TenantId`: TGM009 warns until you add
  `HasIndex("TenantId")`. A unique index without `TenantId` is TGM010; Finbuckle's
  `AdjustUniqueIndexes()` adds `TenantId` to each unique index, which clears it. So is a
  natural primary key without `TenantId`, such as an external login's
  `(LoginProvider, ProviderKey)`; Finbuckle's `AdjustKey()` adds `TenantId` to a key.

`RequireAllEntitiesScoped()` makes every entity type take a side. An entity marked
`IsNotMultiTenant()`, or never marked, then needs `Shared<T>()` (TGM002):

```csharp
.UseTenantGuard(tg => tg
    .RequireAllEntitiesScoped()
    .Shared<Country>()
    .CurrentTenant(context => ((AppDbContext)context).TenantInfo?.Id))
```

## Writes

Finbuckle's `EnforceMultiTenant()`, which `MultiTenantDbContext` calls in `SaveChanges`,
sets the tenant on added rows and checks changed and deleted ones, as its
`TenantMismatchMode` and `TenantNotSetMode` say. It runs before `base.SaveChanges`, so
before TenantGuard's guard, which then checks the tenant Finbuckle set.

The guard checks every save regardless: one from a context that doesn't call
`EnforceMultiTenant()`, or sets `TenantMismatchMode.Ignore` or `Overwrite`. It compares
the current tenant with the tenant each changed or deleted row had when it was read, so
moving a row to another tenant is rejected too. Without `EnforceMultiTenant()`, an added
row has no tenant and is rejected; `StampOnAdd()` gives it the current tenant instead.

In the isolation probe, Finbuckle's default `TenantMismatchMode.Throw` blocks the guard
checks' writes before TenantGuard sees them. The rows pass and name Finbuckle's exception
([The isolation probe](../reference/probe.md)). To prove TenantGuard's own guard instead, set
`TenantMismatchMode.Ignore` on the contexts the probe's factory creates. With
`TenantMismatchMode.Overwrite`, Finbuckle stamps the probe's new rows with the current
tenant, and the InsertGuard rows pass saying so.

## IgnoreQueryFilters

Finbuckle 10 names its filter `__tenant__` (`Finbuckle.MultiTenant.Abstractions.Constants.TenantToken`),
so you can switch off your other filters by name and leave the tenant filter on. Give TG001
that name, and it reports only the calls that switch the tenant filter off:

```ini
[*.cs]
tenantguard.tenant_filter_names = __tenant__
```

```csharp
var deleted = await db.Invoices.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();                 // passes
var everyTenant = await db.Invoices.IgnoreQueryFilters([Constants.TenantToken]).ToListAsync(); // TG001
```

Without the setting, every `IgnoreQueryFilters` call is TG001.

Leave `tenantguard.tenant_members_only` unset. `IsMultiTenant()` adds `TenantId` as a
shadow property, which the analyzer can't see, so under that key TG001 would skip every
`IsMultiTenant()` entity.

## Prove it

The probe needs a context for a given tenant. Finbuckle's context gets its tenant from
`IMultiTenantContextAccessor`, so a test passes one that returns a fixed tenant:

```csharp
internal sealed class FixedTenant(string? tenant) : IMultiTenantContextAccessor<TenantInfo>
{
    public IMultiTenantContext<TenantInfo> MultiTenantContext { get; } =
        new MultiTenantContext<TenantInfo>(tenant is null ? null! : new TenantInfo { Id = tenant, Identifier = tenant, Name = tenant });

    IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
}
```

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => new AppDbContext(new FixedTenant(tenant), options))
    .WithTenants("acme", "globex")
    .RunAsync();

report.AssertAllPassed();
```

The probe stores each tenant name as the row's `TenantId`, which `CurrentTenant` matches
because `FixedTenant` uses the name as the tenant's `Id`. [Testing](testing.md) covers the
rest of the setup.
