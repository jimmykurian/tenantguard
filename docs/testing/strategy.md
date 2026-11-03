# Testing strategy

How TenantGuard tests itself: which tests live where, what they run against, which libraries
they use, and the EF Core behaviors the design relies on. [Testing](../guides/testing.md) is
about testing your app with the probe.

## The test folders

| Folder | Projects | Runs against | Task |
| --- | --- | --- | --- |
| `tests/unit/` | `TenantGuard.EntityFrameworkCore.Tests`, `TenantGuard.Analyzers.Tests`, `TenantGuard.Tool.Tests` | In process: SQLite in memory at most | `task test:unit` |
| `tests/architecture/` | `TenantGuard.Architecture.Tests` | The shipped assemblies, read with NetArchTest | `task test:architecture` |
| `tests/integration/` | `TenantGuard.BehaviorPins`, `TenantGuard.Testing.Tests` | EF Core on SQLite | `task test:integration` |
| `tests/integration/` | `TenantGuard.Providers.Tests` | SQL Server and PostgreSQL in Testcontainers; needs Docker | `task test:providers` |
| `tests/e2e/` | `HandRolled.Api.Tests`, `Finbuckle.Api.Tests` | Each sample app through its host | `task test:e2e` |
| `tests/e2e/` | `ValidateTool.Tests` | The built `dotnet tenantguard validate`, on `ValidateTool.Fixture` (net10.0 and net11.0) and both sample apps | `task test:e2e` |
| `tests/e2e/` | `ProbeAdapters.Tests` | The xUnit v3 and TUnit fixtures, run as processes, with their reports read: each probe row of `ProbeAdapters.Store` is a test | `task test:e2e` |

`task test` runs every group but the provider tests, which `providers.yml` runs on demand,
nightly, and on pull requests that change the runtime or test kit packages, the shared probe
test models, the package versions or those tests; they are never required to merge.

## Conventions

- Every model rule has a positive and a negative model in
  `tests/unit/TenantGuard.EntityFrameworkCore.Tests/Models/`, and its test asserts the exact
  message.
- Analyzer tests run each rule through `CSharpAnalyzerTest` and mark where a diagnostic lands
  with `{|TG001:...|}`.
- The test kit is tested against itself: a deliberately leaky model must make the probe fail
  with the expected rows.
- Tests and samples use the synthetic tenants `acme`, `globex` and `initech`.
- Exceptions and logs never name an entity value other than tenant ids and key values;
  `NoValueLeakTests` and a probe test check it with values Bogus fills.

## Libraries

xUnit v3 runs every test, and they assert with AwesomeAssertions. Where they fit
([0018](../architecture/decisions/0018-test-libraries-beyond-xunit.md)):

- **FsCheck** for a rule that holds for every input;
- **NSubstitute** for a fake of an interface;
- **Verify** for long user-facing output, as a `*.verified.txt` snapshot next to the test;
- **Bogus** for entity values a test checks never reach a message;
- **LightBDD** for the integration tests, as Given/When/Then steps run from `[Fact]`, never
  `[Scenario]`, which xunit.v3 4 silently skips (`task lint:scenarios` catches it);
- **NetArchTest** for the architecture rules.

## Behavior pins

`tests/integration/TenantGuard.BehaviorPins` asserts, against SQLite, the EF Core behaviors
the design assumes, each in one test with the EF Core docs link. If a pin fails on a new EF
Core version, stop and open an issue: the design rests on it.

1. Query filters apply to `FindAsync` when the entity is not already tracked.
2. Query filters apply to `ExecuteDelete` and `ExecuteUpdate`.
3. Query filters compose over `FromSqlRaw` on an entity type, so TG002 leaves `FromSql`,
   `FromSqlRaw` and `FromSqlInterpolated` alone. If this fails for a provider, they join
   TG002.
4. A filter that captures a local in `OnModelCreating` returns the first tenant's rows to a
   second context constructed for another tenant: the bug TGM003 exists for.
5. A filter that references a context field, or a captured null of an interface the context
   implements (Finbuckle's pattern), is evaluated per context instance
   ([0015](../architecture/decisions/0015-tgm003-treats-ef-context-replacement-as-the-context.md)).
6. `IgnoreQueryFilters()` returns other tenants' rows, which is why TG001 matters.
7. A query that reaches a filtered principal through a required navigation joins it with an
   inner join, so dependents of rows the filter hides drop out: a documented caveat.
