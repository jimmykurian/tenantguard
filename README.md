# TenantGuard

> **Preview.** The 0.1.0 previews build their net11.0 target against the EF Core 11
> release candidate, so they are prereleases on NuGet. The stable v0.1.0 follows .NET 11
> and EF Core 11 general availability.

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
| `TenantGuard.EntityFrameworkCore` | net10.0, net11.0 | Model validation, SaveChanges guard, bypass scope; bundles the analyzers |
| `TenantGuard.Analyzers` | netstandard2.0 | The analyzers on their own, for projects without EF Core |
| `TenantGuard.Testing` | net10.0, net11.0 | `TenantIsolationProbe`, seeding, `ProbeReport` |

The net10.0 build runs on EF Core 10, the net11.0 build on EF Core 11. The only
dependency is `Microsoft.EntityFrameworkCore`, so it works with any provider.

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

From then on:

- The host fails to start when the model breaks a rule, with every error in one
  `TenantGuardModelException`.
- `SaveChanges` throws `TenantIsolationViolationException`, before anything reaches the
  database, when it would write, move or delete a row of a tenant other than the
  context's current one.
- The build warns on each `IgnoreQueryFilters()` and each raw SQL statement that doesn't
  mention `TenantId`.

`CurrentTenant` returns the tenant the context runs as, of the tenant property's type. The
[hand-rolled](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/hand-rolled.md)
and [Finbuckle](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/finbuckle.md)
guides show it for each setup.

## Rules

Checked when EF Core builds the model. An entity type is tenant-scoped when it has a
tenant property: one named `TenantId` (or as configured), one marked `[TenantKey]`, or
Finbuckle's.

| ID | Severity | Reported when |
|---|---|---|
| TGM001 | error | A tenant-scoped entity type has no query filter that reads its tenant property |
| TGM002 | error, opt-in | An entity type has no tenant property and isn't declared shared (`RequireAllEntitiesScoped`) |
| TGM003 | error | The tenant filter reads a local value captured when the model was built |
| TGM004 | error | The tenant filter compares the tenant property with a constant |
| TGM005 | warning | The tenant property is nullable |
| TGM006 | error | A derived type declares the tenant property; filters live on the root |
| TGM007 | warning | TenantGuard can't tell where the filter's tenant value comes from |
| TGM008 | warning | An owned type has its own tenant property, which is ignored |

[Rules reference](https://github.com/jimmykurian/tenantguard/blob/main/docs/reference/rules.md)

## Analyzers

| ID | Severity | Reported when |
|---|---|---|
| TG001 | warning | `IgnoreQueryFilters()` switches the tenant filter off |
| TG002 | warning | Raw SQL doesn't mention the tenant column |
| TG003 | error | `[AllowCrossTenantAccess]` has no reason |
| TG004 | info | Raw SQL is built at runtime, so it can't be checked |

Code that crosses tenants on purpose says why, and the analyzers leave it alone:

```csharp
[AllowCrossTenantAccess("billing totals for every tenant, run by the nightly job")]
public Task<decimal> TotalForAllTenantsAsync(AppDbContext db)
    => db.Invoices.IgnoreQueryFilters().SumAsync(invoice => invoice.Total);
```

Two `.editorconfig` keys tune them: `tenantguard.tenant_column` (default `TenantId`) and
`tenantguard.tenant_filter_names`.
[Analyzers reference](https://github.com/jimmykurian/tenantguard/blob/main/docs/reference/analyzers.md)

## Testing

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .RunAsync();

report.AssertAllPassed();
```

For every tenant-scoped entity type, the probe seeds a row per tenant, then checks that
one tenant can't read, find, bulk-delete, update, insert over or delete another's. A
failure lists what leaked:

```text
TenantGuard isolation probe: 3 of 12 checks did not pass.

Entity    Check          Outcome  Detail
--------  -------------  -------  ------
Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). ...
Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. ...
Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). ...
```

It runs under xUnit, NUnit, MSTest and TUnit alike.
[Testing guide](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/testing.md)

## Adopting it in a running app

1. Install, and fix or justify what the analyzers report.
2. Turn on the model rules with `.Enforcement(Enforcement.Log)`: errors are logged, not
   thrown, so nothing stops while you fix them.
3. Run your tests with the write guard on, and wrap the writes that cross tenants on
   purpose in `context.TenantGuard().Bypass("why")`.
4. Drop `Enforcement.Log` (`Throw` is the default) and add
   `AddTenantGuardStartupValidation`.
5. Add a probe test to CI.

[Migration guide](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/migrating.md)

## How it compares

| Tool | What it does | What it leaves to TenantGuard |
|---|---|---|
| Finbuckle.MultiTenant and the other tenancy packages | Resolve the tenant; add the filter and the tenant property | Proving the filter is on every entity and that queries and writes keep to it. TenantGuard runs on top of them. |
| Finbuckle's `[MultiTenant]` and `IsMultiTenant()` | Configure an entity as multi-tenant | Verifying it: they are configuration, not verification |
| EricksonLopez.MultiTenancy.Analyzers | Roslyn rules on that author's `ITenantContext` lifetimes and Dapper helpers | Anything about EF Core query filters |
| ArchUnitNET, NetArchTest | Structural rules on types and dependencies | Anything that needs the EF Core model |

## FAQ

**Does TenantGuard resolve the tenant, or add the filters?** No. It checks the filters you
have, however they got there: written by hand, or added by a tenancy package.

**Does it slow queries down?** No. Queries run as they did. The rules run once, when EF
Core builds the model, and on `SaveChanges` the guard looks only at the tracked entries.

**What about `ExecuteUpdate` and `ExecuteDelete`?** They don't go through `SaveChanges`, so
the write guard doesn't see them. The query filter scopes them, the probe's BulkIsolation
check proves it for each entity, and TG001 flags an `IgnoreQueryFilters()` that would
switch the filter off first.

**And raw SQL?** TG002 and TG004 flag it at build time. `FromSql` on an entity type isn't
flagged: EF Core applies the query filters over it.

**Named query filters?** Supported. A named filter that reads the tenant property counts
for TGM001, and with `tenantguard.tenant_filter_names` set, TG001 lets
`IgnoreQueryFilters(["SoftDelete"])` through while the tenant filter stays on.

**DbContext pooling?** Supported. A context goes back to the pool with the guard on, even
if a bypass scope was never disposed.

**What doesn't it check yet?** Owned entities: the write guard skips their entries, so a
save that changes only the owned part of another tenant's row isn't blocked. The raw SQL
check looks for the column's name in the SQL text; it doesn't parse SQL.

## Docs

- [Reference](https://github.com/jimmykurian/tenantguard/blob/main/docs/reference/README.md):
  rules, analyzers, options, exceptions and logging, with every message
- [Guides](https://github.com/jimmykurian/tenantguard/blob/main/docs/guides/README.md):
  hand-rolled filters, Finbuckle, testing, migrating an existing app
- [Samples](https://github.com/jimmykurian/tenantguard/tree/main/samples): a hand-rolled
  app and a Finbuckle app, each with a probe test
- [Changelog](https://github.com/jimmykurian/tenantguard/blob/main/CHANGELOG.md)

## Building from source

Requires the .NET 11 SDK (11.0.100-rc.3 or later) and [Task](https://taskfile.dev).
`task ci` runs everything CI runs; `task --list` shows the rest.

Commits follow [Conventional Commits](https://conventionalcommits.org).
`git config commit.template .gitmessage` loads a template with the types and scopes.
`task hooks:install` turns on the git hooks in `.githooks/`: pre-commit refuses commits on
`main` and checks the staged files, commit-msg checks the message, and pre-push runs
`task lint` and `task test` before code reaches GitHub.

[CONTRIBUTING.md](https://github.com/jimmykurian/tenantguard/blob/main/CONTRIBUTING.md)
covers the tasks, the tests, the hooks and how to send a change. Report a vulnerability
through [SECURITY.md](https://github.com/jimmykurian/tenantguard/blob/main/SECURITY.md),
not an issue.

## License

[Apache-2.0](https://github.com/jimmykurian/tenantguard/blob/main/LICENSE)
