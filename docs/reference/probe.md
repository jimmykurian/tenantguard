# Isolation probe

`TenantIsolationProbe<TContext>` in `TenantGuard.Testing` proves, entity by entity, that one
tenant can't read, find, bulk-delete, update, insert over or delete another's rows, or load
them through a navigation.
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

For every tenant-scoped entity type, the default seeder creates one row per tenant, with:

- required scalar properties filled from a deterministic generator (string, numeric, bool,
  `Guid`, `DateTime`, `DateOnly`, `TimeOnly`, enum), keys that aren't generated included;
- required foreign keys satisfied by seeding the principals first, in topological order over
  the model's required relationships; a shared principal is seeded once;
- required owned references created too.

`Seed<T>` replaces the default seeder for `T`. The probe always sets the tenant property, and
any required foreign key left unset, even on a row from `Seed<T>`. Seeding runs per tenant
through that tenant's context, one entity type per `SaveChanges`, so the write guard checks
it too.

What the default seeder can't create is skipped, never a silent pass: an entity type in a
cycle of required relationships, one whose required principal can't be seeded, one with no
parameterless constructor or a required complex property, keyless entity types, and types
that share a CLR type. A model with no tenant-scoped entity type gets a single skipped
`(model)` row.

## Checks

Each check runs as tenant B against tenant A's row, with a fresh context per tenant per
check, for every ordered pair of tenants:

| Check | Expectation |
| --- | --- |
| ReadIsolation | `Set<E>()` returns none of A's keys. |
| FindIsolation | `FindAsync` with A's key returns null. |
| BulkIsolation | `Set<E>().ExecuteDeleteAsync()` deletes none of A's rows (skipped with a note when the provider doesn't support it). |
| UpdateGuard | Attaching a copy of A's row, changing a scalar and saving throws `TenantIsolationViolationException`. |
| InsertGuard | Adding a new row stamped with A and saving throws. |
| DeleteGuard | Removing A's row (attached) and saving throws. |
| NavigationLeak | `Include` of a navigation on B's row loads none of A's rows (see below). |

Each check gives one row per entity type, failed when any pair failed; NavigationLeak gives
one row per navigation. The read checks run
for every entity type before any check writes. On a relational provider, each check that
writes runs in a transaction that is rolled back, so a leak can't change what a later check
sees. BulkIsolation deletes by A's key, which removes nothing when the filter holds.
ReadIsolation is skipped when tenant B can't read its own row, because then it proves
nothing.

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
couldn't be seeded or can't be probed, or when saving the link fails, as it does for a
one-to-one relationship whose row already has its dependent.

The rows come after the entity's six checks, one per navigation, by name:

| Entity | Check |
| --- | --- |
| `Order` | `NavigationLeak(Lines)` |
| `Order` | `NavigationLeak(Tags)` |

NavigationLeak runs after the read checks and before the checks that write.

## The report

`ProbeReport.Rows` is `(Entity, Check, Outcome, Detail)`, with outcomes `Passed`, `Failed`
and `Skipped`. `AssertAllPassed()` throws `TenantIsolationProbeException`, whose message is
a fixed-width table of the rows that didn't pass ([Exceptions](exceptions.md)). Details name
tenants and key values, never other entity values.
