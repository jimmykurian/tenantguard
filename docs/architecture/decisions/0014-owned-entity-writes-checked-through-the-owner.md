# 0014. Writes through an owned entity are checked through its owner

- Status: Proposed
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

Not implemented yet: the guard still skips owned entries, `Owned_types_are_skipped` pins
that, and the README's FAQ and [the write guard reference](../../reference/write-guard.md)
say so. When accepted, the test becomes "a change to an owned entity of another tenant's
row is rejected".
