# 0013. No allocations only where the guard can skip DetectChanges

- Status: Accepted
- Date: 2026-10-30

## Context

The quality goal read: the interceptor adds no allocations on a SaveChanges with no
tenant-scoped entries, and O(entries) otherwise. `SavingChanges` runs before `SaveChanges`
calls `DetectChanges`, so the guard has to run it, through `ChangeTracker.Entries()`, to
know which entries are Added, Modified or Deleted: a changed property, or a new entity
reached through a navigation, shows up only after it. Both allocate inside EF Core.
Measured on EF Core 10.0.14 and 11.0 rc.3: 32 bytes with an empty change tracker, and about
490 bytes with one tracked `Country` (360 in `DetectChanges`, 128 in `Entries()`). So when
the model has tenant-scoped entity types, a save whose entries are all shared still
allocates. Avoiding that needs EF Core internals (`IStateManager`), or
`ChangeTracker.Tracked` and `StateChanged` handlers, which make EF allocate event arguments
for every entity a tracking query returns.

## Decision

The goal becomes: the interceptor adds no allocations on a SaveChanges when the model has
no tenant-scoped entity type, nothing is tracked, or a bypass is open, and O(entries)
otherwise.

## Consequences

Implemented: the guard reads the model metadata, then checks for an empty change tracker
without `DetectChanges`, and three tests pin the three cases. A save whose tracked entries
are all shared still allocates inside EF Core, and [the architecture
overview](../README.md) states the goal as this record words it.
