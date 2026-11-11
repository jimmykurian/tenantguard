# Isolation probe

`TenantIsolationProbe<TContext>` in `TenantGuard.Testing` proves, entity by entity, that one
tenant can't read, find, bulk-delete, update, insert over or delete another's rows, or load
them through a navigation, and, when it's given one, that a context with no tenant reads
none of them.
[Testing](../guides/testing.md) shows it in a test; this page says what it does.

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .Seed<Invoice>((tenant, context) => new Invoice { Number = $"{tenant}-1" })
    .RunAsync();

report.AssertAllPassed();
```

## Tenants and contexts

Tenants are strings, at least two, all different. The probe converts each to the tenant
property's type, through a value converter that stores strings or the type's
`TypeConverter`, so a `Guid` tenant is passed as its text. Every context the factory creates
has to use the same database, with the schema in place.

The probe never runs the model rules itself: a model that breaks a rule fails before
anything is seeded.

With a model per tenant ([per-tenant models](options.md#per-tenant-models)), each tenant's
context has a model of its own. The probe plans from the first context's model, and finds
entity types and properties by name in each context.

## Seeding

For every tenant-scoped entity type, the default seeder creates one row per tenant, through
a parameterless constructor or else the constructor EF Core binds for the type, with:

- required scalar properties filled from a deterministic generator (string, numeric, bool,
  `Guid`, `DateTime`, `DateOnly`, `TimeOnly`, enum), keys that aren't generated included. A
  bool is always `false`, so a flag such as `IsDeleted` doesn't put the row behind a
  soft-delete filter;
- required collections empty: an array, list, set or dictionary, such as a primitive
  collection, or a dictionary stored as JSON through a value converter. A required string in
  a `json` or `jsonb` column is `{}`;
- any other required property with a value converter filled through the converter's
  provider type: a generated value, then `{}` and `[]` when the provider type is a string;
- required foreign keys satisfied by seeding the principals first, in topological order over
  the model's required relationships; a shared principal is seeded once;
- required owned references created too, through the constructor EF Core binds when the
  owned type has no parameterless one. A value the constructor or a property initializer
  sets, on the owned type or on its owner, is kept; only the constructor's required arguments and the values left null are
  generated, and an optional constructor parameter gets its default.

When a tenant property is also a foreign key to another entity type, as to a table of
tenants (`Role.TenantId` to `Tenant.Id`), the tenant value is what points at that type's
row. So the probe seeds that type once per tenant, with the key the tenant property points
at set to the tenant, whether the relationship is required or not and whether the type is
tenant-scoped or not. The rest of a composite foreign key that holds the tenant property
points at that row too, even when the relationship is optional. When that key is in turn a
foreign key, as when the table of tenants shares its primary key with another table, the
type it points at is seeded once per tenant the same way. A `Seed<T>` for that type fills its other values; the probe still sets
the key.

`Seed<T>` replaces the default seeder for `T`. The probe always sets the tenant property, and
any required foreign key left unset, even on a row from `Seed<T>`. Seeding runs per tenant
through that tenant's context, one entity type per `SaveChanges`, so the write guard checks
it too.

What the default seeder can't create is skipped, never a silent pass: an entity type in a
cycle of required relationships, one whose required principal can't be seeded, one with no
parameterless constructor that EF Core doesn't create through a constructor either, one
with a required complex property or a required value the generator can't fill, keyless
entity types, and types that share a CLR type. The skip names the property, and the value
converter when the converter threw on every value tried. When the probe can't make an
owner ahead of seeding, as when its constructor needs the tenant, its owned values are
checked as the row is seeded, and one that can't be filled skips the type with the
seeding error instead. When the tenant property points at
a type that can't be seeded, the skip says so:

```text
Needs a seeder: its tenant property TenantId is a foreign key to Tenant, which cannot be seeded: ...
```

A model with no tenant-scoped entity type gets a single skipped `(model)` row.

## Entity types it leaves out

An entity type declared with [`Unfiltered<TEntity>`](options.md#an-unfiltered-entity-type)
isn't probed, and has no rows in the report: it shares rows across tenants on purpose. It
is still seeded when another type needs it as a required principal, with its tenant
property set to that row's tenant. A navigation to it isn't checked by NavigationLeak.

A context declared [`CrossTenant`](options.md#a-cross-tenant-context) has no isolation to
prove, so `RunAsync` throws ([exceptions](exceptions.md#configuration-errors)). So does a
context whose first model is the no-tenant model under `CrossTenantWhenNoTenant`: the
probe's factory gives each context a tenant.

Under [`NullTenantIsHost()`](options.md#host-rows), the probe checks the tenants it is
given. It doesn't seed host rows; with [`WithoutTenant`](#a-context-with-no-tenant), the
host is the context with no tenant, and NoTenantIsolation checks that it reads none of the
tenants' rows.

## A tenant registry

When an entity type's tenant property is its primary key, as in the table of tenants
([a tenant registry](options.md#a-tenant-registry)), each tenant's row has the tenant as
its key. The checks run as for any type, except InsertGuard: a new row stamped A is A's own
row, which is already seeded, so InsertGuard is skipped with a note saying so.

## Checks

Each check runs as tenant B against tenant A's row, with a fresh context per tenant per
check, for every ordered pair of tenants. NoTenantIsolation, which runs only with
`WithoutTenant`, has [a section of its own](#a-context-with-no-tenant).

| Check | Expectation |
| --- | --- |
| ReadIsolation | `Set<E>()` returns none of A's keys. |
| FindIsolation | `FindAsync` with A's key returns null. |
| BulkIsolation | `Set<E>().ExecuteDeleteAsync()` deletes none of A's rows (skipped with a note when the provider doesn't support it). |
| UpdateGuard | Attaching a copy of A's row, changing a scalar and saving throws `TenantIsolationViolationException`, or the app's own `SaveChanges` blocks it (see below). |
| InsertGuard | Adding a new row stamped with A and saving throws, as UpdateGuard, or the app's own `SaveChanges` stamps the row with B (see below). |
| DeleteGuard | Removing A's row (attached) and saving throws, as UpdateGuard. |
| NavigationLeak | `Include` of a navigation on B's row loads none of A's rows (see below). |

Each check gives one row per entity type, failed when any pair failed; NavigationLeak gives
one row per navigation. The read checks run
for every entity type before any check writes. On a relational provider, each check that
writes runs in a transaction that is rolled back, so a leak can't change what a later check
sees. BulkIsolation deletes by A's key, which removes nothing when the filter holds.
ReadIsolation is skipped when tenant B can't read its own row, because then it proves
nothing. For the same reason, FindIsolation and BulkIsolation are skipped when tenant A
can't find its own row with `FindAsync`: another query filter, such as a soft delete, hides
it from every tenant, so not finding it says nothing about the tenant filter. A check that
failed stays failed.

The guard checks also pass when the app's own `SaveChanges` blocks the write before EF Core
saves anything, as Finbuckle's `MultiTenantDbContext` does with its default
`TenantMismatchMode.Throw`: any exception other than a database error (`DbUpdateException`
or `DbException`), when every change the check made is still pending afterwards. The row's
detail names the exception, so a passed row doesn't hide whose check blocked it. A database
error still fails the row, and so does an exception thrown after the save, once EF Core has
accepted the changes. To prove that TenantGuard's own write guard blocks the write, switch the
app's check off in the probe's factory, such as `TenantMismatchMode.Ignore` for Finbuckle.

InsertGuard also passes when the app's own `SaveChanges` stamps the new row with B's tenant,
as Finbuckle's `TenantMismatchMode.Overwrite` does: the row is then B's own, so nothing
crosses tenants, whether the database stores it or refuses it, such as on a unique index
that allows one row per tenant. The row's detail says the app stamped it.

## NavigationLeak

NavigationLeak checks every navigation declared on, or inherited by, a probed entity type
whose target is a tenant-scoped entity type: references and collections, on either end of
the relationship, and skip navigations of a many-to-many relationship. Navigations to an owned
type, or to a type that isn't tenant-scoped, aren't checked: they have no tenant of their
own to leak.

For each navigation and each ordered pair of tenants, the probe links B's row to A's:

| Navigation | The link it seeds |
| --- | --- |
| Toward the principal, such as `OrderLine.Order` | A new dependent stamped B, whose foreign key points at A's principal row |
| Toward dependents, such as `Order.Lines` | A new dependent stamped A, whose foreign key points at B's principal row |
| A skip navigation, such as `Order.Tags` | A join row between B's row and A's target row |

The new dependent is made the way the seeder makes rows, with `Seed<T>` when it's set, and
saved through its own tenant's context, so the write guard lets it through. The navigation's
foreign key is set whether the relationship is required or optional; the dependent's other
required foreign keys point at its own tenant's rows. The link is deleted after the check,
through the context that saved it.

Then, as B, the probe loads B's row with `Include` of the navigation and fails the row when
the navigation holds a row whose tenant isn't B's:

```text
As globex, Include(Lines) on globex's Order (Id: 2) loaded acme's OrderLine (Id: 5).
```

When the relationship's foreign key includes the tenant property, the database can't hold
a link across tenants, so the row passes without seeding one. A skip navigation whose join
entity type is tenant-scoped isn't checked as a skip navigation: the join type's own
navigations are. The row is skipped, naming the reason, when the type of the new dependent
couldn't be seeded or can't be probed, or when saving the link fails. When the foreign key
is unique, as for a one-to-one relationship or a unique index on its columns without a
filter, and the
principal row the link would point at already has a seeded dependent through it, the probe
doesn't try, since a second dependent would break the unique index, and the row is skipped
with

```text
Profile's foreign key (UserId) is unique, and acme's User row (Id: 1) already has a Profile, so a second one to link it across tenants would break the unique index.
```

A unique foreign key whose seeded dependents leave it null, as an optional one-to-one does,
has room for the link, so it's checked as any other.

A row that passed is skipped
too when B can't find its own row with `FindAsync`, or A can't find the row the link
reaches (A's principal, A's new dependent or A's target row), because another query filter
hides it.

The rows come after the entity's six checks, one per navigation, by name, and before its
NoTenantIsolation row:

| Entity | Check |
| --- | --- |
| `Order` | `NavigationLeak(Lines)` |
| `Order` | `NavigationLeak(Tags)` |

NavigationLeak runs after the read checks and before the checks that write.

## A context with no tenant

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .WithoutTenant(() => CreateContext(tenant: null))
    .RunAsync();
```

`WithoutTenant(Func<TContext> createContext)` gives the probe a factory for a context with no
current tenant: the context a background job, a startup task or a request that reached the
database before its tenant was resolved gets. It has to use the same database as the other
contexts. The probe creates a fresh one for each entity type, disposes it, and never writes
through it. Without `WithoutTenant`, the probe runs as before and the report has no
NoTenantIsolation rows. A second call replaces the first factory.

With it, the probe adds one check per probed entity type, once every tenant's row is seeded:

| Check | Expectation |
| --- | --- |
| NoTenantIsolation | As the context with no tenant, `Set<E>()` returns none of the seeded tenants' keys, and `FindAsync` with each tenant's key returns null. |

It catches a filter that opens up when there is no tenant, such as
`CurrentTenantId == null || e.TenantId == CurrentTenantId`, which
[TGM013](rules.md#tgm013-open-without-tenant) reports when the model shows it, also when the
filter is built some other way: through a method the rules can't read, or a tenant accessor
whose default matches every row. A failed row names every row it read:

```text
As no tenant, Set<Invoice>() returned acme's row (Id: 1) and globex's row (Id: 2).
As no tenant, FindAsync(Id: 1) returned acme's row.
```

- A query that throws, as in an app that throws on purpose when there is no tenant,
  returns no rows, so the row passes, and its detail names the exception. An exception from
  the factory itself isn't caught: `RunAsync` throws it.
- `Set<E>()` is checked against the rows their own tenant can read, and `FindAsync` against
  the rows their own tenant can find: a row that another query filter, such as a soft delete,
  hides from its own tenant proves nothing when no tenant doesn't see it either. When no
  tenant's row is left to check, the row is skipped:
  `No tenant could read or find its own Invoice row, so the check proves nothing.`
- A type the probe skips for every check, such as a keyless one, is skipped here too, with
  the same detail.
- Under [`CrossTenantWhenNoTenant`](options.md#the-no-tenant-model-of-a-per-tenant-model),
  when the no-tenant context's model is the cross-tenant one, it reads every tenant's rows on
  purpose, so every NoTenantIsolation row is skipped, with the note
  `The context with no tenant is declared cross-tenant by CrossTenantWhenNoTenant.` A
  no-tenant context whose options declare it `CrossTenant` is skipped the same way, with the
  note naming `CrossTenant`.
- With a model per tenant, the no-tenant context's model is found by entity type name, as
  for each tenant's context.
- The check doesn't load navigations. A context with no tenant has no row of its own to
  `Include` from, and EF Core applies the target's query filter to an `Include`, which the
  target type's own NoTenantIsolation row checks.

NoTenantIsolation runs after the entity type's read checks and before NavigationLeak; its
row is the entity type's last.

## The report

`ProbeReport.Rows` is `(Entity, Check, Outcome, Detail)`, with outcomes `Passed`, `Failed`
and `Skipped`. `AssertAllPassed()` throws `TenantIsolationProbeException`, whose message is
a fixed-width table of the rows that didn't pass ([Exceptions](exceptions.md)).
`ProbeReport.ToString()` returns the same table with every row, passed ones included,
under a summary: `TenantGuard isolation probe: 3 of 12 checks did not pass.`, or
`TenantGuard isolation probe: all 12 checks passed.`, so a test can print the report. A passed
row's detail is empty, except a guard check the app's own `SaveChanges` blocked, which names
its exception, or an insert the app stamped with B. Details name tenants and key values, never other entity values.
