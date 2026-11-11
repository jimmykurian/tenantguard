# Migrating an existing app

In an app that is already running, TenantGuard reports problems the app has today.
Turned on all at once, it can stop the app at startup or reject saves that used to go
through. This order finds the problems first, in the build, the logs and the tests, then
makes each check binding.

## 1. Install, and read the build

```sh
dotnet add package TenantGuard.EntityFrameworkCore --prerelease
```

The analyzers run from the next build, and each finding is a warning:
`IgnoreQueryFilters()` is TG001, raw SQL that doesn't compare the tenant column with a
value is TG002, raw SQL built at runtime is TG004, and a framework switch such as ABP's
`IDataFilter.Disable<IMultiTenant>()` is TG005. With `TreatWarningsAsErrors`, turn them
down while you work through the list:

```ini
[*.cs]
dotnet_diagnostic.TG001.severity = suggestion
dotnet_diagnostic.TG002.severity = suggestion
dotnet_diagnostic.TG004.severity = suggestion
dotnet_diagnostic.TG005.severity = suggestion
```

Fix each call, or justify it with `[AllowCrossTenantAccess("why")]`, then delete these
lines.

## 2. Model rules in Log mode

```csharp
.UseTenantGuard(tg => tg
    .Enforcement(Enforcement.Log)
    .CurrentTenant(context => ((AppDbContext)context).TenantId))
```

Under `Enforcement.Log`, a model that breaks a rule doesn't stop the app: each error is
logged as a `ModelFinding` event at Error level, in the `TenantGuard` category, when EF Core
builds the model. Deploy, read the log, and fix what it found. A test gets the same list
without a deploy; give the options builder the settings you give `UseTenantGuard`:

```csharp
var report = TenantGuardModel.Validate(context.Model, new TenantGuardOptionsBuilder().Options);

Assert.Empty(report.Errors);
```

The [common findings](#common-findings) below say what each one usually means.

## 3. Writes

The write guard has no log-only mode: once `UseTenantGuard` is on, a save that crosses
tenants throws `TenantIsolationViolationException`. Before that reaches production, run
your test suite with the guard on, and look for two kinds of save:

- Jobs, seeders and admin tools that write across tenants on purpose: wrap them in
  `context.TenantGuard().Bypass("why")`, which logs the reason each time.
- Saves with no current tenant, which are rejected by default: give them a tenant, bypass
  them, or let them through for now with `WhenNoCurrentTenant(NoTenantPolicy.Allow)`,
  which logs a `NoTenantAllowed` event for each one so you can find them later.

## 4. Throw at startup

Once the log is clean, drop `Enforcement(Enforcement.Log)`, since `Throw` is the default,
and validate the model when the host starts:

```csharp
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(connectionString)
    .UseTenantGuard(tg => tg
        .CurrentTenant(context => ((AppDbContext)context).TenantId)));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();
```

A model that breaks a rule now stops the app before it serves a request, with every error
in the exception.

## 5. Prove it in CI

Add a probe test ([testing](testing.md)). A new entity then ships with a working filter or
fails the build. To make every entity type take a side, add `RequireAllEntitiesScoped()`:
each one then needs a tenant property, or `Shared<T>()` for a lookup table.

## Common findings

| Finding | Usual cause | Fix |
|---|---|---|
| TGM001 | An entity has a `TenantId`, but its configuration has no `HasQueryFilter` | Add the filter on the root type of the hierarchy; for a table that's cross-tenant on purpose, such as memberships or an outbox, declare it with [`Unfiltered<T>("why")`](../reference/options.md#an-unfiltered-entity-type), and for a whole context, [`CrossTenant("why")`](../reference/options.md#a-cross-tenant-context) |
| TGM002 | Under `RequireAllEntitiesScoped`, an entity type has no tenant property | Add one, or declare the type with `Shared<T>()`; for the table of tenants, name its key as its tenant property ([a tenant registry](../reference/options.md#a-tenant-registry)) |
| TGM003 | The filter reads a local variable, captured when the model was built | Compare with a member of the context, such as its `TenantId`; for a model per tenant, see [`AllowCapturedValues()`](../reference/options.md#per-tenant-models) |
| TGM004 | The filter compares with a constant, often left over from a test or a seed | Compare with a member of the context |
| TGM005 | The tenant property is nullable | Make it required, unless rows without a tenant are meant; for host data, as in ABP, use [`NullTenantIsHost()`](../reference/options.md#host-rows) |
| TGM006 | A derived type declares the tenant property | Move it to the root type |
| TGM007 | The filter reads a value TenantGuard can't trace, such as a field of an `IEntityTypeConfiguration` | Read the tenant from the context; if the value is read per query already, leave it |
| TGM008 | An owned type has its own tenant property | Remove it, or keep it knowing TenantGuard ignores it: owned rows follow their owner |
| TGM009 | No index or key starts with the tenant property | Add `HasIndex(e => e.TenantId)`, or put the tenant property first in an index you have; for a small table read by key, [`Suppress<T>("TGM009", "why")`](../reference/options.md#suppressing-a-rule) |
| TGM010 | A unique index, such as one on `InvoiceNumber`, or a natural primary key, such as an external login's `(LoginProvider, ProviderKey)`, leaves out the tenant property | Make it unique per tenant: `HasIndex(e => new { e.TenantId, e.InvoiceNumber }).IsUnique()`, or `HasKey(e => new { e.TenantId, e.LoginProvider, e.ProviderKey })`; for a value unique across tenants on purpose, such as a token hash, [`Suppress<T>("TGM010", "why")`](../reference/options.md#suppressing-a-rule) |
| TGM011 | A view or SQL query leaves out the tenant column | Select the tenant column and filter on it; for a lookup view every tenant reads, `Shared<T>()` |
| TGM012 | The filter has a switch, such as `!IsTenantFilterEnabled \|\| ...`, as in ABP | Leave it if the switch is turned on only on purpose, and say so once: [`Suppress("TGM012", "why")`](../reference/options.md#suppressing-a-rule) |
| TGM013 | The filter opens up when there's no tenant: `CurrentTenantId == null \|\| ...` | Compare with the current tenant alone; for an admin context meant to see every tenant, [`Suppress("TGM013", "why")`](../reference/options.md#suppressing-a-rule) |
| TGM014 | A line item or registration has no tenant property and no filter | Filter it through its principal, `HasQueryFilter(e => e.Order.TenantId == TenantId)`, or give it a tenant property and a filter; for a table shared on purpose, `Shared<T>()` |

The [reference](../reference/rules.md) has each rule's message and examples.
