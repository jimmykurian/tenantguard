# 0015. TGM003 treats an operand EF Core swaps for the context as the context

- Status: Accepted
- Date: 2026-10-30

## Context

The rules classify a tenant filter's operand by the root of its member chain, and a root
that is a compiler-generated closure is TGM003. Finbuckle 10's `IsMultiTenant()` filter is
`EF.Property<string>(e, "TenantId") == dummyContext.TenantInfo.Id`, where `dummyContext` is
a captured null `IMultiTenantDbContext`. In a query filter, EF Core replaces every
expression whose type the context type derives from or implements with the current context
(`ContextParameterReplacer` in EF's `ExpressionTreeFuncletizer`), so that filter is
evaluated per context and is correct. TGM003 still reported it, so a Finbuckle app with
`UseTenantGuard` under `Enforcement.Throw` failed at startup, though TenantGuard is meant to
work on Finbuckle.

## Decision

Count an expression on the operand's chain as the context when it is not an `object` and
the context type derives from or implements its type, which is how EF Core treats it. The
startup check reads the context type from `ICurrentDbContext`. `TenantGuardModel.Validate`
has no context, so it counts a captured value that is a context, or a captured null of an
interface or `DbContext` type, as the context. On both paths, a captured tenant service
that is not the context is still TGM003.

## Consequences

Implemented: the Finbuckle sample runs with `UseTenantGuard`, behavior pin 5 pins EF Core's
replacement, and [the rules reference](../../reference/rules.md) describes the
classification. Reverting it means the Finbuckle sample has to leave `UseTenantGuard` out.
