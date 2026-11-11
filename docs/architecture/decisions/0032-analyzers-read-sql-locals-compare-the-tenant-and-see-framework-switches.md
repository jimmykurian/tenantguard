# 0032. Analyzers read SQL in locals, compare the tenant, and see framework switches

- Status: Proposed
- Date: 2026-11-11

## Context

The field trial (#28) found that the analyzers' raw-SQL and filter checks don't fit common
code (#71):

1. On a Dapper app, every query was written as `var sql = """...""";` and passed straight
   in, so each was TG004, at info level, and a normal build showed nothing. Reading the local
   as its SQL would have passed 13 of 14 and found one real TG002.
2. TG002 passes SQL that mentions the tenant column anywhere, even as part of another name:
   `OwningTenantId` satisfies `TenantId`. A query that selects, groups or joins on the
   column without scoping the rows passes too.
3. `IgnoreQueryFilters(["SoftDelete"])` is TG001 unless `tenantguard.tenant_filter_names` is
   set, and an app whose tenant filter has no name has nothing to set it to. The trial set a
   name no filter used. On EF Core 10, an unnamed filter can't sit beside named ones on an
   entity type, and `IgnoreQueryFilters` with names never turns an unnamed filter off. Both
   were checked on EF Core 10. Finbuckle 10 names its filter `__tenant__`, so this is about
   filters written as `HasQueryFilter(expression)`.
4. TG002 leaves `FromSqlRaw` on an entity type alone, because EF Core applies the query
   filters over it. A type with no tenant filter has nothing applied.
5. ABP turns its tenant filter off with `IDataFilter.Disable<IMultiTenant>()`, through a
   service rather than `IgnoreQueryFilters`, so no rule sees it.

The analyzer IDs, their messages, default severities and `.editorconfig` keys are the
contract ([0006](0006-the-public-contract.md)), so this needs a decision.

## Decision

- **A local whose declaration sets it to readable SQL** is read as that SQL, for TG002 and
  TG004, when it is never assigned again, passed by `ref` or `out`, or written in a lambda
  or local function.
- **TG002 requires a comparison with a value.** The tenant column has to appear as a whole
  identifier, bare, quoted or qualified, and be compared with a value: with `=` or
  `= ANY (...)`, on either side, or followed by `IN` and a value or a list whose first item
  is a value. A value is a parameter, a hole that isn't a constant, a string literal or a
  number. A column in an `INSERT`'s column list counts too, because the insert stamps the
  row. The message says "does not compare '{Column}' with a value". It is still a text check:
  a comparison anywhere in the text passes.
- **Under `tenantguard.tenant_members_only = true`,** TG002 and TG004 check `FromSql`,
  `FromSqlRaw` and `FromSqlInterpolated` on an entity type with no tenant member, as TG001
  decides it.
- **TG004 is a warning.** Once SQL in locals is read, what's left is SQL built at runtime,
  which a build should show.
- **`tenantguard.tenant_filter_unnamed = true`** says the tenant filters have no name, so
  TG001 doesn't report the overload of `IgnoreQueryFilters` that takes names. With
  `tenantguard.tenant_filter_names` set too, the names are checked as before.
- **A new warning, TG005 filter-switch,** is reported on a call that turns the tenant filter
  off through a framework. ABP's `Volo.Abp.Data.IDataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>`
  is built in, matched by full name, as Dapper is for TG002.
  `tenantguard.filter_switch_methods` adds more, written as `tenantguard.raw_sql_methods`
  entries, each with an optional type argument. `[AllowCrossTenantAccess]` clears it, as it
  clears the others.

## Alternatives considered

- **Only fix the substring match in TG002, and document that a mention anywhere passes.**
  That leaves the trial's miss, a query that selects or joins on the column, a pass.
- **Report the framework switch as TG001.** TG001's message is about `IgnoreQueryFilters`,
  and the two couldn't be configured apart.
- **Keep TG004 at info, and say so in the getting-started guide.** A build would still show
  nothing about SQL TenantGuard can't check.
- **Document a name for an unnamed filter in `tenantguard.tenant_filter_names`.** It works
  only with constant names, and it says something that isn't true about the model.

## Consequences

An app upgrading gets more warnings: TG002 on SQL that names the tenant column without
comparing it, such as a report that groups by it or a join on it, TG004 as a warning, and
TG005 on ABP's switch. Each is fixed or justified. Dapper code that keeps its SQL in locals
gets checked rather than reported as unverifiable.

The comparison check is still textual. It doesn't tell an `UPDATE ... SET TenantId =
@tenantId` from a `WHERE`, or one statement of a batch from another, and analyzers.md says
so.
