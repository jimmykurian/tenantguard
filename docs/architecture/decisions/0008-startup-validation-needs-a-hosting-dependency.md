# 0008. Startup validation through a hosted service, with a hosting dependency

- Status: Rejected
- Date: 2026-10-29

## Context

`AddTenantGuardStartupValidation<TContext>()` was to register an `IHostedService`. That
interface lives in `Microsoft.Extensions.Hosting.Abstractions`, which EF Core doesn't bring
in, so it can't be built under [0002](0002-runtime-depends-on-ef-core-only.md).

## Decision

Proposed: allow `Microsoft.Extensions.Hosting.Abstractions` as a second runtime
dependency, at the version matching each target's EF Core (10.0.x for net10.0, 11.0.x for
net11.0); it is abstractions only, and every ASP.NET Core or worker app already has it. The
other option, a separate `TenantGuard.Hosting` package, adds a fourth package.

Rejected in favor of [0009](0009-startup-validation-through-validateonstart.md), which needs
no new dependency.

## Consequences

None; EF Core stays the only runtime dependency.
