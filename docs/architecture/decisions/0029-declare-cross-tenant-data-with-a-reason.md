# 0029. Declare cross-tenant data, each with a reason

- Status: Accepted
- Date: 2026-11-05

## Context

The field trial (#28) found tenant data that is cross-tenant on purpose, and TenantGuard
had no way to say so short of leaving `UseTenantGuard` off the context (#67). It came up
in six shapes:

1. An entity type with a tenant column that isn't filtered: a membership table, an
   outbox, a tenant store. It gets TGM001, and the write guard blocks its writes. Four of
   nine trial apps, 40 TGM001 errors.
2. A whole sibling context with no filters, for reports and deletions. 25 TGM001 errors,
   and leaving `UseTenantGuard` off doesn't silence the validate tool.
3. A model per tenant whose no-tenant model has no filters, for background work. Its first
   use throws under `Enforcement.Throw`.
4. Host rows with a null tenant, as in ABP: TGM005 on every entity, and the default
   `NoTenantPolicy.Reject` blocks the framework's own host seeding.
5. A rule that's right but unwanted for one type, such as TGM009 on a lookup table or
   TGM010 on a token hash that's unique across tenants. Only the log level silenced it.
6. The table of tenants, filtered on its own key, reported as TGM002 under
   `RequireAllEntitiesScoped`.

`Shared<TEntity>()` covers none of them: it refuses a type with a tenant property.

## Decision

- Each exemption is a declaration in `UseTenantGuard` with a reason, which is logged, and
  printed by the validate tool, so it can be read where it is used, as `Bypass(reason)` is:
  `Unfiltered<TEntity>(reason)`, `CrossTenant(reason)`, `CrossTenantWhenNoTenant(reason)`,
  `Suppress(ruleId, reason)` and `Suppress<TEntity>(ruleId, reason)`. `NullTenantIsHost()`
  describes a data model rather than an exemption, so it takes no reason.
- `Unfiltered<TEntity>` keeps the type tenant-scoped: the rules about its column and
  indexes still run, the rules about its filter don't, the write guard skips its rows and
  the probe leaves it out.
- `CrossTenant` skips every rule and the write guard for the context. The probe refuses it.
- `CrossTenantWhenNoTenant` applies only when the context building the model has no
  current tenant and the model cache isn't EF Core's own, the same test as
  `AllowCapturedValues` ([0026](0026-tgm003-allows-a-captured-tenant-in-a-per-tenant-model.md)).
  The no-tenant model remembers it, and the write guard blocks a save on it with a current
  tenant.
- `NullTenantIsHost` makes the host, the context with no current tenant, one more tenant
  whose value is null: TGM005 isn't reported, and the write guard compares null with null
  instead of applying `WhenNoCurrentTenant`.
- `Suppress` takes a warning for every type, or any rule for one type. An error is never
  suppressed globally, and TGM001 and TGM002 aren't suppressed at all: `Unfiltered` and
  `Shared` say what is meant. A suppressed finding moves to `ModelReport.Suppressed`.
- A tenant registry needs no new option: `TenantProperty<Club>(club => club.Id)` already
  makes it tenant-scoped. The probe seeds the key with the tenant and skips InsertGuard,
  since a row stamped A is A's own row.

## Consequences

Every trial app can run under `Enforcement.Throw` with its cross-tenant data declared,
and the checks that do apply keep running. The public API gains six options on
`TenantGuardOptionsBuilder`, `ModelReport.Suppressed`, three log events (6 to 8), and new
configuration errors, all listed in `docs/reference/`. A declaration is trusted: TenantGuard
can't tell whether an unfiltered type should have been filtered, so the reason, the log
line and the validate tool's output are how a reviewer sees it. The analyzers don't yet see
a cross-tenant context's injections.
