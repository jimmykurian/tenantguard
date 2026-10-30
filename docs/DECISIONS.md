# Decisions

Problems found with a decision in `AGENTS.md` or `docs/SPEC.md`, and the change proposed
for each. An entry is written before any work around it, and the work stops until the
proposal is accepted or rejected. Newest first.

Each entry: date, the decision, the problem, the proposed change, status (open, accepted,
rejected).

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

## 2026-10-29: `AddTenantGuardStartupValidation` needs a hosting dependency (open)

Decision: the runtime package depends on `Microsoft.EntityFrameworkCore` only (AGENTS.md).

Problem: SPEC 4.3 has `AddTenantGuardStartupValidation<TContext>()` register an
`IHostedService`. That interface lives in `Microsoft.Extensions.Hosting.Abstractions`,
which EF Core does not bring in, so Phase 1 cannot build it under the current rule.

Proposed change: allow `Microsoft.Extensions.Hosting.Abstractions` as the second runtime
dependency, at the version matching each target's EF Core (10.0.x for net10.0, 11.0.x for
net11.0). It is abstractions only and every ASP.NET Core or worker app already has it.
The other option, a separate `TenantGuard.Hosting` package, adds a fourth package the
spec does not list.
