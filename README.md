# TenantGuard

> **Pre-release.** Nothing is on NuGet yet and the API is not stable. Follow the build
> plan in [docs/PLAN.md](docs/PLAN.md).

Multi-tenant EF Core apps isolate tenants with global query filters and a `TenantId`
column. A dozen packages ship that mechanism (Finbuckle.MultiTenant, TenancyKit,
SaasSuite, Tenantry, MultiTenancy.EntityFrameworkCore, and more), and most teams hand-roll
it. None of them ship the proof. One forgotten `HasQueryFilter`, one `IgnoreQueryFilters()`
in a report, one `ExecuteSqlRaw` without a `WHERE TenantId`, or one filter that captured a
local variable at model build, and tenant A reads tenant B.

TenantGuard is the proof. It does not resolve tenants and does not add filters. It takes
the model you already have and:

- fails startup when an entity with a tenant property has no query filter on it, or when
  the filter is frozen to a value;
- rejects a `SaveChanges` that writes, moves or deletes another tenant's row;
- flags `IgnoreQueryFilters()` and raw SQL without a tenant predicate at compile time,
  unless the method carries a justification;
- proves isolation per entity in a test, so a new entity cannot ship unguarded.

In an audit of a SaaS codebase, every entity had a `TenantId` and every reviewer assumed
every entity had a filter. One did not. Nothing in the build or the tests could have said
so.

It works on Finbuckle, on any of the tenancy packages, and on hand-rolled query filters.

## Packages

| Package | Targets | Contents |
|---|---|---|
| `TenantGuard.EntityFrameworkCore` | net8.0, net10.0 | Model validation, SaveChanges guard, bypass scope; bundles the analyzers |
| `TenantGuard.Analyzers` | netstandard2.0 | The analyzers on their own, for projects without EF Core |
| `TenantGuard.Testing` | net8.0, net10.0 | `TenantIsolationProbe`, seeding, `ProbeReport` |

## Install

## Quick start

## Rules

## Analyzers

## Testing

## FAQ

## Building from source

Requires the .NET 10 SDK and [Task](https://taskfile.dev). `task ci` runs everything CI
runs; `task --list` shows the rest.

## License

[Apache-2.0](LICENSE)
