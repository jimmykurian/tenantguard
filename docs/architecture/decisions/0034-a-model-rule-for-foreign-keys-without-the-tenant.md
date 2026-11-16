# 0034. A model rule for foreign keys without the tenant property

- Status: Accepted
- Date: 2026-11-16

## Context

Feedback on the 1.0.0 announcement found a reference TenantGuard lets through (#120). An
`Invoice` and a `Customer` are both tenant-scoped, and the foreign key is `CustomerId`
alone. As `acme`, an invoice stamped `acme` whose `CustomerId` is the key of a `globex`
customer is saved:

- The write guard checks each changed row's own tenant property
  ([write guard](../../reference/write-guard.md)). The customer isn't tracked, so there
  is nothing to compare the invoice with.
- The probe's NavigationLeak check proves that `Include` doesn't load the other tenant's
  row. It seeds exactly this link, saved through the dependent's own tenant.
- The reference is still in the database. Raw SQL, a report or a view that joins on
  `CustomerId` crosses tenants, and so does a read of `Customer` with its filter off.

Only the database can refuse the row, with a foreign key that includes the tenant column,
as `(TenantId, CustomerId)` referencing `(TenantId, Id)`. The probe already treats that
shape as safe. No rule says which relationships lack it. The rule IDs and their default
severities are the contract ([0006](0006-the-public-contract.md)), so a new rule needs a
decision.

A check on EF Core 11.0.0 with SQLite found how the composite foreign key behaves:

- The database refuses a link across tenants, and accepts a row whose `CustomerId` is null.
- When an optional relationship is cleared through the navigation, by removing the
  dependent from the principal's collection, or by deleting the principal under
  `ClientSetNull`, EF Core sets every foreign key property whose CLR type can hold null to
  null. A `string` tenant property becomes null, and the save fails; a `Guid` one keeps its
  value. Setting `CustomerId` to null clears the relationship and leaves the tenant alone.
- `DeleteBehavior.SetNull` makes the constraint `ON DELETE SET NULL`, which nulls every
  column of the foreign key in the database, tenant column included, on rows that aren't
  tracked.
- EF Core doesn't allow a nullable property in an alternate key, so the fix needs the
  principal's tenant property to be required.
- EF Core needs a key's values to track a row, so adding a principal whose tenant property
  is still null throws at `Add`. `StampOnAdd` and Finbuckle set the tenant at
  `SaveChanges`, which is too late. A value generator on the tenant property that reads
  the context's tenant sets it at `Add`.
- The database doesn't check a composite foreign key when one of its columns is null. A
  dependent row with a null tenant and a `CustomerId` is accepted, whichever tenant the
  customer belongs to; a check constraint that keeps a row with no tenant from holding a
  `CustomerId` refuses it.

## Decision

- A new warning, on by default: TGM015 tenant-free-foreign-key. It reports each foreign key
  whose dependent and principal entity types are both tenant-scoped, unless one of its
  properties is the dependent's tenant property and pairs with the principal's tenant
  property.
- Required and optional relationships are both reported, and so is a self-reference: each
  can hold a link across tenants.
- Not reported: a foreign key to an entity type with no tenant property; a foreign key of
  an owned type ([0014](0014-owned-entity-writes-checked-through-the-owner.md)); a foreign
  key of an entity type with no tenant property, EF Core's implicit many-to-many join type
  included; and a foreign key whose dependent or principal isn't a table, by the test
  TGM009 and TGM010 use.
- A principal declared `Unfiltered` is still tenant-scoped, so a foreign key to it is
  reported: `Unfiltered` leaves out the rules about a type's filter, and this isn't one.
- It is a warning because most models use single-column foreign keys, and the fix adds an
  alternate key to each principal and needs a migration. It is suppressed as any warning
  is, with `Suppress` or `Suppress<TEntity>`.
- The message gives the foreign key and the principal key the fix needs: the tenant pair
  first, then the foreign key's own pairs without one that already holds the principal's
  tenant property, so the two lists stay the same length and name no property twice.
- A foreign key that pairs the dependent's tenant property with another property of the
  principal gets a message of its own, with no code. Dropping that pair would take the
  other property out of the principal key and leave a key that allows one principal row
  for each tenant, and the property that should hold it instead is the app's to name.
- A principal whose tenant property is nullable is still reported. It can't take the fix
  while it has rows with no tenant, as under `NullTenantIsHost`, so the reference says to
  suppress the rule for the dependent, with the reason.
- A foreign key that has the tenant pair isn't reported when the dependent's tenant
  property is nullable, though the database then checks only the rows that have a tenant.
  TGM005 already reports a nullable tenant property, and under `NullTenantIsHost` the rows
  with no tenant are the host's on purpose. The reference gives the limit and the two ways
  to close it: a required tenant property, or a check constraint.
- The write guard doesn't check where a foreign key points. When the principal isn't
  tracked, that takes a query for each foreign key on every save, and another writer can
  still change the principal between the query and the save.
- The probe doesn't change. NavigationLeak already seeds a link across tenants on purpose,
  to check reads.
- Behavior pin 11 asserts what EF Core does to the tenant property when an optional
  composite relationship is cleared, since the rules reference tells apps how to clear one.

## Consequences

An app that upgrades gets one TGM015 warning in its log for each relationship between
tenant-scoped types, which is most relationships in most models. Nothing fails: warnings
are logged, not thrown. A team that won't change its foreign keys says so once, with
`Suppress("TGM015", reason)`.

Fixing a relationship costs an alternate key on the principal and a migration, and the
migration fails while the table holds a link across tenants, which is the thing to find.
With a `string` tenant property, an optional relationship then has to be cleared through
its foreign key property; clearing it through the navigation makes the save fail, loudly,
and moves no row. `DeleteBehavior.SetNull` doesn't fit a foreign key that includes the
tenant property, and the reference says to keep `ClientSetNull` or use `Restrict`.

A model with host rows, where the principal's tenant property is nullable, gets warnings
it can only suppress, unless host rows are given a tenant value of their own. Where the
dependent's tenant property is nullable, a foreign key that passes TGM015 still lets a host
row point at any tenant's row, until a check constraint is added: the rule reads the
model, and can't see check constraints written as SQL.

An app that leaves the tenant to be stamped at `SaveChanges`, as with `StampOnAdd` or
Finbuckle's shadow `TenantId`, needs a value generator on each principal's tenant property
before it can take the fix. The Finbuckle sample has one, and the reference shows it. The
probe sets a copied row's values before it tracks the copy, so a shadow tenant property in
a key can be probed.

Many-to-many relationships with EF Core's implicit join type stay unchecked: the join
table has no tenant column to put in a foreign key. The rules reference says so, and how to
have one checked. A tenant-scoped dependent that is tracked alongside its principal could
be compared by the write guard at no cost; that is left for a later proposal.
