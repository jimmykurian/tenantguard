# The validate tool

`dotnet tenantguard validate` runs the [model rules](rules.md) on a compiled app's
`DbContext` and prints what they find, without starting the app or opening a database
connection. It's for a CI step that builds the app but has no host, test project or
connection string.

It ships as the .NET tool package `TenantGuard.Tool`:

```sh
dotnet tool install --global TenantGuard.Tool --prerelease
dotnet tenantguard validate --assembly src/App/bin/Release/net10.0/App.dll
```

Under [package source mapping](https://learn.microsoft.com/nuget/consume-packages/package-source-mapping),
`dotnet tool install --add-source` fails, because the added source has no mapping. Point it
at a `nuget.config` that maps `TenantGuard.*` to the source instead:
`dotnet tool install --global TenantGuard.Tool --prerelease --configfile nuget.config`.

## Requirements

- The app targets net10.0 or net11.0, and its build output holds `App.deps.json` and
  `App.runtimeconfig.json`, as `dotnet build` writes them.
- The startup assembly references `Microsoft.EntityFrameworkCore.Design`, at any patch of
  the app's EF Core major version, as `dotnet ef` needs. The tool creates the context the
  way `dotnet ef` does (see below).
- The tool's version is the version of `TenantGuard.EntityFrameworkCore` the app references.
  An app that doesn't reference it can be checked with any version, under the default
  options: the tool loads its own copy of the runtime package.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `--assembly <path>` | required | The assembly that holds the `DbContext` |
| `--startup-assembly <path>` | `--assembly` | The app's entry assembly, when the context lives in a class library |
| `--context <name>` | the only `DbContext` | The context's name or full name; needed when the assembly has more than one |
| `--warnings-as-errors` | off | Fails on warnings too |
| `--help` | | Prints the usage and exits 0 |

Arguments after `--` go to the app, as they do with `dotnet ef`: to
`IDesignTimeDbContextFactory<TContext>.CreateDbContext(args)`, or to the app's `Main` when
the context comes from its host.

## How it finds and creates the context

The tool runs the app's assembly under its own dependencies and runtime, with
`dotnet exec` and the app's `.deps.json` and `.runtimeconfig.json`, so the app's EF Core,
providers and TenantGuard are the ones that load. It then creates the context with EF
Core's `DbContextActivator`, which tries, in order:

1. An `IDesignTimeDbContextFactory<TContext>` in the startup assembly.
2. The app's host: the service provider from `CreateHostBuilder`, or from the
   `WebApplication` or `Host` that `Main` builds. The host is built, not started.
3. A parameterless constructor.

Building the model doesn't open a connection, so a placeholder connection string works.
When the host needs configuration that CI doesn't have, add a design-time factory.

## What it checks

The tool reads the options the context passes to `UseTenantGuard`, and runs the same rules,
with the same options and the context's type, that run when EF Core builds the model. A
context without `UseTenantGuard` is checked under the default options. `Enforcement.Log`
makes no difference: the tool reports errors either way.

A context whose options pass a compiled model to `UseModel` can't be checked, because the
rules don't run on a compiled model; the tool says so and exits 2.

## Output

Each finding is printed on its own line, errors first, in the format the model exception
uses, followed by a summary line:

```text
- TGM001: Customer has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.
- TGM005 (warning): Invoice.TenantId is nullable: a row with no tenant matches no tenant's filter, but a context with no current tenant can read it. Make 'TenantId' required, or declare host rows with NullTenantIsHost().
TenantGuard validated AppDbContext: 1 error, 1 warning.
```

A model with no findings prints only the summary:
`TenantGuard validated AppDbContext: 0 errors, 0 warnings.` The output never includes
entity property values.

[Declarations](options.md#cross-tenant-data) are printed too, so the exemptions show in CI.
Suppressed findings come after the others, with their reason, then one line lists the
unfiltered entity types, and the summary counts the suppressed findings when there are any.
Neither fails the run, `--warnings-as-errors` included:

```text
- TGM005 (warning): Invoice.TenantId is nullable: a row with no tenant matches no tenant's filter, but a context with no current tenant can read it. Make 'TenantId' required, or declare host rows with NullTenantIsHost().
- TGM009 (suppressed: a lookup table, read by key): Country has no index or key that starts with 'TenantId', so a query that only its tenant filter narrows, such as a list of a tenant's rows, scans every tenant's rows. Add HasIndex(e => e.TenantId), or put 'TenantId' first in an existing index or key; a table read only by its key doesn't need one.
Unfiltered on purpose: Membership (maps users to their tenants), OutboxMessage (dispatched for every tenant by one worker)
TenantGuard validated AppDbContext: 0 errors, 1 warning, 1 suppressed.
```

A context declared `CrossTenant`, or whose model is the no-tenant model under
`CrossTenantWhenNoTenant`, prints one line and exits 0:

```text
TenantGuard validated ReportingDbContext: declared cross-tenant (reports and data deletion across tenants), so no rules ran.
```

Findings and the summary go to standard output; a reason the tool couldn't run goes to
standard error. The app's own logging runs too, as it does under `dotnet ef`, so a host
with a console logger prints its lines among them. TenantGuard's own log of the model
check, its summary, findings and declarations, is left out while the tool runs, so each
prints once.

## Exit codes

| Code | When |
| --- | --- |
| 0 | No errors, and no warnings under `--warnings-as-errors` |
| 1 | At least one error, or a warning under `--warnings-as-errors` |
| 2 | The tool couldn't validate: a bad option, an assembly or its `.deps.json` not found, no context or more than one without `--context`, a context it couldn't create, a version mismatch, or a compiled model |

For exit code 2, the message says what failed and what to do, such as
`The assembly has 2 DbContext types: AppDbContext, AuditDbContext. Pass --context to pick one.`
When creating the context fails:

| Cause | Message |
| --- | --- |
| The app's `.deps.json` lists no `Microsoft.EntityFrameworkCore.Design` | `{Startup} doesn't reference Microsoft.EntityFrameworkCore.Design. Add it to the startup project, as dotnet ef needs, so the tool can create the context the way the app does.` |
| An assembly fails to load, such as an EF Core assembly at a version the app doesn't have, also inside the app's factory | `Couldn't load {Assembly}, which creating {Context} needs: {Error}` |
| The startup assembly, or the context's, has more than one `IDesignTimeDbContextFactory<T>` for a context, which makes EF Core fail for every context | `{Startup} has {Count} IDesignTimeDbContextFactory<{Context}> types: {Types}. Keep one, so the tool can create the context the way dotnet ef does.` |
| Anything else, with no design-time factory for the context in the startup assembly or the context's | `Couldn't create {Context}: {Error} Add an IDesignTimeDbContextFactory<{Context}> to the startup assembly, or make the app's host build without configuration CI doesn't have.` |
| Anything else, with one: the startup assembly's first, as EF Core looks there first | `Couldn't create {Context} through {Factory}: {Error}` |
