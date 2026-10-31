# Analyzers

The analyzers flag, while you build, the code that reads or writes past the tenant filter.
`TenantGuard.EntityFrameworkCore` carries them, so installing it is enough.
`TenantGuard.Analyzers` ships them on their own, for a project that calls EF Core without
the runtime package. They target `netstandard2.0` and Roslyn 4.8, so they run in Visual
Studio 2022 17.8 and later and in the .NET 8 SDK and later.

| ID | Name | Severity | Reported when |
|---|---|---|---|
| TG001 | ignore-query-filters | warning | `IgnoreQueryFilters()` turns off the tenant filter |
| TG002 | raw-sql-without-tenant | warning | Raw SQL the compiler can read mentions none of the tenant columns |
| TG003 | missing-justification | error | `[AllowCrossTenantAccess]` has no reason |
| TG004 | raw-sql-unverifiable | info | Raw SQL is built at runtime, so it can't be checked |

Change a severity in `.editorconfig` like any other diagnostic:
`dotnet_diagnostic.TG002.severity = error`.

## Configuration

Two `.editorconfig` keys, read for each file. Both take a comma-separated list, and a
`#` comment after the value is ignored.

```ini
[*.cs]
tenantguard.tenant_column = TenantId           # the default
tenantguard.tenant_filter_names = TenantFilter # optional: the names of your tenant filters
```

- `tenantguard.tenant_column`: the columns raw SQL has to mention (TG002), matched
  ignoring case. The default is `TenantId`.
- `tenantguard.tenant_filter_names`: the names of the query filters that hold the tenant
  predicate (TG001), matched exactly, case included. Set it if you use EF Core's named
  filters.

## Justifying cross-tenant code

A method or class that crosses tenants on purpose says why:

```csharp
[AllowCrossTenantAccess("billing totals for every tenant, run by the nightly job")]
public async Task<decimal> TotalForAllTenantsAsync(AppDbContext db)
    => await db.Invoices.IgnoreQueryFilters().SumAsync(invoice => invoice.Total);
```

The attribute counts on the method, on an enclosing lambda or local function, and on any
containing class, and it clears TG001, TG002 and TG004 there. It counts only with a
reason: one with a null, empty or whitespace reason clears nothing and is TG003. The
analyzers match it by its full name, `TenantGuard.AllowCrossTenantAccessAttribute`, so a
project with only `TenantGuard.Analyzers` can declare its own.

## TG001 ignore-query-filters

```text
IgnoreQueryFilters disables the tenant filter. Add [AllowCrossTenantAccess("why")] to the method, or ignore only the non-tenant filters by name.
```

Reported on `IgnoreQueryFilters()`. The overload that takes filter names, from EF Core 10,
is reported too, unless `tenantguard.tenant_filter_names` is set and the names are
constants that leave every tenant filter on. Constant names are an array, a collection
initializer, or a collection expression without a spread, of string constants.

```csharp
// TG001
var all = await db.Invoices.IgnoreQueryFilters().ToListAsync();

// With tenantguard.tenant_filter_names = TenantFilter, this one passes:
// it switches off only the soft-delete filter.
var deleted = await db.Invoices.IgnoreQueryFilters(["SoftDelete"]).ToListAsync();
```

The diagnostic underlines the call, from the method name to its closing parenthesis.

## TG002 raw-sql-without-tenant

```text
Raw SQL bypasses query filters and does not mention '{Column}'. Add a tenant predicate, or [AllowCrossTenantAccess("why")].
```

Reported on the SQL passed to `ExecuteSqlRaw`, `ExecuteSqlInterpolated` and `ExecuteSql`,
their `Async` forms, `SqlQueryRaw` and `SqlQuery`, when the SQL is one the compiler can
read and it mentions none of `tenantguard.tenant_column`, ignoring case. Readable SQL is a
constant (a literal, a `const`, or constants joined with `+`) or an interpolated string,
whose literal parts and constant holes, such as `nameof(Invoice.TenantId)`, are checked.
With several columns, `{Column}` lists them: `does not mention 'TenantId' or 'OrgId'`.

```csharp
// TG002
await db.Database.ExecuteSqlRawAsync("DELETE FROM Invoices WHERE Paid = 1");

// Passes
await db.Database.ExecuteSqlAsync($"DELETE FROM Invoices WHERE Paid = 1 AND TenantId = {tenantId}");
```

The check looks for the column's name in the SQL text; it doesn't parse SQL, so a mention
anywhere passes. `FromSql`, `FromSqlRaw` and `FromSqlInterpolated` on an entity type aren't
flagged: EF Core applies the query filters over them (behavior pin 3).

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

Reported on SQL the compiler can't read, such as a variable or a string joined from
runtime values, passed to the methods TG002 covers. It's information: check the SQL by
hand, or justify the method.
