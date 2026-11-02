# 0018. Test libraries beyond xUnit

- Status: Accepted
- Date: 2026-11-01

## Context

Tests used xUnit v3, `Microsoft.EntityFrameworkCore.Sqlite`,
`Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` and the ASP.NET Core shared framework, so
assertions were xUnit's `Assert.*` and fakes were written by hand. The maintainer asked to
assert with AwesomeAssertions and to evaluate NSubstitute, Bogus, AutoFixture,
AutoFixture.AutoNSubstitute, FsCheck and Verify.MSTest, and to write the integration tests
with LightBDD.

## Decision

All test-only; xUnit stays the test framework:

- **AwesomeAssertions** (Apache-2.0, the community fork of FluentAssertions 7) in every test
  project, through `tests/Directory.Build.props` with a global `using`. `Assert.Throws<T>`
  became `ThrowExactly<T>`, keeping the exact-type check, and `Assert.Equal` on a sequence
  became the ordered `Equal`.
- **FsCheck** property tests where a rule holds for every input: blank tenant property names
  and bypass reasons are rejected, names and reasons are kept as given, and the probe
  converts any tenant string into a string or Guid tenant property, or fails naming both.
- **NSubstitute** for Finbuckle's tenant context accessor in `tests/e2e`. `LogCapture` stays
  hand-written: substituting `ILogger.Log<TState>` with its formatter reads worse.
- **Verify.XunitV3**, not Verify.MSTest, which needs MSTest. It snapshots the probe's
  failure table. Verify charges a maintenance fee and fails the build without a
  declaration: the test kit's test project claims the OpenSource exemption until 2027-11.
- **Bogus** (MIT) for a rule nothing tested before: exceptions and logs name tenant ids and
  key values and no other entity value. `NoValueLeakTests` fills every other value through
  Bogus with fixed seeds, makes each kind of cross-tenant write and checks the exceptions
  and logs; a probe test does the same for the report.
- **LightBDD** (BSD-2-Clause) for the integration tests, as Given/When/Then steps.
  LightBDD.XUnit3 3.12.1 throws a TypeLoadException on xunit.v3 4 (LightBDD#409), and the
  test platform then drops every `[Scenario]` test without reporting it, so scenarios run
  from `[Fact]` through `Runner.RunScenarioAsync`, and `task lint:scenarios` fails on
  `[Scenario]`.
- **NetArchTest.eNhancedEdition** (MIT) in `tests/architecture/`, turning dependency,
  namespace, exception, immutability and interceptor rules into tests.
- **AutoFixture and AutoFixture.AutoNSubstitute are not used:** each test's EF model is the
  point of the test, so anonymous objects have nothing to fill.

## Consequences

- Renew Verify's exemption before 2027-11, or the build fails.
- When LightBDD supports xunit.v3 4, switch to `[Scenario]` and drop `lint:scenarios`.
- [The testing strategy](../../testing/strategy.md) says which library a new test uses.
