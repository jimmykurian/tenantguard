# Decisions

Problems found with a decision in `AGENTS.md` or `docs/SPEC.md`, and the change proposed
for each. An entry is written before any work around it, and the work stops until the
proposal is accepted or rejected. Newest first.

Each entry: date, the decision, the problem, the proposed change, status (open, accepted,
rejected).

## 2026-10-30: Writes through an owned entity skip the write guard (open)

Decision: the write guard checks "every entry whose entity type is tenant-scoped (owned
types skipped)" (SPEC 5), and the PLAN test is "owned types skipped".

Problem: EF tracks an owned entity as an entry of its own. Changing only the owned part,
such as `order.Shipping.Street`, leaves the owner Unchanged and the owned entry Modified,
and EF writes it to the owner's row (`OwnsOne`) or to rows keyed by the owner
(`OwnsMany`). The guard skips the owned entry and never looks at the owner, so a context
for globex that tracks acme's `Order` (read with `IgnoreQueryFilters`, or attached) can
change acme's shipping address, or add or delete acme's order lines. Reproduced on EF Core
10.0.14 and 11.0 rc.3: the save went through and acme's row changed.

Proposed change: an Added, Modified or Deleted entry of an owned type is checked through
its owner. The guard finds the owner's entry from the ownership foreign key (public API,
built only when an owned entry changed) and requires the owner's original tenant to equal
the current tenant, walking up through nested owned types. Owned types still have no
tenant property of their own to check. SPEC 5 says "(owned types are checked through their
owner)", and the PLAN test becomes "a change to an owned entity of another tenant's row is
rejected".

## 2026-10-30: No allocations only where the guard can skip DetectChanges (open)

Decision: "The interceptor adds no allocations on a SaveChanges with no tenant-scoped
entries, and O(entries) otherwise" (SPEC 13), and the PLAN test "a context with no
tenant-scoped entries allocates nothing".

Problem: `SavingChanges` runs before `SaveChanges` calls `DetectChanges`, so the guard has
to run it, through `ChangeTracker.Entries()`, to know which entries are Added, Modified or
Deleted: a changed property, or a new entity reached through a navigation, shows up only
after it. Both allocate inside EF Core. Measured on EF Core 10.0.14 and 11.0 rc.3: 32 bytes
with an empty change tracker, and about 490 bytes with one tracked `Country` (360 in
`DetectChanges`, 128 in `Entries()`). So when the model has tenant-scoped entity types, a
save whose entries are all shared still allocates. Avoiding that needs EF Core internals
(`IStateManager`), or `ChangeTracker.Tracked` and `StateChanged` handlers, which make EF
allocate event arguments for every entity a tracking query returns.

Proposed change: SPEC 13 reads "The interceptor adds no allocations on a SaveChanges when
the model has no tenant-scoped entity type, nothing is tracked, or a bypass is open, and
O(entries) otherwise." The guard does that now: it reads the model metadata, then checks
for an empty change tracker without `DetectChanges`, and three tests pin the three cases.
The PLAN test item changes to match.

## 2026-10-30: Startup validation through ValidateOnStart, not a hosted service (accepted)

Decision: `AddTenantGuardStartupValidation<TContext>()` registers an `IHostedService`
(SPEC 4.3), which needs `Microsoft.Extensions.Hosting.Abstractions` (entry of 2026-10-29).

Problem: that makes `Microsoft.Extensions.Hosting.Abstractions` a second runtime
dependency, against the EF Core only rule in AGENTS.md.

Proposed change: register the check through `ValidateOnStart` in
`Microsoft.Extensions.Options`, which EF Core already brings in. The host runs it at start,
before any hosted service, so a bad model still fails the host before it serves a request,
and EF Core stays the only runtime dependency. The check resolves the context in a scope,
or creates one from `IDbContextFactory<TContext>`, and fails with a message when the
context is not registered or does not use `UseTenantGuard`. SPEC 4.3 changes to match.

## 2026-10-30: Public API for options and severity (accepted)

Decision: SPEC 8 is the complete v0.1 public API, and anything it does not list is internal.

Problem: SPEC 8 uses `Severity` and `TenantGuardOptions` without listing them, and gives no
way to get a `TenantGuardOptions` for `TenantGuardModel.Validate` outside `UseTenantGuard`.

Proposed change: add `public enum Severity { Warning, Error }`, an opaque
`public sealed class TenantGuardOptions`, and a public constructor and `Options` property on
`TenantGuardOptionsBuilder`, the way `DbContextOptionsBuilder` exposes its options:
`new TenantGuardOptionsBuilder().TenantPropertyNames("OrgId").Options`. SPEC 8 lists them.

## 2026-10-30: Per-file copyright headers instead of none (accepted)

Decision: one LICENSE at the root and no per-file headers (AGENTS.md).

Problem: the owner wants the same per-file headers as their other .NET services, which
put StyleCop's XML copyright header in C# files and a `#` copyright line in YAML.

Proposed change: C# files start with the StyleCop header built from `companyName` and
`copyrightText` in `stylecop.json`, enforced by SA1633 and the rules that follow it. YAML
files and the commit-msg hook start with the same copyright line as a `#` comment.
Markdown, JSON, MSBuild and ignore files have none. The text names Apache-2.0 instead of
"All rights reserved". SA1636 matches the text exactly, so changing the year means
changing `copyrightText` and every header together.

## 2026-10-30: Target net10.0 and net11.0 instead of net8.0 and net10.0 (accepted)

Decision: the library targets `net8.0` (EF Core 8.0.x, also serving EF Core 9) and
`net10.0` (EF Core 10.x), built with the .NET 10 SDK (AGENTS.md, SPEC 2).

Problem: .NET 8, .NET 9, EF Core 8 and EF Core 9 reach end of support on 2026-11-10,
before v0.1.0 ships, so the `net8.0` build would serve only unsupported versions.
EF Core 11 requires .NET 11, so .NET 11 apps would load the `net10.0` build, compiled
against EF Core 10, and nothing would test that combination.

Proposed change: target `net10.0` (EF Core 10.x) and `net11.0` (EF Core 11.x), built
with the .NET 11 SDK. `GetDeclaredQueryFilters` exists on both targets, so the Phase 1
`#if` split goes away. Teams on .NET 8 or 9 cannot use the library. Until .NET 11 ships
on 2026-11-10, `global.json` pins SDK `11.0.100-rc.3.26522.148` with `allowPrerelease`
and `net11.0` references EF Core `11.0.0-rc.3.26522.148`; at release, move both to the
GA versions and drop `allowPrerelease`.

## 2026-10-29: `AddTenantGuardStartupValidation` needs a hosting dependency (rejected)

Decision: the runtime package depends on `Microsoft.EntityFrameworkCore` only (AGENTS.md).

Problem: SPEC 4.3 has `AddTenantGuardStartupValidation<TContext>()` register an
`IHostedService`. That interface lives in `Microsoft.Extensions.Hosting.Abstractions`,
which EF Core does not bring in, so Phase 1 cannot build it under the current rule.

Proposed change: allow `Microsoft.Extensions.Hosting.Abstractions` as the second runtime
dependency, at the version matching each target's EF Core (10.0.x for net10.0, 11.0.x for
net11.0). It is abstractions only and every ASP.NET Core or worker app already has it.
The other option, a separate `TenantGuard.Hosting` package, adds a fourth package the
spec does not list.

Rejected in favor of the ValidateOnStart entry above, which needs no new dependency.
