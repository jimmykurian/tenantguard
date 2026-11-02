# 0002. The runtime package depends on EF Core only

- Status: Accepted
- Date: 2026-10-29

## Context

TenantGuard checks models built by apps on any provider and any tenancy package. Every
dependency of the runtime package becomes a dependency of every app that installs it, and
can conflict with what the app already uses.

## Decision

`TenantGuard.EntityFrameworkCore` depends on `Microsoft.EntityFrameworkCore` only: no
provider, no tenancy library, and no logging or hosting package beyond what EF Core already
brings in (`Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`,
`Microsoft.Extensions.DependencyInjection`). Finbuckle appears only in `samples/` and in the
tests of the sample apps ([0017](0017-end-to-end-tests-live-in-tests-e2e.md)).

## Consequences

- Startup validation can't use `IHostedService`
  ([0008](0008-startup-validation-needs-a-hosting-dependency.md),
  [0009](0009-startup-validation-through-validateonstart.md)).
- Finbuckle's annotation is matched by name, not by type.
- The architecture tests check the runtime's dependencies on every build.
