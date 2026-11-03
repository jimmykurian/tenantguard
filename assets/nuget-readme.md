<img src="https://raw.githubusercontent.com/jimmykurian/tenantguard/main/assets/icon.png" alt="TenantGuard logo: a shield divided into separate cells, one per tenant" width="96" height="96">

# TenantGuard

> **Preview.** The 0.1.0 previews build their net11.0 target against the EF Core 11
> release candidate, so they are prereleases. The stable 0.1.0 follows .NET 11 and EF Core
> 11 general availability.

TenantGuard proves an EF Core multi-tenant model leaks nothing. It doesn't resolve tenants
or add filters; it takes the model you already have, on Finbuckle, any tenancy package or
hand-rolled query filters, and:

- fails startup when an entity with a tenant property has no query filter on it, or when
  the filter is frozen to a value;
- rejects a `SaveChanges` that writes, moves or deletes another tenant's row;
- flags `IgnoreQueryFilters()` and raw SQL without a tenant predicate at build time, unless
  the method carries a justification;
- proves isolation per entity in a test, so a new entity can't ship unguarded.

| Package | Contents |
| --- | --- |
| `TenantGuard.EntityFrameworkCore` | Model validation, the SaveChanges guard and the bypass scope; bundles the analyzers |
| `TenantGuard.Analyzers` | The analyzers on their own, for projects without EF Core |
| `TenantGuard.Testing` | `TenantIsolationProbe`, seeding and `ProbeReport` |

## Install

```sh
dotnet add package TenantGuard.EntityFrameworkCore --prerelease
dotnet add package TenantGuard.Testing --prerelease   # in the test project
```

## Quick start

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();
```

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .RunAsync();

report.AssertAllPassed();
```

## Learn more

- [Documentation](https://tenantguard.is-cool.dev/), with search
- [README, with a demo and the architecture](https://github.com/jimmykurian/tenantguard#readme)
- [Rules](https://github.com/jimmykurian/tenantguard/blob/main/docs/reference/rules.md) and [analyzers](https://github.com/jimmykurian/tenantguard/blob/main/docs/reference/analyzers.md), with every message
- [Guides](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/README.md): hand-rolled filters, Finbuckle, testing, migrating an existing app
- [Changelog](https://github.com/jimmykurian/tenantguard/blob/main/CHANGELOG.md)

Apache-2.0.
