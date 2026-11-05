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
type tenant-free on purpose, which matters under `RequireAllEntitiesScoped` (TGM002). It
applies only to a type with no tenant property: one that has a tenant property stays
tenant-scoped, and TGM001 still applies to it.

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
| TGM010 | tenant-free-unique | error | A unique index or alternate key of a tenant-scoped entity type doesn't include its tenant property |

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

A unique index on `InvoiceNumber` alone is unique across all tenants: one tenant's insert
fails because another tenant already used the number, and the failure tells the first
tenant that the number exists. `{Columns}` lists the index's properties, comma-separated,
and `{Members}` the same properties as `e.{Name}`. The primary key isn't checked: a
generated key is unique across tenants by design. Nor is the unique index EF Core makes for
a one-to-one foreign key, whose values are keys of the principal's rows, or a unique index
or alternate key that includes every column of a foreign key to a tenant-scoped entity
type, such as `(ProjectId, Name)` or a join table's `(ProjectId, LabelId)`: each principal
row belongs to one tenant, so another tenant can't use the same values. A foreign key to an
entity type with no tenant property, such as a shared `Country`, doesn't count. A unique
index with a filter, set with `HasFilter`, is checked like any other.

```csharp
// TGM010
modelBuilder.Entity<Invoice>().HasIndex(invoice => invoice.InvoiceNumber).IsUnique();

// Passes, and also satisfies TGM009.
modelBuilder.Entity<Invoice>().HasIndex(invoice => new { invoice.TenantId, invoice.InvoiceNumber }).IsUnique();
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
relational reference.

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
