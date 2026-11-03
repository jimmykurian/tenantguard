# 0024. A .NET tool package for model validation in CI

- Status: Proposed
- Date: 2026-11-03

## Context

Issue #20 asks for `dotnet tenantguard validate`: the model rules run on a compiled app's
`DbContext` in CI, with no host, test project or database. [0002](0002-runtime-depends-on-ef-core-only.md)
keeps the runtime package to EF Core alone, and the packages list has three packages. A tool
has to load the app's own EF Core, providers and TenantGuard, at the app's versions, and
create its context the way the app configures it.

## Decision

- A fourth package, `TenantGuard.Tool`, a .NET tool with the command `dotnet-tenantguard`
  (`dotnet tenantguard`). It has no public API. The [validate tool](../../reference/validate-tool.md)
  reference page is its contract.
- It runs in two processes, as `dotnet ef` does. The tool parses the options, then starts
  `dotnet exec` with the app's `.deps.json` and `.runtimeconfig.json` on a payload built for
  the app's target (net10.0 or net11.0), shipped inside the tool package. The payload loads
  in the app's dependency context, so no EF Core or provider version is the tool's own.
- The payload creates the context with `DbContextActivator` from
  `Microsoft.EntityFrameworkCore.Design`, which the app references, as `dotnet ef` requires,
  instead of reimplementing the factory and host lookup.
- The payload reads the context's TenantGuard options and runs the internal
  `TenantModelRules.Check(model, options, contextType)`, the call the EF convention makes,
  through `InternalsVisibleTo`. So the tool's version has to match the app's
  `TenantGuard.EntityFrameworkCore`, and it checks that first.
- The runtime package stays EF Core only. The tool depends on `Microsoft.EntityFrameworkCore.Design`
  (in the payload) and nothing else; options are parsed by hand, without a command-line
  library.

## Consequences

CI can check a model with one command and no database. The tool must be released at the
same version as the other packages, which it is, since all of them share `Version` in
`Directory.Build.props`. An app without `Microsoft.EntityFrameworkCore.Design` gets a clear
error asking for it. A version mismatch fails rather than running rules that differ from
the app's. Packages, the architecture tests and the release now cover four packages.
