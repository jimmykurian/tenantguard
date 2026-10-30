# Decisions

Problems found with a decision in `AGENTS.md` or `docs/SPEC.md`, and the change proposed
for each. An entry is written before any work around it, and the work stops until the
proposal is accepted or rejected. Newest first.

Each entry: date, the decision, the problem, the proposed change, status (open, accepted,
rejected).

## 2026-10-29: `AddTenantGuardStartupValidation` needs a hosting dependency (open)

Decision: the runtime package depends on `Microsoft.EntityFrameworkCore` only (AGENTS.md).

Problem: SPEC 4.3 has `AddTenantGuardStartupValidation<TContext>()` register an
`IHostedService`. That interface lives in `Microsoft.Extensions.Hosting.Abstractions`,
which EF Core does not bring in, so Phase 1 cannot build it under the current rule.

Proposed change: allow `Microsoft.Extensions.Hosting.Abstractions` as the second runtime
dependency, at the version matching each target's EF Core (8.0.x for net8.0, 10.0.x for
net10.0). It is abstractions only and every ASP.NET Core or worker app already has it.
The other option, a separate `TenantGuard.Hosting` package, adds a fourth package the
spec does not list.
