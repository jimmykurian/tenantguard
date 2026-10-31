# Build plan

Work one phase at a time. A phase is done when every checkbox is ticked, the acceptance
criteria pass, and `task ci` is green. Then commit, summarize, and stop. Do not start the
next phase until asked. Phase 6 is a backlog, not work.

Repository layout, fixed from Phase 0:

```
TenantGuard.slnx
Directory.Build.props            Version, TFMs, analyzers, SourceLink, warnings-as-errors
Directory.Packages.props         Central Package Management
global.json                      .NET SDK 11, rollForward latestFeature
src/TenantGuard.EntityFrameworkCore/
src/TenantGuard.Analyzers/
src/TenantGuard.Testing/
tests/TenantGuard.EntityFrameworkCore.Tests/
tests/TenantGuard.Analyzers.Tests/
tests/TenantGuard.Testing.Tests/
tests/TenantGuard.BehaviorPins/
samples/HandRolled.Api/          minimal API, own ITenantContext, own filters
samples/Finbuckle.Api/           Finbuckle.MultiTenant, proves library-agnostic
docs/                            SPEC.md, PLAN.md, DECISIONS.md, guides/, reference/
Taskfile.yml  .ci-parity.yml  .github/workflows/{ci,release}.yml
```

## Phase 0: scaffold

- [x] Confirm the name: `TenantGuard` free on NuGet for all three IDs and as a GitHub repo;
      else fall back per SPEC 2 and update every file
- [x] Solution, `Directory.Build.props`, `Directory.Packages.props`, `global.json`,
      `.editorconfig` (C# style plus the analyzer keys from SPEC 6), `.gitignore`
- [x] Three `src/` projects with the TFMs and package metadata from AGENTS.md (authors,
      license expression, readme, icon placeholder, repository URL, SourceLink, symbols)
- [x] Four `tests/` projects (xUnit v3, SQLite, analyzer testing) each with one passing
      smoke test
- [x] `samples/HandRolled.Api` and `samples/Finbuckle.Api` skeletons that build
- [x] `LICENSE` (Apache-2.0), `README.md` with the positioning and the audit paragraph from
      SPEC 1, a "pre-release" notice, and empty headings for Install, Quick start, Rules,
      Analyzers, Testing, FAQ
- [x] No `CLAUDE.md`: Claude Code reads `AGENTS.md` directly
- [x] `Taskfile.yml` with every task in AGENTS.md; `scrub` warns and passes when unset
- [x] `.github/workflows/ci.yml` per SPEC 12 with `jimmykurian/ci-parity@v0`; `.ci-parity.yml`
- [x] `docs/DECISIONS.md` with a short header

Acceptance: `task ci` green locally and in CI. `dotnet pack` produces three packages.

## Phase 1: model validation

- [x] Options model: `TenantGuardOptions`, `TenantGuardOptionsBuilder`, `UseTenantGuard`
      as an `IDbContextOptionsExtension` with `Info` (debug string, cache key fragment)
- [x] Tenant property discovery per SPEC 4.1, including shadow properties and
      `[TenantKey]`; Finbuckle annotation left as a TODO string constant until Phase 4
- [x] Filter inspection: find the filter(s) referencing the tenant property with
      `GetDeclaredQueryFilters()`, which EF Core 10 and 11 both have
- [x] Operand classification and rules TGM001 to TGM008 with the exact messages
- [x] `IModelFinalizedConvention` plugin, `TenantGuardModelException`, `Enforcement`
- [x] `TenantGuardModel.Validate` and `ModelReport`
- [x] `AddTenantGuardStartupValidation<TContext>()` startup check through `ValidateOnStart`
- [x] Tests: one positive and one negative model per rule; strict mode with `Shared<T>()`;
      TPH hierarchy; keyless type; owned type; shadow property; `EF.Property` filter;
      filter through a navigation
- [x] Behavior pins 4 and 5 (SPEC 9), which justify TGM003

Acceptance: a context with one unguarded entity fails at host start with a message that
names the entity and the property. The captured-local pin demonstrates the leak and
TGM003 catches the same model.

## Phase 2: SaveChanges guard

- [x] `TenantGuardSaveChangesInterceptor`, sync and async, registered by `UseTenantGuard`
- [x] Per-model metadata cached as a model annotation; no reflection at save time
- [x] Added / Modified / Deleted checks per SPEC 5, `ValueComparer`-based equality
- [x] `NoTenantPolicy`, `StampOnAdd`
- [x] `TenantIsolationViolationException` with `Violations`, key values, message cap of 10
- [x] `context.TenantGuard().Bypass(reason)` scope with Warning log; empty reason throws
- [x] Logging per SPEC 11 through the application `ILoggerFactory`, `NullLogger` fallback
- [x] Tests: every state and policy; shadow tenant property; owned types skipped;
      cross-tenant move rejected; bypass scope; a context with no tenant-scoped entries
      allocates nothing (a `[Fact]` using `GC.GetAllocatedBytesForCurrentThread`)
- [x] Behavior pins 1, 2, 6

Acceptance: the hand-rolled sample, with a deliberately mis-stamped insert, throws before
the database sees it, and the log carries `ViolationBlocked`.

## Phase 3: analyzers

- [x] Analyzer project, `Microsoft.CodeAnalysis.CSharp` 4.8.0, `EnforceExtendedAnalyzerRules`,
      `IsRoslynComponent`, release tracking files (`AnalyzerReleases.*.md`)
- [x] `.editorconfig` option reading for `tenantguard.tenant_column` and
      `tenantguard.tenant_filter_names`
- [x] TG001 (`IgnoreQueryFilters`, both overloads), TG002, TG003, TG004 with the exact
      messages; attribute matched by full name on the method, then the containing type
- [x] Bundle the analyzer into `TenantGuard.EntityFrameworkCore` under
      `analyzers/dotnet/cs`, and ship `TenantGuard.Analyzers` standalone
- [x] Tests with `CSharpAnalyzerTest`: positive and negative per rule, interpolated SQL,
      SQL in a constant, named filter overload with and without tenant names, attribute on
      class vs method, `Async` variants
- [x] Behavior pin 3 (FromSql composes with filters); if it fails, FromSql joins TG002 and
      SPEC 6 is updated in the same PR

Acceptance: `dotnet build` of a sample with an `IgnoreQueryFilters()` reports TG001 at
the call site; adding `[AllowCrossTenantAccess("report")]` clears it; an empty reason is
TG003.

## Phase 4: test kit and samples

- [x] `TenantIsolationProbe<TContext>`: tenants, context factory, `Seed<T>` overrides
- [x] Default seeder: value generator, required-FK topological seeding, shared principals
      seeded once, `Skipped (needs a seeder)` on cycles or missing seeders
- [x] Six checks per SPEC 7 with fresh contexts; provider capability detection for
      `ExecuteDelete`
- [x] `ProbeReport`, `AssertAllPassed`, fixed-width failure table
- [x] Tests: a guarded model passes every row; a model with one unfiltered entity fails
      `ReadIsolation`, `FindIsolation` and `BulkIsolation` for that entity only; a context
      without the interceptor fails the three `*Guard` rows; a cycle reports `Skipped`
- [x] Behavior pin 7
- [x] `samples/HandRolled.Api` complete: own `ITenantContext` from a header, filters via
      context member, `UseTenantGuard`, startup validation, a probe test project
- [x] `samples/Finbuckle.Api` complete: `IsMultiTenant()`, shadow `TenantId`; confirm the
      Finbuckle annotation name and wire it into discovery (SPEC 4.1 step 3)
- [x] `task samples` builds both and runs their probe tests

Acceptance: both samples pass the probe; removing one `HasQueryFilter` in the hand-rolled
sample fails startup (TGM001), and with `Enforcement.Log` fails the probe instead.

## Phase 5: docs, packaging, v0.1.0

- [x] `PublicAPI.Shipped.txt` populated from Unshipped; `task lint:api` enforced
- [x] README complete: install, quick start (five lines of registration), the rules table,
      the analyzer table, the probe snippet, adoption path (`Enforcement.Log` first,
      then `Throw`), comparison table from SPEC 1, FAQ
- [x] `docs/reference/` (rules, analyzers, options, exceptions, logging) and
      `docs/guides/` (hand-rolled, Finbuckle, testing with xUnit/NUnit/MSTest/TUnit,
      migrating an existing app)
- [x] `release.yml` per SPEC 12: release-please, `task pack`, NuGet Trusted Publishing,
      assets on the GitHub Release; `CHANGELOG.md` seeded
- [x] Opt-in Testcontainers job for SQL Server and PostgreSQL running the probe tests
- [x] Package icon, `PackageReadmeFile`, tags, `PackageProjectUrl`; validate with
      `dotnet-validate` or `meziantou.framework.nuget-packages-validation`
- [ ] Tag `v0.1.0` and confirm the three packages install into a fresh console app

Acceptance: a fresh project adds `TenantGuard.EntityFrameworkCore` from nuget.org, gets
TG001 on an `IgnoreQueryFilters()` with no further setup, and fails startup on an
unguarded entity after `UseTenantGuard` plus `AddTenantGuardStartupValidation`.

## Phase 6: backlog (do not start)

- `dotnet tenantguard validate` CLI: load a compiled context from an assembly and print
  the `ModelReport`, for CI without a database
- NavigationLeak probe check: `Include` across tenant-scoped navigations
- TGM009: no index starting with the tenant column on a tenant-scoped entity
- Dapper and ADO.NET raw SQL in TG002 (configurable method list)
- Per-tenant model support: `AllowCapturedValues` when an `IModelCacheKeyFactory` keys by
  tenant
- xUnit and TUnit adapters that turn `ProbeReport` rows into individual test results
- Source generator that emits `[TenantKey]` discovery for AOT scenarios
