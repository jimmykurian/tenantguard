# 0014. Writes through an owned entity are checked through its owner

- Status: Accepted
- Date: 2026-10-30

## Context

The write guard checks every entry whose entity type is tenant-scoped, and skips owned
types. EF Core tracks an owned entity as an entry of its own. Changing only the owned part,
such as `order.Shipping.Street`, leaves the owner Unchanged and the owned entry Modified,
and EF writes it to the owner's row (`OwnsOne`) or to rows keyed by the owner (`OwnsMany`).
The guard skips the owned entry and never looks at the owner, so a context for globex that
tracks acme's `Order` (read with `IgnoreQueryFilters`, or attached) can change acme's
shipping address, or add or delete acme's order lines. Reproduced on EF Core 10.0.14 and
11.0 rc.3: the save went through and acme's row changed.

## Decision

An Added, Modified or Deleted entry of an owned type is checked through its owner. The
guard finds the owner's entry from the ownership foreign key (public API, built only when an
owned entry changed) and requires the owner's original tenant to equal the current tenant,
walking up through nested owned types. Owned types still have no tenant property of their
own to check.

## Consequences

Implemented in #108. A change to an owned entity of another tenant's row is rejected, for
`OwnsOne`, `OwnsMany` (add, change and delete) and nested owned types, and `OwnedWriteGuardTests`
covers each. An owner that is itself Added, Modified or Deleted is checked by its own entry,
so its owned entries aren't reported again, and `StampOnAdd` stamps a new owner as before.
A Modified owned entry is checked through the owner its original foreign key names too, so
an `OwnsMany` row with a key of its own can't be moved away from another tenant's row, and
one whose owner isn't tracked, as when only the owned entry was attached, is a violation:
its tenant can't be read.
`TenantViolation.Owner` names the owner whose tenant was checked, since EF Core's display
name of an owned type doesn't. [The write guard reference](../../reference/write-guard.md#owned-entities)
gives the details, and [Exceptions](../../reference/exceptions.md#tenantisolationviolationexception)
the message.
