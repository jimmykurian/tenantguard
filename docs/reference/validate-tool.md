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

## Requirements

- The app targets net10.0 or net11.0, and its build output holds `App.deps.json` and
  `App.runtimeconfig.json`, as `dotnet build` writes them.
- The startup assembly references `Microsoft.EntityFrameworkCore.Design`, as `dotnet ef`
  needs. The tool creates the context the way `dotnet ef` does (see below).
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
- TGM005 (warning): Invoice.TenantId is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.
TenantGuard validated AppDbContext: 1 error, 1 warning.
```

A model with no findings prints only the summary:
`TenantGuard validated AppDbContext: 0 errors, 0 warnings.` The output never includes
entity property values.

Findings and the summary go to standard output; a reason the tool couldn't run goes to
standard error. The app's own logging runs too, as it does under `dotnet ef`, so a host
with a console logger prints its lines among them.

## Exit codes

| Code | When |
| --- | --- |
| 0 | No errors, and no warnings under `--warnings-as-errors` |
| 1 | At least one error, or a warning under `--warnings-as-errors` |
| 2 | The tool couldn't validate: a bad option, an assembly or its `.deps.json` not found, no context or more than one without `--context`, a context it couldn't create, a version mismatch, or a compiled model |

For exit code 2, the message says what failed and what to do, such as
`The assembly has 2 DbContext types: AppDbContext, AuditDbContext. Pass --context to pick one.`
