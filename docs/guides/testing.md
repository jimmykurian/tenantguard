# Testing

`TenantGuard.Testing` proves isolation in a test. For each tenant-scoped entity type, it
seeds a row per tenant, then checks that one tenant can't read, find, bulk-delete, update,
insert over or delete another's. It works with any test framework: `AssertAllPassed()`
throws when a check didn't pass, and every framework fails a test that throws.

```sh
dotnet add package TenantGuard.Testing --prerelease
```

## The probe

```csharp
var report = await new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .RunAsync();

report.AssertAllPassed();
```

- The factory returns a context whose current tenant is the tenant it is given. The probe
  calls it for each tenant and each check, and disposes what it gets.
- Every context the factory creates has to use the same database, with the schema in
  place. With SQLite in memory, keep one connection open for the whole test.
- Tenants are strings, at least two. The probe converts each to the tenant property's
  type, so `"42"` works for an `int` tenant and a `Guid`'s text for a `Guid` one.

When a check fails, the message is a table of the rows that didn't pass:

```text
TenantGuard isolation probe: 3 of 12 checks did not pass.

Entity    Check          Outcome  Detail
--------  -------------  -------  ------
Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). ...
Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. ...
Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). ...
```

## The checks

For each tenant-scoped entity type, as tenant B, against tenant A's row:

| Check | Passes when |
|---|---|
| ReadIsolation | `Set<E>()` doesn't return the row |
| FindIsolation | `FindAsync` with the row's key returns null |
| BulkIsolation | `ExecuteDeleteAsync` aimed at the row's key deletes nothing |
| UpdateGuard | Saving a change to an attached copy of the row throws `TenantIsolationViolationException` |
| InsertGuard | Saving a new row stamped with A throws |
| DeleteGuard | Saving the removal of the row throws |

Each check runs for every ordered pair of tenants, and the report has one row per entity
type and check, failed if any pair failed. The read checks run before any check writes.
On a relational provider, each check that writes runs in a transaction that is rolled
back, so a leak can't change what a later check sees.

A skipped row fails `AssertAllPassed()` too, because a check that couldn't run proves
nothing. On EF Core's in-memory provider, which has no `ExecuteDelete` and no
transactions, BulkIsolation is always skipped, so use SQLite in memory instead.

## Seeding

The probe seeds one row per tenant for each tenant-scoped entity type. It fills required
properties from a deterministic generator, seeds required principals first, and sets the
tenant property. For a type the generator can't fill, give it a seeder:

```csharp
.Seed<Invoice>((tenant, context) => new Invoice { Number = $"{tenant}-1", TenantId = tenant })
```

The probe still sets the tenant property, and any required foreign key left unset, on a
row from `Seed<T>`. A type it can't seed, such as one with a required property of a type the
generator doesn't know, is reported as skipped with the reason.

To look at the rows yourself, read `report.Rows`: each `ProbeRow` has the `Entity`, the
`Check`, an `Outcome` (`Passed`, `Failed` or `Skipped`) and a `Detail`.

## Model tests

`TenantGuardModel.Validate` runs the model rules on a model and returns what they found,
without throwing or a database. It suits a test that pins the model's shape:

```csharp
var options = new TenantGuardOptionsBuilder().RequireAllEntitiesScoped().Shared<Country>().Options;
var report = TenantGuardModel.Validate(context.Model, options);

Assert.Empty(report.Findings);
```

Build the model from a context without `UseTenantGuard`, or with `Enforcement.Log`:
otherwise a model with errors throws as soon as `context.Model` builds it.

## xUnit, NUnit, MSTest and TUnit

The probe code is the same under each. Put the setup in one place:

```csharp
internal static class Isolation
{
    public static async Task<ProbeReport> ProbeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .UseTenantGuard(tg => tg.CurrentTenant(context => ((AppDbContext)context).TenantId))
            .Options;
        await using (var setup = new AppDbContext(options, new TenantContext()))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
        }

        return await new TenantIsolationProbe<AppDbContext>(tenant => new AppDbContext(options, new TenantContext { TenantId = tenant }))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);
    }
}
```

xUnit v3:

```csharp
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Every_entity_stays_inside_its_tenant()
        => (await Isolation.ProbeAsync(TestContext.Current.CancellationToken)).AssertAllPassed();
}
```

NUnit:

```csharp
public sealed class TenantIsolationTests
{
    [Test]
    public async Task Every_entity_stays_inside_its_tenant()
        => (await Isolation.ProbeAsync()).AssertAllPassed();
}
```

MSTest:

```csharp
[TestClass]
public sealed class TenantIsolationTests
{
    [TestMethod]
    public async Task Every_entity_stays_inside_its_tenant()
        => (await Isolation.ProbeAsync()).AssertAllPassed();
}
```

TUnit:

```csharp
public sealed class TenantIsolationTests
{
    [Test]
    public async Task Every_entity_stays_inside_its_tenant()
        => (await Isolation.ProbeAsync()).AssertAllPassed();
}
```

## On your own database

SQLite catches a missing or frozen filter as well as any database, since the filter is
EF Core's. Your provider adds its own SQL, its `ExecuteDelete` and its transactions; to
cover those, run the probe on it too. This repo runs it on SQL Server and PostgreSQL in
Testcontainers, one database per test (see
[TenantGuard.Providers.Tests](../../tests/TenantGuard.Providers.Tests)):

```csharp
var database = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = $"probe_{Guid.NewGuid():N}" };
var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(database.ConnectionString)
    .UseTenantGuard(tg => tg.CurrentTenant(context => ((AppDbContext)context).TenantId))
    .Options;
```

Create the schema with `EnsureCreatedAsync()` or your migrations before the probe runs.
The databases go away with the container when the test run ends.
