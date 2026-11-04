# 0027. No source generator while compiled models refuse query filters

- Status: Accepted
- Date: 2026-11-04

## Context

Issue #26 asks for a source generator that finds `[TenantKey]` at build time, so tenant
property discovery works under Native AOT, where EF Core uses a compiled model. It asked
first for a check of what a compiled model keeps.

The check found:

- `dotnet ef dbcontext optimize` refuses a model with a query filter, on EF Core 10.0.14
  and on 11.0.0-rc.3: "Compiled model can't be generated, because query filters are not
  supported." Native AOT needs a compiled model, so an app that isolates tenants with query
  filters can't run under Native AOT, with or without TenantGuard.
- The `[TenantKey]` lookup raises no trim or AOT analyzer warning. On a model built at
  runtime, `[TenantKey]`, Finbuckle's annotation and `TenantPropertyNames` are all found.

## Decision

- No source generator. Nothing it would generate can be used while a compiled model can't
  carry the tenant filter.
- Behavior pin 9 asserts that EF Core's compiled model scaffolder refuses a model with a
  query filter. When an EF Core release generates one, the pin fails, and #26 is revisited:
  what the compiled model keeps of `[TenantKey]` decides whether a generator is needed.
- The rules and compatibility pages say that a compiled model, and so Native AOT, can't
  carry query filters on EF Core 10 or 11.

## Consequences

TenantGuard ships no third Roslyn component, and the runtime package isn't marked
`IsAotCompatible`. An app that needs a compiled model for another reason can't isolate
tenants with query filters, and TenantGuard has nothing to check in it. Issue #26 stays
open, labelled blocked on EF Core.
