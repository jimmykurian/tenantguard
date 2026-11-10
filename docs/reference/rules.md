# Model rules

`UseTenantGuard` adds a convention that runs these rules when EF Core finalizes the model,
the first time a context of that type is used. An error throws
`TenantGuardModelException`, listing every error, under `Enforcement.Throw` (the default),
and is logged at Error level under `Enforcement.Log`. A warning is always logged.

EF Core builds the model lazily, so register `AddTenantGuardStartupValidation<TContext>()`
to build it when the host starts: a model that breaks a rule then stops the host before it
serves a request. `TenantGuardModel.Validate(model, options)` runs the same rules and
returns the findings without throwing, for tests and tooling. See [options](options.md).

A compiled model, passed to `UseModel`, isn't built at runtime, so the rules don't run on
it and report nothing. The write guard still works: it finds the tenant-scoped entity
types itself. On EF Core 10 and 11, EF Core refuses to generate a compiled model for a model
with a query filter (behavior pin 9), so a tenant-isolated app can't use one yet; see
[compatibility](compatibility.md#compiled-models-and-native-aot).

## Which entity types are tenant-scoped

For each entity type, the tenant property is the first match of:

1. An explicit registration: `tg.TenantProperty<Invoice>(invoice => invoice.OrgId)`.
2. A property marked `[TenantKey]`.
3. Finbuckle's `Finbuckle:MultiTenant` annotation, which `IsMultiTenant()` adds, with
   Finbuckle's `TenantId` property. It's matched by name, so the runtime package needs no
   Finbuckle reference; `tests/e2e/Finbuckle.Api.Tests` checks the name against Finbuckle's
   `Constants.MultiTenantAnnotationName`.
4. A CLR or shadow property named in `TenantPropertyNames`, `TenantId` by default.

An entity type with a tenant property is tenant-scoped. Only the root of an inheritance
hierarchy is examined, because EF Core allows query filters only on the root; derived
types share it. Owned types are skipped: their rows belong to their owner (TGM008).
Keyless entity types are examined like any other. `Shared<TEntity>()` declares an entity
type tenant-free on purpose, which clears TGM011 and TGM014 for it, and TGM002 under
`RequireAllEntitiesScoped`. It applies only to a type with no tenant property: one that has
a tenant property stays tenant-scoped, and TGM001 still applies to it.

Tenant data that is cross-tenant on purpose is declared, each with a reason
([cross-tenant data](options.md#cross-tenant-data)):

- `Unfiltered<TEntity>` keeps a type tenant-scoped and leaves out the rules about its
  filter: TGM001, TGM003, TGM004, TGM007, TGM012 and TGM013.
- `CrossTenant`, and `CrossTenantWhenNoTenant` for the model of a context with no current
  tenant, skip every rule for the model.
- `NullTenantIsHost` leaves out TGM005.
- `Suppress` and `Suppress<TEntity>` leave out one rule, for every type (warnings only) or
  for one. A suppressed finding goes to `ModelReport.Suppressed` instead of `Findings`.

The table of tenants, filtered on its own key, is tenant-scoped once its key is named as
its tenant property, `TenantProperty<Club>(club => club.Id)`: TGM001 checks its filter, and
TGM002 doesn't report it.

## Rules

| ID | Name | Severity | Reported when |
|---|---|---|---|
| TGM001 | unguarded-entity | error | A tenant-scoped entity type has no query filter that references its tenant property |
| TGM002 | unscoped-entity | error, only with `RequireAllEntitiesScoped` | An entity type has no tenant property and isn't declared shared |
| TGM003 | captured-value | error | The tenant filter compares the tenant property to a value captured when the model was built |
| TGM004 | literal-tenant | error | The tenant filter compares the tenant property to a constant |
| TGM005 | nullable-tenant | warning | The tenant property is nullable |
| TGM006 | tenant-on-derived | error | A derived type declares a tenant property and its root has none |
| TGM007 | filter-not-parameterized | warning | The tenant filter's other operand can't be classified, or the filter never compares the tenant property |
| TGM008 | owned-tenant | warning | An owned type has a tenant property |
| TGM009 | unindexed-tenant | warning | No index or key of a tenant-scoped entity type starts with its tenant property |
| TGM010 | tenant-free-unique | error | A unique index, alternate key or natural primary key of a tenant-scoped entity type doesn't include its tenant property |
| TGM011 | tenant-free-view | warning | An entity type mapped to a view, SQL query or function has no tenant property and isn't declared shared |
| TGM012 | switchable-filter | warning | The tenant filter is ORed with a condition that doesn't read the row, so it can be switched off |
| TGM013 | open-without-tenant | warning | The tenant filter is ORed with a null check of the current tenant, so it returns every row when there is none |
| TGM014 | unscoped-dependent | warning | An entity type with no tenant property and no filter that reaches one belongs to a tenant-scoped principal through a required foreign key |

TGM011 and TGM014 aren't reported under `RequireAllEntitiesScoped`, where TGM002 reports the
same types as errors.

### TGM001 unguarded-entity

```text
{Entity} has tenant property '{Property}' but no query filter references it. Add HasQueryFilter(e => e.{Property} == <current tenant>) on the root type.
```

Without a filter, every query returns every tenant's rows. A filter that reaches the
tenant property through a navigation, such as `post => post.Blog.TenantId == TenantId`,
counts: the row is scoped through its principal. So do named filters. A filter that is
always true counts as no filter, such as `true || e.TenantId == null`, which a filter built
by hand from the tenant can become when the model is built with no tenant (see
[how the operand is classified](#how-the-operand-is-classified)).

```csharp
// TGM001
modelBuilder.Entity<Invoice>();

// Passes: the filter reads a member of the context, evaluated for each query.
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
```

### TGM002 unscoped-entity

```text
{Entity} has no tenant property and is not declared shared. Add '{Property}', or declare it with Shared<{Entity}>().
```

Only under `RequireAllEntitiesScoped()`, which asks for every entity type to be either
tenant-scoped or shared on purpose. `{Property}` is the first of `TenantPropertyNames`.

```csharp
options.UseTenantGuard(tg => tg
    .RequireAllEntitiesScoped()
    .Shared<Country>());   // a lookup table every tenant reads
```

### TGM003 captured-value

```text
{Entity}: the tenant filter captures a local value. The model is cached per context type, so every tenant will see the first tenant's rows. Reference a DbContext member instead.
```

EF Core builds the model once per context type and caches it. A filter that reads a local
variable, from `OnModelCreating` or an `IEntityTypeConfiguration`, keeps the value it had
for the first context, so every later tenant gets the first tenant's rows. Behavior pin 4
shows the leak.

```csharp
// TGM003: tenant is read once, when the model is built.
var tenant = TenantId;
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == tenant);

// Passes: EF Core reads TenantId from the context running each query.
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
```

A captured variable whose type the context implements, such as the
`IMultiTenantDbContext` Finbuckle's filter captures, isn't TGM003: EF Core puts the
current context in its place, so it is read per query (behavior pin 5).

An app whose `IModelCacheKeyFactory` keys the model cache by tenant builds one model per
tenant, and captures the tenant on purpose: `AllowCapturedValues()` leaves TGM003 out when
the context's cache key passes the check in [per-tenant models](options.md#per-tenant-models)
(behavior pin 8).

### TGM004 literal-tenant

```text
{Entity}: the tenant filter compares '{Property}' to the constant {Value}. Reference a DbContext member instead.
```

A filter fixed to one tenant, such as `invoice.TenantId == "acme"`, shows that tenant's
rows to everyone. `{Value}` is the constant, with strings quoted.

### TGM005 nullable-tenant

```text
{Entity}.{Property} is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.
```

A row whose tenant is null matches no tenant's filter, and the write guard has no tenant
to compare it with. Make the property required unless rows without a tenant are meant.
When a null tenant means host data, `NullTenantIsHost()` says so, and TGM005 isn't
reported ([host rows](options.md#host-rows)).

### TGM006 tenant-on-derived

```text
{Entity} declares '{Property}' but filters can only be defined on the root type {Root}. Move the property to the root.
```

EF Core puts query filters on the root of a hierarchy only, so a tenant property on a
derived type can't be filtered.

### TGM007 filter-not-parameterized

```text
{Entity}: the tenant filter's operand could not be classified; confirm it is evaluated per query.
```

The filter references the tenant property, but TenantGuard can't tell where the value it
is compared with comes from. Check that it is read per query, from the context or a static
member, and not frozen when the model was built. It is also reported when the filter
reads the tenant property without comparing it with anything but `null`, as
`e => e.TenantId != null` alone does.

### TGM008 owned-tenant

```text
{Owned} is owned by {Owner}; its '{Property}' is ignored, isolation follows the owner.
```

An owned type is stored with its owner and filtered through it, so its own tenant
property plays no part. The ownership key isn't a tenant property: EF Core names a shadow
one `{Owner}{OwnerKey}`, so a type owned by an entity named `Tenant` gets a `TenantId` that
refers to its owner, and TGM008 skips it, shadow or declared. The write guard skips owned entries, so it checks the owner's
tenant only when the owner's own row is written: a save that changes only the owned part
of another tenant's row isn't blocked yet.

### TGM009 unindexed-tenant

```text
{Entity} has no index or key that starts with '{Property}', so each query its tenant filter scopes reads past other tenants' rows. Add HasIndex(e => e.{Property}), or put '{Property}' first in an existing index or key.
```

Every query of a tenant-scoped table filters on the tenant column, so without an index
that starts with it the database scans every tenant's rows. The primary key, alternate
keys and indexes all count, including the ones EF Core adds for foreign keys, such as the
index a foreign key of `(TenantId, OrderId)` gets.

```csharp
// TGM009: the only key is Id, and no index starts with TenantId.
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);

// Passes.
modelBuilder.Entity<Invoice>(invoice =>
{
    invoice.HasQueryFilter(row => row.TenantId == TenantId);
    invoice.HasIndex(row => row.TenantId);
});
```

### TGM010 tenant-free-unique

```text
{Entity}: the unique index on ({Columns}) doesn't include '{Property}', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasIndex(e => new { e.{Property}, {Members} }).IsUnique().
```

```text
{Entity}: the alternate key ({Columns}) doesn't include '{Property}', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasAlternateKey(e => new { e.{Property}, {Members} }).
```

```text
{Entity}: the primary key ({Columns}) doesn't include '{Property}' and its values aren't generated, so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasKey(e => new { e.{Property}, {Members} }).
```

A unique index on `InvoiceNumber` alone is unique across all tenants: one tenant's insert
fails because another tenant already used the number, and the failure tells the first
tenant that the number exists. `{Columns}` lists the index's properties, comma-separated,
and `{Members}` the same properties as `e.{Name}`.

The primary key is checked when it is a natural key: none of its properties has values
generated, by the database or by EF Core (`ValueGenerated.Never`), such as an external
login's `(LoginProvider, ProviderKey)`. A second tenant then can't link an external account
the first tenant already linked, and the refusal tells it the account exists. A generated
key, such as an identity column or a `Guid` EF Core generates, is unique across tenants by
design, and isn't checked. Nor is a key of a single `Guid` property the app assigns: a
random `Guid` can't be reused by another tenant. A `Guid` that comes from outside, such as
an identity provider's object ID, can; for that, make the key per tenant,
`HasKey(e => new { e.TenantId, e.Id })`, or keep the external ID in a column of its own
behind a generated key, with a unique index on `(TenantId, ExternalId)`. A unique index on
`(TenantId, Id)` alone doesn't help: the key on `Id` is still unique across tenants.

Nor is the unique index EF Core makes for
a one-to-one foreign key, whose values are keys of the principal's rows, or a unique index
or alternate key that includes every column of a foreign key to a tenant-scoped entity
type, such as `(ProjectId, Name)` or a join table's `(ProjectId, LabelId)`, a primary key
included: each principal row belongs to one tenant, so another tenant can't use the same
values. That covers a dependent that shares its principal's key in a one-to-one
relationship. A foreign key to an
entity type with no tenant property, such as a shared `Country`, doesn't count. A unique
index with a filter, set with `HasFilter`, is checked like any other.

```csharp
// TGM010
modelBuilder.Entity<Invoice>().HasIndex(invoice => invoice.InvoiceNumber).IsUnique();

// Passes, and also satisfies TGM009.
modelBuilder.Entity<Invoice>().HasIndex(invoice => new { invoice.TenantId, invoice.InvoiceNumber }).IsUnique();

// TGM010: a natural key, unique across tenants.
modelBuilder.Entity<ExternalLogin>().HasKey(login => new { login.LoginProvider, login.ProviderKey });

// Passes: unique per tenant.
modelBuilder.Entity<ExternalLogin>().HasKey(login => new { login.TenantId, login.LoginProvider, login.ProviderKey });
```

### TGM011 tenant-free-view

```text
{Entity} is mapped to {Source} and has no tenant property, so it returns every tenant's rows. Select '{Property}' in it and filter on it, or declare it with Shared<{Entity}>().
```

`{Source}` is `the view '{Name}'`, `a SQL query` or `the function '{Name}'`, from the
relational mapping (`ToView`, `ToSqlQuery`, `ToFunction`), and `{Property}` is the first of
`TenantPropertyNames`. A view that drops the tenant column reads the rows of every tenant
in the tables it selects from, and it has no tenant property, so no other rule looks at it.
TenantGuard can't see which tables a view reads, so it reports every view with no tenant
property in a model that has a tenant-scoped entity type; a lookup view that every tenant
reads is declared with `Shared<TEntity>()`. Keyless entity types and ones with a key are
reported alike. Not reported when the provider isn't relational
([which tables](#which-tables-tgm009-and-tgm010-check) says how TenantGuard tells).

```csharp
// TGM011
modelBuilder.Entity<InvoiceTotal>().HasNoKey().ToView("invoice_totals");

// Passes: the view selects the tenant column, and the type filters on it.
modelBuilder.Entity<InvoiceTotal>(total =>
{
    total.HasNoKey().ToView("invoice_totals");
    total.HasQueryFilter(row => row.TenantId == TenantId);
});
```

### TGM012 switchable-filter

```text
{Entity}: the tenant filter can be switched off: it is ORed with a condition that doesn't read the row, and while that condition is true, every tenant's rows are returned. Confirm it is true only on purpose.
```

Reported when the tenant comparison sits under an `||` (or `|`), at any depth, whose other
side doesn't read the filter's parameter, such as a flag on the context:
`!IsTenantFilterEnabled || e.TenantId == TenantId`. That is how some frameworks, ABP among
them, switch the filter off for a scope. The switch is read per query, so it may well be
fine; the warning says the filter can be turned off, and where. A condition on the row,
such as `e.IsPublic || e.TenantId == TenantId`, isn't reported: it shows the rows it
selects, not every row. Under a framework that switches its filters this way, one
`Suppress("TGM012", reason)` covers the model.

TenantGuard first pushes each `!` inward, reads a bool `|` or `&` as `||` or `&&`, and reads
a comparison with a bool constant as the condition or its negation (`x == true` is `x`,
`x == false` is `!x`), so an `||` counts only when it is still an `||` with no negation
above it. A constant `false` beside the tenant comparison, as `false | x` leaves, is no
switch. `!(BlockAll || e.TenantId != TenantId)` is
`!BlockAll && e.TenantId == TenantId`, which only ever narrows the rows, and isn't
reported; `!(Enabled && e.TenantId != TenantId)` is `!Enabled || e.TenantId == TenantId`,
and is.

A chain of `||`, however it is grouped, is read as one list of conditions, so
`(a || b) || e.TenantId == TenantId` and `a || (b || e.TenantId == TenantId)` are the same
filter. Each condition in the list that doesn't read the row is a switch of its own. One
that is a null check of the current tenant is TGM013, and every other one is TGM012, so a
filter with both, such as `!IsTenantFilterEnabled || (CurrentTenantId == null || e.TenantId == CurrentTenantId)`,
gets both, and suppressing one leaves the other. A switch isn't reported when an `&&` above
it has, on its other side, the opposite of the switch's condition, which rules the switch
out: `Enabled && (!Enabled || e.TenantId == TenantId)`, or
`CurrentTenantId != null && (CurrentTenantId == null || e.TenantId == CurrentTenantId)`. The
opposite is matched by its shape, the same member chain with the negation pushed in, `==`
and `!=` swapped, `HasValue` and `!HasValue`, or `string.IsNullOrEmpty` and its negation.
TenantGuard doesn't reason further about guards, so a switch ruled out some other way is
still reported, and is suppressed with its reason.

```csharp
// TGM012
modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => !IsTenantFilterEnabled || invoice.TenantId == TenantId);
```

### TGM013 open-without-tenant

```text
{Entity}: the tenant filter returns every tenant's rows when there is no current tenant: it is ORed with a null check of the value '{Property}' is compared with. Compare '{Property}' with the current tenant alone, so a context with no tenant sees no tenant's rows.
```

Reported for a switch, found as TGM012 finds one, whose other side is a null check of the
expression the tenant property is compared with, so
`!(CurrentTenantId == null || e.TenantId != CurrentTenantId)`, which shows no rows when there
is no tenant, isn't reported. The null checks are `== null`, `!HasValue`, `string.IsNullOrEmpty` or
`string.IsNullOrWhiteSpace`. `CurrentTenantId == null || e.TenantId == CurrentTenantId`
returns every row whenever the current tenant is null: in background jobs, on routes
outside a tenant, in a context created without one. Under `NullTenantIsHost()`, the host is
the context with no current tenant, and the same filter shows the host every tenant's rows
rather than its own. When seeing every tenant with no current tenant is meant, as for an
admin context, say so with `Suppress("TGM013", reason)`, or `Suppress<TEntity>` for one
type.

```csharp
// TGM013
modelBuilder.Entity<Booking>().HasQueryFilter(booking => CurrentTenantId == null || booking.ClubId == CurrentTenantId);

// Passes: with no current tenant, no tenant's rows.
modelBuilder.Entity<Booking>().HasQueryFilter(booking => booking.ClubId == CurrentTenantId);
```

With no current tenant, the filter that passes returns no tenant's rows. When the tenant
property is nullable, EF Core's null semantics make `booking.ClubId == CurrentTenantId`
true for a row whose `ClubId` is null too, so it returns the rows that have no tenant:
under `NullTenantIsHost()`, the host's rows, and otherwise rows TGM005 warns about.

### TGM014 unscoped-dependent

```text
{Entity} has no tenant property and no query filter that reaches one, but its rows belong to tenant-scoped {Principal} through the required foreign key ({Columns}), so Set<{Entity}>() returns every tenant's rows. Filter it through {Principal}, give it '{Property}' and a filter, or declare it with Shared<{Entity}>().
```

A line item or a registration reached only through its tenant-scoped principal is still
an entity type: `Set<OrderLine>()`, a `DbSet` or a raw query returns every tenant's rows.
Reported for an entity type that isn't owned, isn't tenant-scoped and isn't declared
shared, when it has a required foreign key to a tenant-scoped entity type and no query
filter on its root reaches a tenant property through a navigation. A filter through the
principal counts, as it does for TGM001: `line => line.Order.TenantId == TenantId`, or with
`EF.Property<string>(line.Order, "TenantId")`, which a shadow tenant property needs. The
filter is folded first, so one built as `true || line.Order.TenantId == TenantId`, which
admits every row, doesn't count. The
join entity type EF Core creates for a many-to-many relationship isn't reported: it has no
CLR type of its own. When more than one foreign key qualifies, the message names the first,
by principal name.

```csharp
// TGM014
modelBuilder.Entity<OrderLine>().HasOne(line => line.Order).WithMany(order => order.Lines).IsRequired();

// Passes: filtered through its order.
modelBuilder.Entity<OrderLine>().HasQueryFilter(line => line.Order.TenantId == TenantId);
```

### Which tables TGM009 and TGM010 check

Both rules check the indexes and keys declared anywhere in the hierarchy, so an index on a
derived type in a table-per-hierarchy mapping counts. They skip keyless entity types and
the ones mapped to a view, a SQL query or a function, since the database holds no index of
theirs, and every entity type when the provider isn't relational, such as Cosmos DB. When EF Core
builds the model, TenantGuard tells a relational provider by its options extension, which
derives from `RelationalOptionsExtension`; `TenantGuardModel.Validate` tells by the
`Relational:` annotations a relational provider puts on each entity type of the model
`context.Model` returns. Both are matched by name, so the runtime package needs no
relational reference. TGM011 checks the types these rules skip, the ones mapped to a view,
a SQL query or a function, and tells the provider the same way.

## How the operand is classified

TGM003, TGM004 and TGM007 look at what the tenant property is compared with: in `==`,
`!=`, `Equals` and `Contains`, against a member of the filter's parameter, or
`EF.Property<T>(e, "{Property}")`, that resolves to the tenant property. The name in
`EF.Property` can be a constant or a captured `string`, such as a local or a helper method's
parameter, read when the model is built. Then TenantGuard follows the other operand's member
and call chain to its root:

- An expression whose type the context type derives from or implements, other than
  `object`: the context, which passes. EF Core puts the current context in its place.
  `TenantGuardModel.Validate` doesn't know the context type, so it counts a captured value
  that is a context, or a captured null of an interface or `DbContext` type, as the
  context.
- A constant that is a `DbContext`: the context, which passes.
- A constant of a compiler-generated closure type (marked `CompilerGeneratedAttribute`, or
  named like `<>c__DisplayClass`): a captured local, TGM003.
- A constant primitive, string, `Guid` or enum: TGM004.
- A static member, or a static method call, extension methods included: passes. The
  call's arguments aren't examined.
- Anything else: TGM007.

A comparison with `null`, such as `e.TenantId != null`, isn't a tenant comparison, so it
is skipped; a filter that has no other comparison of the tenant property is TGM007.

Before it looks, TenantGuard folds the `bool` constants in the filter, such as
`Expression.Constant(tenant == null)` in a filter built with the `Expression` API, which
are fixed when the model is built: `true || x` is `true`, and `false || x` and `true && x`
are `x`. So `false || e.TenantId == "acme"` is TGM004, and `true || e.TenantId == null`
filters nothing and references no tenant property, so it is TGM001. `false && x` is left
as it is: it hides every row, and leaks none.

TGM012 and TGM013 look at the folded filter too, so a switch that is a constant when the
model is built, such as `Expression.Constant(false) || x`, is no switch. TGM013's null check
matches when it reads the same member chain as the operand the tenant property is compared
with, such as `CurrentTenantId` on the context in both.
