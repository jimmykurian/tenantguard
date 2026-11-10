# Test adapters

`ProbeReport.AssertAllPassed()` fails one test with every row that didn't pass. The test
adapters turn each row of a probe run into a test of its own instead, so the test runner
shows which entity and check failed. There is one package per test framework, so
`TenantGuard.Testing` takes no test framework dependency:

| Package | Test framework | Depends on |
|---|---|---|
| `TenantGuard.Testing.XunitV3` | xUnit v3 | `TenantGuard.Testing`, `xunit.v3.extensibility.core` |
| `TenantGuard.Testing.TUnit` | TUnit | `TenantGuard.Testing`, `TUnit.Core` |

Both have the same shape: a class that runs the probe, and an attribute that turns the
report into test cases. NUnit and MSTest could follow it.

## The probe source

```csharp
public sealed class AppProbe : IProbeSource
{
    public Task<ProbeReport> RunAsync(CancellationToken cancellationToken)
        => new TenantIsolationProbe<AppDbContext>(tenant => TestDatabase.CreateContext(tenant))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);
}
```

`IProbeSource`, in `TenantGuard.Testing`, has one method, `RunAsync`, which returns the
report. The class needs a public parameterless constructor. It sets up whatever the probe
needs, such as the database, the way a test would.

The probe runs once for each source type in a test process, the first time an attribute
asks for its rows. Every test method that uses the same source shares that report.

## xUnit v3

```csharp
public sealed class IsolationTests
{
    [Theory]
    [ProbeRows<AppProbe>]
    public void Tenant_isolation(ProbeRow row) => row.AssertPassed();
}
```

`ProbeRowsAttribute<TSource>` is a `DataAttribute`, where `TSource` is an `IProbeSource`
with a parameterless constructor. It gives the theory one data row for each `ProbeRow`:

- The test is named `{Entity}.{Check}`, such as `Invoice.ReadIsolation`,
  `Order.NavigationLeak(Lines)` or, with `WithoutTenant`, `Invoice.NoTenantIsolation`.
- A skipped row is a skipped test, whose reason is the row's detail.
- The probe runs when xUnit runs the theory, not when it discovers tests, so a test
  explorer shows the rows after the first run. They can't be run one at a time.
- The probe gets xUnit's `TestContext.Current.CancellationToken`.

## TUnit

```csharp
public sealed class IsolationTests
{
    [Test]
    [ProbeRows<AppProbe>]
    public void Tenant_isolation(ProbeRow row) => row.AssertPassed();
}
```

`ProbeRowsAttribute<TSource>` is an `AsyncUntypedDataSourceGeneratorAttribute`, with the
same constraint on `TSource`. It hands TUnit each row as a `TestDataRow<ProbeRow>`, which
carries the test's name and skip reason; the test method still takes a `ProbeRow`. Each row
is a test named `{Entity}.{Check}`, and a skipped row is a skipped test, whose reason is the
row's detail. TUnit creates its tests before it runs
them, also when a test explorer discovers them, so the probe runs then: each row can be
seen and run on its own, and discovery needs the database.

## ProbeRow.AssertPassed

`ProbeRow.AssertPassed()`, in `TenantGuard.Testing`, does nothing for a passed row, and
throws `TenantIsolationProbeException` for a failed or skipped one, with the same table
`AssertAllPassed()` uses, for that row alone:

```text
TenantGuard isolation probe: Invoice ReadIsolation did not pass.

Entity   Check          Outcome  Detail
-------  -------------  -------  ------
Invoice  ReadIsolation  Failed   As acme, Set<Invoice>() returned globex's row (Id: 2). ...
```

The adapters mark skipped rows skipped, so in their tests it only throws for a failed row.

## When the probe can't run

When `RunAsync` throws, such as when the model breaks a rule or the database can't be
reached, there are no rows. xUnit fails the theory with that exception; TUnit fails the
test method's data source with it.
