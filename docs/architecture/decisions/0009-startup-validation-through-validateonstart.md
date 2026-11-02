# 0009. Startup validation through ValidateOnStart

- Status: Accepted
- Date: 2026-10-30

## Context

EF Core builds the model lazily, so a model that breaks a rule fails on its first query,
possibly long after deployment. Startup validation should fail the host before it serves a
request, without the hosting dependency [0008](0008-startup-validation-needs-a-hosting-dependency.md)
needed.

## Decision

`AddTenantGuardStartupValidation<TContext>()` registers its check through `ValidateOnStart`
in `Microsoft.Extensions.Options`, which EF Core already brings in. When the host starts,
before any hosted service, the check resolves the context in a scope, or creates one from
`IDbContextFactory<TContext>`, and touches `.Model`. A context that isn't registered, or
doesn't use `UseTenantGuard`, fails the check with a message saying so.

## Consequences

A bad model fails the host before it serves a request, and EF Core stays the only runtime
dependency. It works in any app built on the generic host.
