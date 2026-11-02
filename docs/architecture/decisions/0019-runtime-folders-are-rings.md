# 0019. The runtime package's folders are rings with an enforced direction

- Status: Accepted
- Date: 2026-11-01

## Context

Applying Clean Architecture's Dependency Rule (dependencies point inward, and the build
enforces it) to the runtime package: between packages it held, and the architecture tests
check it. Inside the runtime it didn't: `Model/` held the rules next to the EF convention
plumbing, and `Options/` held the configuration everything reads next to the composition
root that depends on everything, so the folders depended on each other both ways. The write
guard's scope also read `TenantGuardOptionsExtension` from the context options itself.

## Decision

The folders are rings, innermost first, and a ring depends only on rings further in:
`Options/` (configuration and the user's markers), `Model/` (the rules), `Diagnostics/`
(logging), `Interception/` (the write guard), and `Hosting/` (the composition root:
`TenantGuardOptionsExtension`, `UseTenantGuard`, `AddTenantGuardStartupValidation`, the EF
convention plugin and convention, the startup check). `TenantGuardScope` takes its options
and logger in its constructor, from the composition root. The namespace stays `TenantGuard`
([0007](0007-flat-public-namespaces.md)).

## Consequences

`RingRules` in `tests/architecture` reads each type's folder from the symbols and fails on
an outward dependency. The rest of Clean Architecture's layering (Domain, Application,
Infrastructure, API) doesn't apply to a library whose domain is the user's EF model.
