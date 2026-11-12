# Write guard

The write guard stops a `SaveChanges` that would write, move or delete a row of a tenant
other than the context's current one, before anything reaches the database.
`UseTenantGuard` registers it, as an interceptor that implements both `SavingChanges` and
`SavingChangesAsync`.

## What each entry is checked against

For every entry whose entity type is tenant-scoped, against the current tenant:

| State | Check |
| --- | --- |
| Added | The tenant value equals the current tenant. If it's not set and `StampOnAdd` is on, it's set to the current tenant; otherwise it's a violation. |
| Modified | Both the tenant property's `OriginalValue` and its `CurrentValue` equal the current tenant. A changed tenant value is a violation: rows don't move across tenants. |
| Deleted | The `OriginalValue` equals the current tenant. |

Values are compared with the tenant property's `ValueComparer`, so the tenant can be any
type EF Core maps.

Entity types declared with `Unfiltered<TEntity>` are skipped, and so is every save of a
context declared `CrossTenant`, or of the no-tenant model under `CrossTenantWhenNoTenant`
([cross-tenant data](options.md#cross-tenant-data)).

## Owned entities

An owned type has no tenant property of its own: its rows are its owner's
([0014](../architecture/decisions/0014-owned-entity-writes-checked-through-the-owner.md)).
EF Core tracks an owned entity, such as `Order.Shipping` (`OwnsOne`) or `Order.Lines`
(`OwnsMany`), as an entry of its own, so changing only the owned part leaves the owner's
entry Unchanged. An Added, Modified or Deleted entry of an owned type is checked through its
owner:

- the guard finds the owner's entry from the ownership foreign key, walking up through
  nested owned types to the owner that isn't owned;
- when that owner is Added, Modified or Deleted, its own check covers the owned entry,
  which isn't checked again;
- when the owner is Unchanged, its tenant value has to be the current tenant, as for a
  Deleted entry; otherwise the owned entry is a violation.

So a context for globex that tracks acme's `Order`, read with `IgnoreQueryFilters()` or
attached, can't change `order.Shipping.Street`, or add, change or delete acme's order
lines. The owner's checks apply as they are: an owned entry of an `Unfiltered<TEntity>`
type, of a context declared `CrossTenant`, or of an owner that isn't tenant-scoped isn't
checked, and `NoTenantPolicy`, `NullTenantIsHost` and the bypass scope work the same way.

A tenant property declared on the owned type itself is ignored, and TGM008 reports it
([rules](rules.md#tgm008-owned-tenant)).

## The current tenant

The current tenant comes from `CurrentTenant(context)`. When it returns null,
`NoTenantPolicy.Reject` (the default) makes any tenant-scoped write a violation, and
`Allow` skips the check and logs once per `SaveChanges` at Information level.

A tenant-scoped write with no `CurrentTenant` configured, or with a current tenant that
isn't of the tenant property's type, throws `InvalidOperationException` naming the context,
or the entity and the property ([Exceptions](exceptions.md#configuration-errors)).

## Host rows

Under `NullTenantIsHost()`, the host is the context with no current tenant, and its tenant
value is null. The checks above run against it as against any tenant, and
`WhenNoCurrentTenant` doesn't apply:

| Current tenant | Row's tenant | Result |
| --- | --- | --- |
| none (the host) | null | Written |
| none (the host) | `"acme"` | A violation: the host doesn't write a tenant's row |
| `"acme"` | null | A violation, or stamped with `"acme"` when added under `StampOnAdd` |
| `"acme"` | `"acme"` | Written |

A tenant property that can't hold null has no host rows, so with no current tenant every
write to it is a violation.

## StampOnAdd

`StampOnAdd` treats null and the property's sentinel (its type's default, unless set with
`HasSentinel`) as not set. It stamps only when the save has no violations, so a blocked save
leaves the entities as they were.

## Violations

Violations throw `TenantIsolationViolationException`, an `InvalidOperationException`, which
lists each one: the entity type, its state, its key values (when `IncludeKeysInErrors` is
on, the default), and the expected and actual tenant; for an owned entry, also its owner,
whose tenant is the actual one. [Exceptions](exceptions.md) has the
message. No other value of the entity is in the exception or the logs.

## A model built for another tenant

Under [`AllowCapturedValues()`](options.md#per-tenant-models), a model whose filters capture
the tenant remembers the tenant it was built for. Under `CrossTenantWhenNoTenant`, the
cross-tenant model remembers that it was built with no tenant, and a save on it with a
current tenant is blocked the same way. When a save writes a tenant-scoped row
and the context's current tenant is a different one, the guard blocks the whole save, before
it checks any entry: the context has another tenant's model, so what it read came from that
tenant. The [exception](exceptions.md#tenantisolationviolationexception) names both tenants
and has no `Violations`. With no current tenant, `NoTenantPolicy` applies as before.

## Bypass

`using (context.TenantGuard().Bypass("why"))` switches the write guard off for that context
instance until the scope is disposed, and logs a Warning. It doesn't switch the query filters
off. [Options](options.md#the-bypass-scope) has the details.

## What it doesn't see

`ExecuteUpdate` and `ExecuteDelete` don't go through `SaveChanges`, so the write guard
doesn't see them. The query filter scopes them, and the probe's BulkIsolation check proves
it for each entity. Raw SQL is the analyzers' job (TG002, TG004).
