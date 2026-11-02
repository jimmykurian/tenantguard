# Packages

TenantGuard ships three NuGet packages. The runtime package bundles the analyzers, so
installing it turns them on.

| Package | Targets | Contents |
| --- | --- | --- |
| `TenantGuard.EntityFrameworkCore` | net10.0, net11.0 | Options, the model rules, the SaveChanges interceptor, the bypass scope and `AllowCrossTenantAccessAttribute`; bundles the analyzers |
| `TenantGuard.Analyzers` | netstandard2.0 | TG001 to TG004, also shipped on their own for projects without EF Core |
| `TenantGuard.Testing` | net10.0, net11.0 | `TenantIsolationProbe`, seeding and `ProbeReport` |

The net10.0 build references EF Core 10.x and the net11.0 build EF Core 11.x
([0010](decisions/0010-target-net10-and-net11.md)).

## Dependencies

- The runtime package depends on `Microsoft.EntityFrameworkCore` only, and on what EF Core
  already brings in ([0002](decisions/0002-runtime-depends-on-ef-core-only.md)).
- The test kit depends on the runtime package and EF Core, and on no test framework, so it
  works under xUnit, NUnit, MSTest and TUnit alike.
- The analyzers depend on `Microsoft.CodeAnalysis.CSharp` 4.8.0 only, with
  `EnforceExtendedAnalyzerRules`, and know nothing of EF Core or the runtime package
  ([0004](decisions/0004-analyzers-on-netstandard2-and-roslyn-4-8.md)). They recognize
  `[AllowCrossTenantAccess]` by its full name.

The architecture tests in `tests/architecture/` check each of these on every build.

## Namespaces

The runtime package's public types are in `TenantGuard`, the test kit's in
`TenantGuard.Testing`, whatever folder they are in
([0007](decisions/0007-flat-public-namespaces.md)).

## Rings inside the runtime package

The runtime package's folders are rings, innermost first, and a ring depends only on rings
further in ([0019](decisions/0019-runtime-folders-are-rings.md)):

| Ring | Folder | Holds |
| --- | --- | --- |
| 1 | `Options/` | `TenantGuardOptions`, the builder, the enums, `[TenantKey]`, `[AllowCrossTenantAccess]` |
| 2 | `Model/` | The rules, findings and reports, tenant property discovery, operand classification |
| 3 | `Diagnostics/` | Logging |
| 4 | `Interception/` | The write guard, violations, the bypass API, the per-context scope, the interceptor |
| 5 | `Hosting/` | The composition root: `UseTenantGuard`, `AddTenantGuardStartupValidation`, the EF convention plugin and convention, the startup check |

## Public API

The public API is complete for v0.1; anything not listed is `internal`. The
`PublicAPI.Shipped.txt` file of each package is the source of truth, and `task lint:api`
fails when it's out of date.

```csharp
namespace TenantGuard;
public sealed class TenantKeyAttribute : Attribute;
public sealed class AllowCrossTenantAccessAttribute(string reason) : Attribute;
public enum Enforcement { Throw, Log }
public enum NoTenantPolicy { Reject, Allow }
public enum Severity { Warning, Error }
public sealed class TenantGuardOptions;   // opaque; from TenantGuardOptionsBuilder.Options
public sealed class TenantGuardOptionsBuilder { TenantGuardOptionsBuilder(); TenantGuardOptions Options; /* see Options */ }
public static class TenantGuardDbContextOptionsBuilderExtensions { UseTenantGuard(...) }
public static class TenantGuardServiceCollectionExtensions { AddTenantGuardStartupValidation<TContext>() }
public static class TenantGuardDbContextExtensions { TenantGuard(this DbContext) : TenantGuardContext }
public sealed class TenantGuardContext { IDisposable Bypass(string reason); }
public static class TenantGuardModel { ModelReport Validate(IModel, TenantGuardOptions) }
public sealed record ModelFinding(string RuleId, Severity Severity, string Entity, string? Property, string Message);
public sealed class ModelReport { IReadOnlyList<ModelFinding> Findings; Errors; Warnings; }
public sealed class TenantGuardModelException : InvalidOperationException { ModelReport Report; }
public sealed class TenantIsolationViolationException : InvalidOperationException { IReadOnlyList<TenantViolation> Violations; }
public sealed record TenantViolation(string Entity, EntityState State, IReadOnlyDictionary<string, object?> Keys, object? Expected, object? Actual);

namespace TenantGuard.Testing;
public sealed class TenantIsolationProbe<TContext> where TContext : DbContext { ... }
public sealed class ProbeReport { Rows; AssertAllPassed(); }
public sealed record ProbeRow(string Entity, string Check, ProbeOutcome Outcome, string Detail);
public enum ProbeOutcome { Passed, Failed, Skipped }
public sealed class TenantIsolationProbeException : Exception;
```

The builder's methods are in [Options](../reference/options.md). Options are set in code
only; there is no `appsettings` binding.

## Repository layout

```text
TenantGuard.slnx
Directory.Build.props            Version, TFMs, analyzers, SourceLink, warnings as errors
Directory.Packages.props         Central Package Management
global.json                      .NET SDK 11, rollForward latestFeature
src/TenantGuard.EntityFrameworkCore/
src/TenantGuard.Analyzers/
src/TenantGuard.Testing/
tests/unit/                      TenantGuard.EntityFrameworkCore.Tests, TenantGuard.Analyzers.Tests
tests/architecture/              TenantGuard.Architecture.Tests
tests/integration/               TenantGuard.BehaviorPins, TenantGuard.Testing.Tests, TenantGuard.Providers.Tests
tests/e2e/                       HandRolled.Api.Tests, Finbuckle.Api.Tests
samples/HandRolled.Api/          minimal API, its own ITenantContext and filters
samples/Finbuckle.Api/           Finbuckle.MultiTenant, showing TenantGuard is library-agnostic
tools/Metrics/                   the README's code metrics
site/                            the docs site, built from docs/
docs/                            guides/, reference/, architecture/, testing/, deployment/
Taskfile.yml  .ci-parity.yml  .github/workflows/
```
