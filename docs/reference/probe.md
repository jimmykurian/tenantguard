# Isolation probe

`TenantIsolationProbe<TContext>` in `TenantGuard.Testing` proves, entity by entity, that one
tenant can't read, find, bulk-delete, update, insert over or delete another's rows.
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

Each check gives one row per entity type, failed when any pair failed. The read checks run
for every entity type before any check writes. On a relational provider, each check that
writes runs in a transaction that is rolled back, so a leak can't change what a later check
sees. BulkIsolation deletes by A's key, which removes nothing when the filter holds.
ReadIsolation is skipped when tenant B can't read its own row, because then it proves
nothing.

## The report

`ProbeReport.Rows` is `(Entity, Check, Outcome, Detail)`, with outcomes `Passed`, `Failed`
and `Skipped`. `AssertAllPassed()` throws `TenantIsolationProbeException`, whose message is
a fixed-width table of the rows that didn't pass ([Exceptions](exceptions.md)). Details name
tenants and key values, never other entity values.
