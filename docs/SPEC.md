# TenantGuard v0.1 specification

## 1. Purpose and positioning

Multi-tenant EF Core apps isolate tenants with global query filters and a `TenantId`
column. A dozen packages ship that mechanism (Finbuckle.MultiTenant, TenancyKit,
SaasSuite, Tenantry, MultiTenancy.EntityFrameworkCore, and more), and most teams hand-roll
it. None of them ship the proof. One forgotten `HasQueryFilter`, one `IgnoreQueryFilters()`
in a report, one `ExecuteSqlRaw` without a `WHERE TenantId`, or one filter that captured a
local variable at model build, and tenant A reads tenant B.

TenantGuard is the proof. It does not resolve tenants and does not add filters. It takes
the model you already have and:

- fails startup when an entity with a tenant property has no query filter on it, or when
  the filter is frozen to a value (SPEC 4);
- rejects a `SaveChanges` that writes, moves or deletes another tenant's row (SPEC 5);
- flags `IgnoreQueryFilters()` and raw SQL without a tenant predicate at compile time,
  unless the method carries a justification (SPEC 6);
- proves isolation per entity in a test, so a new entity cannot ship unguarded (SPEC 7).

The README opens with this, then with the audit story in one paragraph: in an audit of a
SaaS codebase, every entity had a `TenantId` and every reviewer assumed every entity had
a filter. One did not. Nothing in the build or the tests could have said so.

Closest tools, for the README's comparison table:

- Finbuckle.MultiTenant and the tenancy packages: resolve the tenant, add the filter and
  the shadow property. TenantGuard runs on top of them.
- EricksonLopez.MultiTenancy.Analyzers: Roslyn rules about that author's `ITenantContext`
  lifetimes and Dapper helpers. Nothing about EF Core filters.
- Finbuckle's `[MultiTenant]` + `IsMultiTenant()`: configuration, not verification.
- ArchUnitNET / NetArchTest: structural rules on types, no knowledge of the EF model.

## 2. Packages

| Package | TFMs | Contents |
|---|---|---|
| `TenantGuard.EntityFrameworkCore` | net10.0, net11.0 | options, model rules, interceptor, bypass scope, `AllowCrossTenantAccessAttribute`; bundles the analyzer |
| `TenantGuard.Analyzers` | netstandard2.0 | TG001 to TG004 (also shipped standalone for projects without EF) |
| `TenantGuard.Testing` | net10.0, net11.0 | `TenantIsolationProbe`, seeding, `ProbeReport` |

Root namespace `TenantGuard`. Name availability (NuGet IDs, GitHub repo) is confirmed in
Phase 0; fallbacks in order: `TenantProof`, `EFCore.TenantGuard`.

## 3. Vocabulary

- Tenant property: the property that holds the tenant key on an entity. CLR or shadow.
  Found by convention (SPEC 4.1).
- Tenant-scoped entity: an entity type whose root type has a tenant property.
- Shared entity: an entity type declared tenant-free on purpose (lookup tables).
- Current tenant: the value the app says the context is operating as, obtained from the
  `CurrentTenant` resolver.
- Guard: the interceptor plus the model rules together.

## 4. Model validation

### 4.1 Discovering tenant-scoped entities

For each entity type in the model, the tenant property is the first match of:

1. An explicit registration: `tg.TenantProperty<TEntity>(e => e.OrgId)`.
2. A property carrying `[TenantKey]` (attribute shipped in the runtime package).
3. Finbuckle's multi-tenant annotation on the entity type, `Finbuckle:MultiTenant`
   (Finbuckle's `Constants.MultiTenantAnnotationName`; matched by string, no package
   reference; `samples/Finbuckle.Api.Tests` checks it against the sample's Finbuckle
   version), with Finbuckle's `TenantId` property.
4. A property (CLR or shadow) whose name is in `TenantPropertyNames`
   (default `["TenantId"]`).

Only the root type of an inheritance hierarchy is examined, because EF allows filters
only on the root. Owned types are skipped (SPEC 4.3, TGM008). Keyless entity types are
examined like any other.

### 4.2 Rules

Each rule has an ID, a severity, and a normative message. Errors throw
`TenantGuardModelException` (listing every error, not just the first) when
`Enforcement` is `Throw`, and are logged at Error level when it is `Log`. Warnings are
always logged.

| ID | Severity | Trigger | Message |
|---|---|---|---|
| TGM001 unguarded-entity | error | A tenant-scoped entity has no query filter whose expression references its tenant property | `{Entity} has tenant property '{Property}' but no query filter references it. Add HasQueryFilter(e => e.{Property} == <current tenant>) on the root type.` |
| TGM002 unscoped-entity | error, only with `RequireAllEntitiesScoped` | An entity type has no tenant property and is not declared shared | `{Entity} has no tenant property and is not declared shared. Add '{Property}', or declare it with Shared<{Entity}>().` |
| TGM003 captured-value | error | The tenant filter compares the tenant property to a value read from a compiler-generated closure (a local captured in OnModelCreating or a configuration class) | `{Entity}: the tenant filter captures a local value. The model is cached per context type, so every tenant will see the first tenant's rows. Reference a DbContext member instead.` |
| TGM004 literal-tenant | error | The tenant filter compares the tenant property to a literal constant | `{Entity}: the tenant filter compares '{Property}' to the constant {Value}. Reference a DbContext member instead.` |
| TGM005 nullable-tenant | warning | The tenant property is nullable | `{Entity}.{Property} is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.` |
| TGM006 tenant-on-derived | error | A non-root type in a hierarchy declares a tenant property while the root has none | `{Entity} declares '{Property}' but filters can only be defined on the root type {Root}. Move the property to the root.` |
| TGM007 filter-not-parameterized | warning | The tenant filter references the tenant property but the comparison operand is not rooted at the DbContext instance, a static member, or a method call (anything the validator cannot classify) | `{Entity}: the tenant filter's operand could not be classified; confirm it is evaluated per query.` |
| TGM008 owned-tenant | warning | An owned type has a tenant property | `{Owned} is owned by {Owner}; its '{Property}' is ignored, isolation follows the owner.` |

Operand classification for TGM003/004/007: walk the filter's body for binary comparisons
and method calls (`Equals`, `Contains`) involving a `MemberExpression` on the filter
parameter, or `EF.Property<T>(parameter, "{Property}")`, that resolves to the tenant
property. For the other operand, find the root of its member/call chain:

- Any expression on the chain, other than an `object`, whose type the context type derives
  from or implements: OK. EF puts the current context in its place, which is how
  Finbuckle's filter reaches the context through a captured null `IMultiTenantDbContext`.
  The startup check knows the context type. `TenantGuardModel.Validate` does not, so it
  counts a captured value that is a context, or a captured null of an interface or
  `DbContext` type, as the context.
- `ConstantExpression` whose type derives from `DbContext`: OK (EF parameterizes
  context-instance members per query).
- `ConstantExpression` whose type is compiler-generated (`CompilerGeneratedAttribute`, or
  name contains `<>c__DisplayClass`): TGM003.
- `ConstantExpression` of a primitive, string, Guid or enum: TGM004.
- `MemberExpression` with null `Expression` (static) or `MethodCallExpression` rooted at a
  static or at the context: OK.
- Anything else: TGM007.

A filter that references the tenant property only inside a navigation
(`p => p.Blog.TenantId == ...`) counts as referencing it (the row is scoped through its
principal); TGM001 still passes. The README documents the required-navigation caveat
from the EF docs: a filtered required principal can drop dependents via inner join.

### 4.3 Registration and timing

```csharp
optionsBuilder.UseTenantGuard(tg => tg
    .TenantPropertyNames("TenantId")                          // default
    .TenantProperty<Invoice>(i => i.OrgId)                    // explicit, per entity
    .CurrentTenant(ctx => ((ITenantContext)ctx).TenantId)     // required for the interceptor
    .RequireAllEntitiesScoped()                               // enables TGM002
    .Shared<Country>().Shared<Plan>()
    .Enforcement(Enforcement.Throw)                           // or Log
    .WhenNoCurrentTenant(NoTenantPolicy.Reject)               // or Allow
    .StampOnAdd(false)
    .IncludeKeysInErrors(true));                              // default
```

`UseTenantGuard` adds an `IDbContextOptionsExtension` that registers an
`IConventionSetPlugin` with one `IModelFinalizedConvention`. The convention runs the
rules on the finalized `IModel`, stores the per-entity metadata as a model annotation
(`TenantGuard:Entities`), and throws or logs per `Enforcement`.

Because EF builds the model lazily, `services.AddTenantGuardStartupValidation<TContext>()`
registers a startup check through `ValidateOnStart` (in `Microsoft.Extensions.Options`,
which EF Core already brings in). When the host starts, before any hosted service, the
check resolves `TContext` in a scope, or creates one from `IDbContextFactory<TContext>`, and
touches `.Model`, so a bad model fails the host before it serves a request. A context that
is not registered, or does not use `UseTenantGuard`, fails the check with a message saying so.

`TenantGuardModel.Validate(IModel model, TenantGuardOptions options) : ModelReport` runs
the rules on any model without a context, for unit tests and tooling. The options come from
`new TenantGuardOptionsBuilder()`, configured as in `UseTenantGuard`, through its `Options`
property. `ModelReport` lists `Findings` (rule, severity, entity, property, message) and
exposes `Errors`, `Warnings`.

## 5. SaveChanges guard

`TenantGuardSaveChangesInterceptor` (registered by `UseTenantGuard`) implements
`SavingChanges` and `SavingChangesAsync`. For every entry whose entity type is
tenant-scoped (owned types skipped):

| State | Check |
|---|---|
| Added | tenant value equals current tenant. If null or default and `StampOnAdd`, set it to the current tenant; otherwise violation. |
| Modified | both `OriginalValue` and `CurrentValue` of the tenant property equal current tenant. A changed tenant value is a violation (no moving rows across tenants). |
| Deleted | `OriginalValue` equals current tenant. |

Current tenant comes from `CurrentTenant(ctx)`. When it returns null:
`NoTenantPolicy.Reject` makes any tenant-scoped write a violation; `Allow` skips the
check and logs once per SaveChanges at Information level. A tenant-scoped write with no
`CurrentTenant` configured, or a current tenant that is not of the tenant property's type,
throws `InvalidOperationException` naming the context, or the entity and property.

`StampOnAdd` treats null and the property's sentinel (its type's default unless set with
`HasSentinel`) as not set. It stamps only when the save has no violations, so a blocked
save leaves the entities as they were.

Violations throw `TenantIsolationViolationException : InvalidOperationException` before
anything reaches the database. It carries `Violations`: entity type name, state, key
values (when `IncludeKeysInErrors`, default true), expected tenant, actual tenant. The
message lists up to 10 violations and the total; keys EF has not generated yet are left out:

```text
TenantGuard blocked 12 writes outside the current tenant "acme":
- Added Invoice (Id: 7): TenantId is "globex"
- Modified Invoice (Id: 3): TenantId changes from "acme" to "globex"
...
- and 2 more
Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).
```

Bypass: `using (context.TenantGuard().Bypass("nightly aggregation across tenants"))`
disables the interceptor for that context instance for the scope and logs a Warning
`TenantGuard bypass: {Reason} ({Context})`. An empty reason throws `ArgumentException`.
Bypass does not disable query filters; it only stops the write guard. A pooled context
goes back to the pool with the guard on, even if a bypass was never disposed.

Comparison uses the property's `ValueComparer`. Tenant values may be any type EF maps.

## 6. Analyzers

Package `TenantGuard.Analyzers`, `netstandard2.0`, `Microsoft.CodeAnalysis.CSharp` 4.8.0,
`EnforceExtendedAnalyzerRules`. Configured through `.editorconfig`:

```ini
tenantguard.tenant_column = TenantId           # default; comma-separated allowed
tenantguard.tenant_filter_names = TenantFilter # optional; names of the tenant filters
```

| ID | Severity | Trigger | Message |
|---|---|---|---|
| TG001 ignore-query-filters | warning | `IgnoreQueryFilters()` with no arguments, or `IgnoreQueryFilters(names)` where `names` is not a constant collection or contains a name in `tenant_filter_names` (when set; when unset, any names), in a method or type without `[AllowCrossTenantAccess]` | `IgnoreQueryFilters disables the tenant filter. Add [AllowCrossTenantAccess("why")] to the method, or ignore only the non-tenant filters by name.` |
| TG002 raw-sql-without-tenant | warning | `ExecuteSqlRaw`, `ExecuteSqlInterpolated` and `ExecuteSql`, their `Async` forms, `SqlQueryRaw` and `SqlQuery`, whose SQL argument is a literal or interpolated string that contains none of `tenant_column` (case-insensitive), in a method or type without `[AllowCrossTenantAccess]` | `Raw SQL bypasses query filters and does not mention '{Column}'. Add a tenant predicate, or [AllowCrossTenantAccess("why")].` |
| TG003 missing-justification | error | `[AllowCrossTenantAccess]` with a null, empty or whitespace reason | `AllowCrossTenantAccess needs a reason.` |
| TG004 raw-sql-unverifiable | info | Same methods as TG002 with a non-literal SQL argument | `Raw SQL built at runtime cannot be checked for a tenant predicate.` |

`FromSql`, `FromSqlRaw` and `FromSqlInterpolated` on entity types are not flagged: EF
composes global query filters over them. Behavior pin 3 (SPEC 9) holds EF to that; if it
fails for a provider, they join TG002 and this section changes.

A constant collection is an array, a collection initializer, or a collection expression
without a spread, whose items are string constants. SQL the analyzers can read is a
constant (a literal, a `const`, or constants joined with `+`) or an interpolated string, of
which the literal parts and the constant holes, such as `nameof(Invoice.TenantId)`, are
checked; any other SQL argument is TG004. With several `tenant_column` values, `{Column}`
lists them: `does not mention 'TenantId' or 'OrgId'`.

`AllowCrossTenantAccessAttribute(string reason)` lives in the runtime package
(`TenantGuard` namespace), valid on methods and classes. The analyzer matches it by full
name so the standalone analyzer package needs no runtime reference. It counts on the
method, on an enclosing lambda or local function, and on any containing class, and clears
TG001, TG002 and TG004 there. One with a null, empty or whitespace reason clears nothing.

## 7. Test kit

```csharp
var probe = new TenantIsolationProbe<AppDbContext>(tenant => CreateContext(tenant))
    .WithTenants("acme", "globex")
    .Seed<Invoice>((tenant, ctx) => new Invoice { Number = $"{tenant}-1", OrgId = tenant });

var report = await probe.RunAsync();
report.AssertAllPassed();   // throws with a table of failures
```

Seeding: for every tenant-scoped entity type, the default seeder creates one row per
tenant with required scalar properties filled from a deterministic generator (string,
numeric, bool, Guid, DateTime/DateOnly/TimeOnly, enum; non-generated keys included) and
required foreign keys satisfied by seeding principals first, in topological order over
the model's required relationships. A shared principal is seeded once. A cycle, or a
required FK to an entity with a user seeder that was not provided, is reported as
`Skipped (needs a seeder)` for that entity, never a silent pass. `Seed<T>` overrides the
default for `T`.

Tenants are strings, at least two. The probe converts each to the tenant property's type,
through a value converter that stores strings or the type's `TypeConverter`, so a `Guid`
tenant is passed as its text. Every context the factory creates has to use the same
database, with the schema in place. Seeding runs per tenant through that tenant's
context, one entity type per `SaveChanges`, so the write guard checks it too. The probe
always sets the tenant property, and any required foreign key left unset, even on a row
from `Seed<T>`. The default seeder also creates required owned references. It needs a
parameterless constructor and reports a required complex property as needing a seeder.
Keyless entity types, and types that share a CLR type, are reported as skipped. A model
with no tenant-scoped entity type gets a single skipped `(model)` row.

Checks, per tenant-scoped entity type, using a fresh context per tenant per check:

| Check | Expectation |
|---|---|
| ReadIsolation | As tenant B, `Set<E>()` returns none of A's keys. |
| FindIsolation | As tenant B, `FindAsync` with A's key returns null. |
| BulkIsolation | As tenant B, `Set<E>().ExecuteDeleteAsync()` deletes 0 of A's rows (skipped with a note when the provider does not support it). |
| UpdateGuard | As tenant B, attach a copy of A's row, modify a scalar, `SaveChangesAsync` throws `TenantIsolationViolationException`. |
| InsertGuard | As tenant B, add a new row stamped with A, `SaveChangesAsync` throws. |
| DeleteGuard | As tenant B, remove A's row (attached), `SaveChangesAsync` throws. |

Each check runs for every ordered pair of tenants and gives one row per entity type: failed
when any pair failed. The read checks run for every entity type before any check writes.
On a relational provider, each check that writes runs in a transaction that is rolled
back, so a leak cannot change what a later check sees. BulkIsolation deletes by A's key,
which removes nothing when the filter holds. ReadIsolation is skipped when tenant B
cannot read its own row, because then it proves nothing.

`ProbeReport.Rows` is `(Entity, Check, Outcome, Detail)` with outcomes `Passed`, `Failed`,
`Skipped`. `AssertAllPassed()` throws `TenantIsolationProbeException` whose message is a
fixed-width table of non-passing rows. The probe never touches the model validation; a
model that fails TGM rules fails before the probe runs, which is the point.

```text
TenantGuard isolation probe: 3 of 12 checks did not pass.

Entity    Check          Outcome  Detail
--------  -------------  -------  ------
Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). ...
Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. ...
Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). ...
```

The kit is framework-agnostic; docs show xUnit, NUnit, MSTest and TUnit usage.

## 8. Public API (complete for v0.1)

```csharp
namespace TenantGuard;
public sealed class TenantKeyAttribute : Attribute;
public sealed class AllowCrossTenantAccessAttribute(string reason) : Attribute;
public enum Enforcement { Throw, Log }
public enum NoTenantPolicy { Reject, Allow }
public enum Severity { Warning, Error }
public sealed class TenantGuardOptions;   // opaque; from TenantGuardOptionsBuilder.Options
public sealed class TenantGuardOptionsBuilder { TenantGuardOptionsBuilder(); TenantGuardOptions Options; /* methods in SPEC 4.3 */ }
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

Anything not listed is `internal`. `PublicAPI.Shipped.txt` is the source of truth from
Phase 5 on.

## 9. Behavior pins

`tests/TenantGuard.BehaviorPins` asserts, against SQLite, the EF behaviors the design
assumes. Each is one test with the EF docs link in a comment.

1. Query filters apply to `FindAsync` when the entity is not already tracked.
2. Query filters apply to `ExecuteDelete` and `ExecuteUpdate`.
3. Query filters compose over `FromSqlRaw` on an entity type.
4. A filter that captures a local in `OnModelCreating` returns the first tenant's rows to
   a second context constructed for another tenant (the bug TGM003 exists for).
5. A filter referencing a context field, or a captured null of an interface the context
   implements (Finbuckle's pattern), is evaluated per context instance.
6. `IgnoreQueryFilters()` returns other tenants' rows (so TG001 matters).
7. Filters on a required principal drop dependents via inner join (documented caveat).

## 10. Configuration surface in projects

- `.editorconfig` keys from SPEC 6 for the analyzers.
- Options in code only; no appsettings binding in v0.1.

## 11. Logging

Through `ILoggerFactory` from the application service provider when present, else
`NullLogger`. Category `TenantGuard`. Events: `ModelValidated` (Information, counts),
`ModelFinding` (per finding, severity by rule), `NoTenantAllowed` (Information),
`Bypass` (Warning), `ViolationBlocked` (Error, before the throw).

## 12. This repo's own CI

- `ci.yml`: ubuntu, `actions/setup-dotnet` from `global.json`, `go-task/setup-task`,
  then one step per task (`lint`, `build`, `test:*`, `pack`, `samples`, `scrub`),
  plus `jimmykurian/ci-parity@v0` with `runner: taskfile`.
- `release.yml`: release-please (simple type, bumps `Directory.Build.props` Version via
  `extra-files`), then `task pack` and `dotnet nuget push` with NuGet Trusted Publishing
  (`NuGet/login` OIDC, no stored API key), then attach `.nupkg` and `.snupkg` to the
  GitHub Release.
- SQL Server and PostgreSQL runs of the probe tests via Testcontainers are a separate
  opt-in job (`workflow_dispatch`, nightly, and pull requests that change those
  tests), not required for merge.

## 13. Non-functional

- Model validation adds under 50 ms to model building for a 200-entity model.
- The interceptor adds no allocations on a SaveChanges with no tenant-scoped entries, and
  O(entries) otherwise.
- All messages are deterministic and stable; they are part of the public contract.
