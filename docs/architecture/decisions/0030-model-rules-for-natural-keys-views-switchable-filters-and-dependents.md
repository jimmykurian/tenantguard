# 0030. Model rules for natural keys, views, switchable filters and unscoped dependents

- Status: Accepted
- Date: 2026-11-10

## Context

The field trial (#28) found five ways a model leaks that pass every rule (#70):

1. TGM010 skipped every primary key, because a generated key is unique across tenants. A
   natural key isn't, such as an external login's `(LoginProvider, ProviderKey)`: a second
   tenant can't link an account the first already linked, and the refusal tells it the
   account exists. Finbuckle's Identity model has this key.
2. An entity type mapped to a view that drops the tenant column reads every tenant's rows.
   It has no tenant property, so no rule runs on it unless `RequireAllEntitiesScoped` is on.
3. A filter shaped like `!IsTenantFilterEnabled || e.TenantId == TenantId`, which is how
   ABP switches its filter off, passes every rule, and nothing says it can be turned off.
4. A filter shaped like `CurrentTenantId == null || e.ClubId == CurrentTenantId` returns
   every tenant's rows whenever there is no current tenant: background jobs, routes outside
   a tenant, a context created without one.
5. Dependent entity types with no tenant property and no filter, such as line items, are
   reached through a tenant-scoped principal but are `DbSet`s too, so `Set<T>()` returns
   every tenant's rows. One trial app had 24. Only the opt-in `RequireAllEntitiesScoped`
   shows them, mixed in with tables shared on purpose.

The rule IDs, their scope and their default severities are the contract
([0006](0006-the-public-contract.md)), so widening TGM010 and adding rules needs a decision.

## Decision

- TGM010 checks a natural primary key: one with no property whose values are generated
  (`ValueGenerated.Never`) and no tenant property, with a message of its own. Its existing
  exemptions apply to the primary key too: a key that includes every column of a foreign
  key to a tenant-scoped entity type isn't reported, which covers join tables and a
  one-to-one dependent that shares its principal's key. A key of a single `Guid` property
  isn't reported either, because frameworks such as ABP assign random `Guid` keys with
  value generation switched off, and a random `Guid` can't be reused by another tenant. It
  stays an error, like the rest of TGM010.
- Four warnings, on by default:
  - TGM011 tenant-free-view: an entity type mapped to a view, a SQL query or a function,
    with no tenant property and not declared shared, in a model with a tenant-scoped entity
    type. TenantGuard can't see which tables a view reads, so it reports every such view;
    `Shared<TEntity>()` declares a lookup view.
  - TGM012 switchable-filter: the tenant comparison sits under an `||` whose other side
    doesn't read the row. A condition on the row, such as `e.IsPublic`, isn't reported.
    Negations are pushed inward first, so an `||` under a `!`, which only narrows the rows,
    as in `!(BlockAll || e.TenantId != TenantId)`, isn't reported either.
  - TGM013 open-without-tenant: the other side of that `||`, after the same rewrite, is a
    null check of the value the tenant property is compared with. It replaces TGM012 for
    that switch only. A chain of `||` is flattened into one list of conditions, however it
    is grouped, and each condition that doesn't read the row is a switch of its own, so a
    filter with an independent switch as well gets both, and suppressing either one leaves
    the other in place.
  - Neither reports a switch that an enclosing `&&` rules out with the switch's opposite,
    as in `CurrentTenantId != null && (CurrentTenantId == null || ...)`. The opposite is
    matched by shape; TenantGuard doesn't reason further, so a switch ruled out some other
    way is still reported, and is suppressed with its reason.
  - TGM014 unscoped-dependent: an entity type that isn't owned, tenant-scoped or declared
    shared, with a required foreign key to a tenant-scoped entity type and no query filter
    that reaches a tenant property through a navigation. EF Core's implicit many-to-many
    join types aren't reported.
- TGM011 and TGM014 aren't reported under `RequireAllEntitiesScoped`, where TGM002 already
  reports the same types as errors. `Unfiltered<TEntity>` leaves out TGM012 and TGM013 with
  the other rules about its filter. All four can be suppressed as any warning is.
- The probe doesn't change here. Running its read checks once as a context with no tenant,
  which would catch shape 4 at runtime, needs a factory that can make a context with no
  tenant, so a new option; it is a separate proposal.

## Consequences

The rules cover all five shapes by default. An app whose model has a natural primary key
without the tenant property fails at startup under `Enforcement.Throw` once it upgrades:
the key is made per tenant, or the type gets `Suppress<TEntity>("TGM010", reason)`. The
warnings are logged, not thrown, so an upgrade only adds log lines, but TGM012 is reported
on every filtered type in an app on a framework that switches filters this way, and needs
one global `Suppress("TGM012", reason)`. TGM011 can't tell a view of a shared table from a
view of a tenant-scoped one, so a lookup view needs `Shared<TEntity>()`.

The single `Guid` exemption trades a rare miss for less noise: a `Guid` key that comes from
outside, such as an identity provider's object ID, is still unique across tenants and isn't
reported. The rules reference says so, and how to make it per tenant: the tenant in the
key, or the external ID behind a generated key with a per-tenant unique index.
