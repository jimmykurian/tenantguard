# 0031. Probe a context with no tenant

- Status: Proposed
- Date: 2026-11-10

## Context

A filter such as `CurrentTenantId == null || e.TenantId == CurrentTenantId` returns every
tenant's rows to a context with no current tenant: a background job, a startup task, or a
request that reached the database before its tenant was resolved (#100). TGM013
([0030](0030-model-rules-for-natural-keys-views-switchable-filters-and-dependents.md))
reports the shape when the model shows it. It can't see the same leak built some other
way, such as a filter that calls a method the rules can't read, a tenant accessor whose
default matches every row, or a tenancy package that switches its filter off with no tenant.

The probe would catch the leak at runtime, but every context it creates comes from
`Func<string, TContext>`, so every context has a tenant. 0030 left this out, because it
needs a factory for a context with no tenant. The probe's public API and its check names
are the contract ([0006](0006-the-public-contract.md)), so it needs a decision.

## Decision

- `TenantIsolationProbe<TContext>.WithoutTenant(Func<TContext> createContext)` takes a
  factory for a context with no current tenant, and returns the probe. It is opt-in: without
  it, the probe runs and reports as before.
- With it, a new read check, NoTenantIsolation, runs once per probed entity type, after the
  read checks and before NavigationLeak, as a fresh context from that factory. It passes
  when `Set<E>()` returns none of the seeded tenants' keys and `FindAsync` with each
  tenant's key returns null. Its row is the entity type's last.
- A query that throws returns no rows, so the row passes, naming the exception, as the
  guard checks do when the app's own `SaveChanges` blocks a write.
- It is checked only against rows their own tenant can read or find, as the read checks are,
  and skipped when none is left. Under `CrossTenantWhenNoTenant`, a no-tenant context whose
  model is the cross-tenant one is skipped with a note. Under `NullTenantIsHost`, the context
  with no tenant is the host, and the check is the same.
- It doesn't load navigations: a context with no tenant has no row of its own to `Include`
  from, and EF Core applies the target's query filter to an `Include`, which the target
  type's own row checks. It doesn't write: with no current tenant, the write guard's
  `WhenNoCurrentTenant` already decides, and its default rejects every tenant-scoped write.

## Alternatives considered

- **A `Func<string?, TContext>` factory.** It breaks every caller, and most factories don't
  expect a null tenant.
- **Calling the existing factory with `null`.** It has the same problem, without the user
  agreeing to it.
- **A skipped NoTenantIsolation row when `WithoutTenant` isn't called,** to point users at
  it. It adds a row to every report and a skipped test to every adapter's list; the testing
  guide says when to call it instead.

## Consequences

An app that calls `WithoutTenant` gets one more row per probed entity type, and one more
test per type in the test adapters, named `{Entity}.NoTenantIsolation`. One that doesn't
sees no change. The leak TGM013 reports in the model is now proved or ruled out at runtime,
however the filter is built, but only for apps that give the probe a context with no tenant.
