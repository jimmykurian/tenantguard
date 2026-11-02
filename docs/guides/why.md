# Why TenantGuard

Multi-tenant EF Core apps isolate tenants with global query filters and a `TenantId`
column. A dozen packages ship that mechanism (Finbuckle.MultiTenant, TenancyKit, SaasSuite,
Tenantry, MultiTenancy.EntityFrameworkCore, and more), and most teams hand-roll it. None of
them ship the proof. One forgotten `HasQueryFilter`, one `IgnoreQueryFilters()` in a report,
one `ExecuteSqlRaw` without a `WHERE TenantId`, or one filter that captured a local variable
at model build, and tenant A reads tenant B.

In an audit of a SaaS codebase, every entity had a `TenantId` and every reviewer assumed
every entity had a filter. One did not. Nothing in the build or the tests could have said so.

## What it proves

TenantGuard does not resolve tenants and does not add filters. It takes the model you
already have and:

- fails startup when an entity with a tenant property has no query filter on it, or when the
  filter is frozen to a value ([Model rules](../reference/rules.md));
- rejects a `SaveChanges` that writes, moves or deletes another tenant's row
  ([Write guard](../reference/write-guard.md));
- flags `IgnoreQueryFilters()` and raw SQL without a tenant predicate at compile time,
  unless the method carries a justification ([Analyzers](../reference/analyzers.md));
- proves isolation per entity in a test, so a new entity can't ship unguarded
  ([Isolation probe](../reference/probe.md)).

## How it compares

| Tool | What it does | What it leaves to TenantGuard |
| --- | --- | --- |
| Finbuckle.MultiTenant and the other tenancy packages | Resolve the tenant; add the filter and the tenant property | Proving the filter is on every entity and that queries and writes keep to it. TenantGuard runs on top of them. |
| Finbuckle's `[MultiTenant]` and `IsMultiTenant()` | Configure an entity as multi-tenant | Verifying it: they are configuration, not verification |
| EricksonLopez.MultiTenancy.Analyzers | Roslyn rules on that author's `ITenantContext` lifetimes and Dapper helpers | Anything about EF Core query filters |
| ArchUnitNET, NetArchTest | Structural rules on types and dependencies | Anything that needs the EF Core model |
