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

Owned entity types are skipped: an entry of an owned type isn't checked, and changing only
the owned part of another tenant's row isn't blocked yet
([0014](../architecture/decisions/0014-owned-entity-writes-checked-through-the-owner.md)).

## The current tenant

The current tenant comes from `CurrentTenant(context)`. When it returns null,
`NoTenantPolicy.Reject` (the default) makes any tenant-scoped write a violation, and
`Allow` skips the check and logs once per `SaveChanges` at Information level.

A tenant-scoped write with no `CurrentTenant` configured, or with a current tenant that
isn't of the tenant property's type, throws `InvalidOperationException` naming the context,
or the entity and the property ([Exceptions](exceptions.md#configuration-errors)).

## StampOnAdd

`StampOnAdd` treats null and the property's sentinel (its type's default, unless set with
`HasSentinel`) as not set. It stamps only when the save has no violations, so a blocked save
leaves the entities as they were.

## Violations

Violations throw `TenantIsolationViolationException`, an `InvalidOperationException`, which
lists each one: the entity type, its state, its key values (when `IncludeKeysInErrors` is
on, the default), and the expected and actual tenant. [Exceptions](exceptions.md) has the
message. No other value of the entity is in the exception or the logs.

## Bypass

`using (context.TenantGuard().Bypass("why"))` switches the write guard off for that context
instance until the scope is disposed, and logs a Warning. It doesn't switch the query filters
off. [Options](options.md#the-bypass-scope) has the details.

## What it doesn't see

`ExecuteUpdate` and `ExecuteDelete` don't go through `SaveChanges`, so the write guard
doesn't see them. The query filter scopes them, and the probe's BulkIsolation check proves
it for each entity. Raw SQL is the analyzers' job (TG002, TG004).
