# Analyzers

The analyzers flag, while you build, the code that reads or writes past the tenant filter.
`TenantGuard.EntityFrameworkCore` carries them, so installing it is enough.
`TenantGuard.Analyzers` ships them on their own, for a project that calls EF Core without
the runtime package. They target `netstandard2.0` and Roslyn 4.8, so they run in Visual
Studio 2022 17.8 and later and in the .NET 8 SDK and later.

| ID | Name | Severity | Reported when |
|---|---|---|---|
| TG001 | ignore-query-filters | warning | `IgnoreQueryFilters()` turns off the tenant filter |
| TG002 | raw-sql-without-tenant | warning | Raw SQL the compiler can read compares none of the tenant columns with a value |
| TG003 | missing-justification | error | `[AllowCrossTenantAccess]` has no reason |
| TG004 | raw-sql-unverifiable | warning | Raw SQL is built at runtime, so it can't be checked |
| TG005 | filter-switch | warning | A framework's own switch turns off the tenant filter, such as ABP's `IDataFilter.Disable<IMultiTenant>()` |

Change a severity in `.editorconfig` like any other diagnostic:
`dotnet_diagnostic.TG002.severity = error`.

## Configuration

Six `.editorconfig` keys, read for each file. Each takes a comma-separated list, except
`tenantguard.tenant_members_only` and `tenantguard.tenant_filter_unnamed`, which take
`true` or `false`, and a `#` comment after the value is ignored.

```ini
[*.cs]
tenantguard.tenant_column = TenantId           # the default
tenantguard.tenant_filter_names = TenantFilter # optional: the names of your tenant filters
tenantguard.tenant_filter_unnamed = true       # optional: your tenant filters have no name
tenantguard.raw_sql_methods = Acme.Data.Sql.Query, Acme.Data.Sql.Execute # optional
tenantguard.filter_switch_methods = Acme.Data.Filters.DisableTenant # optional
tenantguard.tenant_members_only = true         # optional: no shadow tenant properties
```

- `tenantguard.tenant_column`: the columns raw SQL has to compare with a value (TG002),
  matched ignoring case. Under `tenantguard.tenant_members_only`, also the members that make an
  entity type tenant-scoped (TG001). The default is `TenantId`.
- `tenantguard.tenant_filter_names`: the names of the query filters that hold the tenant
  predicate (TG001), matched exactly, case included. Set it if you use EF Core's named
  filters.
- `tenantguard.tenant_filter_unnamed`: `true` says your tenant filters have no name, as
  `HasQueryFilter(expression)` without one gives. EF Core doesn't put an unnamed filter
  beside named ones on an entity type, and `IgnoreQueryFilters` with names never turns an
  unnamed filter off (behavior pin 10), so TG001 doesn't report that overload. Matched ignoring case; the
  default is `false`. With `tenantguard.tenant_filter_names` set too, the names are checked
  as before, for the entity types whose tenant filter is named.
- `tenantguard.raw_sql_methods`: more methods that take raw SQL, checked by TG002 and
  TG004 beside the [built-in ones](#which-calls-take-raw-sql). Each entry is the
  containing type's full name, a dot and the method name, such as `Acme.Data.Sql.Query`,
  matched exactly, case included, and it covers every overload, generic or not, and
  extension methods called either way. A nested type is written with dots:
  `Acme.Data.Outer.Inner.Query`.
- `tenantguard.filter_switch_methods`: more methods that turn the tenant filter off (TG005),
  beside the [built-in ones](#tg005-filter-switch). Each entry is written as for
  `tenantguard.raw_sql_methods`, optionally followed by one type argument's full name in
  angle brackets, such as `Acme.Data.Filters.Disable<Acme.Data.ITenantOwned>`. With a type
  argument, only calls with that type argument match: the method's own, or else its
  containing type's. A call matches through the method itself, or the interface method it
  implements.
- `tenantguard.tenant_members_only`: `true` says every tenant property is a property or
  field of its entity's class, never a shadow property, and is named in
  `tenantguard.tenant_column` or marked `[TenantKey]`. Then TG001 can skip a query over a
  type with no tenant member. Matched ignoring case; the default is `false`. Don't set it
  with Finbuckle, whose `IsMultiTenant()` adds a shadow `TenantId`, or with any shadow
  tenant property, and add each name given to `TenantProperty<TEntity>(...)` to
  `tenantguard.tenant_column`.

## Which calls take raw SQL

TG002 and TG004 check the SQL handed to these calls. They're matched by full name, so the
analyzers take no dependency on Dapper or a database provider.

| Library | Calls | The SQL |
|---|---|---|
| EF Core | `ExecuteSqlRaw`, `ExecuteSqlInterpolated`, `ExecuteSql`, their `Async` forms, `SqlQueryRaw` and `SqlQuery` on `Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions` | the `sql` argument |
| Dapper | `Query`, `QueryFirst`, `QueryFirstOrDefault`, `QuerySingle`, `QuerySingleOrDefault`, `QueryMultiple`, `Execute`, `ExecuteScalar` and `ExecuteReader`, their `Async` forms, and `QueryUnbufferedAsync`, on `Dapper.SqlMapper` | the `sql` argument |
| ADO.NET | An assignment to `CommandText` on a type that derives from `System.Data.Common.DbCommand` or implements `System.Data.IDbCommand`, in a statement or an object initializer | the value assigned |
| ADO.NET | A constructor of such a type whose first parameter is a `string` named `cmdText` or `commandText`, such as `new SqlCommand(sql, connection)`, `NpgsqlCommand` or `SqliteCommand` | that argument |
| `tenantguard.raw_sql_methods` | Each method listed | the first parameter of type `string` or `System.FormattableString`, after the receiver of an extension method |

A call whose SQL isn't in one of those places, such as Dapper's overloads that take a
`CommandDefinition`, or a `+=` on `CommandText`, isn't checked.

`FromSql`, `FromSqlRaw` and `FromSqlInterpolated` on an entity type are checked too, under
`tenantguard.tenant_members_only = true`, when the entity type has no tenant member, as
TG001 decides it: EF Core applies the query filters over them (behavior pin 3), but such a
type has no tenant filter to apply. Without the key, the analyzers can't tell, so they're
left alone.

## Which SQL the compiler can read

TG002 checks SQL the compiler can read; TG004 reports the rest. Readable SQL is:

- a constant: a literal, a `const`, or constants joined with `+`;
- an interpolated string, whose literal parts and constant holes, such as
  `nameof(Invoice.TenantId)`, are its text, and whose other holes are values;
- a local variable whose declaration sets it to readable SQL, and that is never assigned
  again, passed by `ref` or `out`, or written in a lambda or local function, such as
  `var sql = """SELECT ...""";` passed on as `sql`.

A parameter, a field, or SQL joined from runtime values is built at runtime, so it is
TG004.

The text check doesn't know what the SQL means. A stored procedure's name passed to Dapper
with `CommandType.StoredProcedure`, or a `SELECT 1` health check, compares no tenant
column, so it is TG002: justify the method.

## Justifying cross-tenant code

A method or class that crosses tenants on purpose says why:

```csharp
[AllowCrossTenantAccess("billing totals for every tenant, run by the nightly job")]
public async Task<decimal> TotalForAllTenantsAsync(AppDbContext db)
    => await db.Invoices.IgnoreQueryFilters().SumAsync(invoice => invoice.Total);
```

The attribute counts on the method, on an enclosing lambda or local function, and on any
containing class, and it clears TG001, TG002, TG004 and TG005 there. It counts only with a
reason: one with a null, empty or whitespace reason clears nothing and is TG003. The
analyzers match it by its full name, `TenantGuard.AllowCrossTenantAccessAttribute`, so a
project with only `TenantGuard.Analyzers` can declare its own.

## TG001 ignore-query-filters

```text
IgnoreQueryFilters disables the tenant filter. Add [AllowCrossTenantAccess("why")] to the method, or ignore only the non-tenant filters by name.
```

Reported on `IgnoreQueryFilters()`. The overload that takes filter names, from EF Core 10,
is reported too, unless `tenantguard.tenant_filter_names` is set and the names are
constants that leave every tenant filter on. With only `tenantguard.tenant_filter_unnamed =
true` and no names set, that overload isn't reported, whatever the names; with both keys
set, the names are checked as for `tenantguard.tenant_filter_names` alone. Constant names are an array, a collection
initializer, or a collection expression without a spread, of string constants.

With `tenantguard.tenant_members_only = true`, it isn't reported when the query's entity
type has no tenant filter to turn off: a class or struct with no property or field named
in `tenantguard.tenant_column` or marked `[TenantKey]`, on itself or a base type, and none
on a type it reaches through reference navigations, followed one after another, as a
filter such as `post => post.Blog.TenantId == TenantId` does. Collection navigations don't
count. A navigation to an interface or a type parameter counts as reaching a tenant. A
query over a type parameter, an interface or `object` is still reported. Without the key,
the analyzer can't tell a type with a shadow tenant property from one with none, so every
call is reported.

```csharp
// TG001
var all = await db.Invoices.IgnoreQueryFilters().ToListAsync();

// With tenantguard.tenant_filter_names = TenantFilter, this one passes:
// it switches off only the soft-delete filter.
var deleted = await db.Invoices.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();

// With tenantguard.tenant_members_only = true, this one passes:
// Company has no TenantId, so its only filter is soft delete.
var exists = await db.Companies.IgnoreQueryFilters().AnyAsync(company => company.Slug == slug);
```

The diagnostic underlines the call, from the method name to its closing parenthesis.

## TG002 raw-sql-without-tenant

```text
Raw SQL bypasses query filters and does not compare '{Column}' with a value. Add a tenant predicate, or [AllowCrossTenantAccess("why")].
```

Reported on the SQL handed to a [call that takes raw SQL](#which-calls-take-raw-sql),
when the compiler [can read it](#which-sql-the-compiler-can-read) and it compares none of
`tenantguard.tenant_column` with a value. With several columns, `{Column}` lists them:
`does not compare 'TenantId' or 'OrgId' with a value`.

The column counts as a whole identifier, matched ignoring case: bare, quoted as
`"TenantId"`, `[TenantId]` or `` `TenantId` ``, and qualified or not, as `i.TenantId` or
`[dbo].[Invoices].[TenantId]`. A name it is only part of, such as `OwningTenantId`, or a
parameter named like it, such as `@TenantId`, isn't the column. It passes when it is:

- compared with `=` to a value, on either side, or with `= ANY (` a value `)`;
- followed by `IN`, then a value or a parenthesized list whose first item is a value;
- in the column list of an `INSERT INTO ... (...)`.

A value is a parameter (`@name`, `:name`, `$name`, `$1` or `?`), a composite-format
placeholder such as `{0}`, which `ExecuteSqlRaw` and `SqlQueryRaw` turn into parameters, an
interpolated hole that isn't a constant, a string literal or a number. A column on the other side, as in a join's
`a.TenantId = b.TenantId`, or a subquery, isn't a value, and neither `<>`, `!=`, `<=` nor
`>=` is a comparison that scopes the rows.

```csharp
// TG002
await db.Database.ExecuteSqlRawAsync("DELETE FROM Invoices WHERE Paid = 1");
var unpaid = await connection.QueryAsync<Invoice>("SELECT * FROM Invoices WHERE Paid = 0");
command.CommandText = "DELETE FROM Invoices WHERE Paid = 1";

var report = await connection.QueryAsync<Row>("SELECT TenantId, COUNT(*) FROM Invoices GROUP BY TenantId");
var lines = await connection.QueryAsync<Line>("SELECT l.* FROM Lines l JOIN Invoices i ON i.TenantId = l.TenantId");

// Passes
await db.Database.ExecuteSqlAsync($"DELETE FROM Invoices WHERE Paid = 1 AND TenantId = {tenantId}");
var mine = await connection.QueryAsync<Invoice>("SELECT * FROM Invoices WHERE TenantId = @tenantId", new { tenantId });
var sql = """
    SELECT * FROM "Invoices" WHERE "TenantId" IN @tenants
    """;
var ours = await connection.QueryAsync<Invoice>(sql, new { tenants });
await connection.ExecuteAsync("INSERT INTO Invoices (TenantId, Number) VALUES (@tenantId, @number)", invoice);
```

The check reads the SQL text; it doesn't parse SQL. A comparison anywhere passes, in any
statement of a batch, so an `UPDATE` that sets `TenantId = @tenantId` but filters on
something else passes too: justify or fix what the text check can't tell apart. See [which
calls take raw SQL](#which-calls-take-raw-sql) for `FromSql` on an entity type.

## TG003 missing-justification

```text
AllowCrossTenantAccess needs a reason.
```

Reported on an `[AllowCrossTenantAccess]` whose reason is null, empty or whitespace, or
that has none.

## TG004 raw-sql-unverifiable

```text
Raw SQL built at runtime cannot be checked for a tenant predicate.
```

Reported on SQL the compiler [can't read](#which-sql-the-compiler-can-read), such as a
parameter or a string joined from runtime values, handed to a [call that takes raw
SQL](#which-calls-take-raw-sql). It's a warning, so a build shows each piece of SQL
TenantGuard can't check: check the SQL by hand and justify the method, or, in an app that
builds SQL at runtime on purpose, lower it: `dotnet_diagnostic.TG004.severity = suggestion`.

## TG005 filter-switch

```text
{Method} turns off the tenant filter. Add [AllowCrossTenantAccess("why")] to the method.
```

Reported on a call to a method that turns the tenant filter off through the framework
rather than `IgnoreQueryFilters`, as ABP's data filters do. `{Method}` is the method as
the entry names it, with its type argument: `IDataFilter.Disable<IMultiTenant>`. Built in,
matched by full name so the analyzers take no dependency on ABP:

| Framework | Call |
| --- | --- |
| ABP | `Volo.Abp.Data.IDataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>`, which covers `IDataFilter.Disable<IMultiTenant>()` and `IDataFilter<IMultiTenant>.Disable()` |

`tenantguard.filter_switch_methods` adds more. `IDataFilter.Disable<ISoftDelete>()` and other
filters' switches aren't reported. The diagnostic underlines the call, from the method name
to its closing parenthesis, as TG001's does.

```csharp
// TG005
using (dataFilter.Disable<IMultiTenant>())
{
    var all = await invoices.GetListAsync();
}

// Passes: only the soft-delete filter is off.
using (dataFilter.Disable<ISoftDelete>())
{
    var deleted = await invoices.GetListAsync();
}
```
