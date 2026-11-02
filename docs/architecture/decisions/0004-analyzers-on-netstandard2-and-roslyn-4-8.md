# 0004. The analyzers target netstandard2.0 on Roslyn 4.8.0

- Status: Accepted
- Date: 2026-10-29

## Context

Roslyn loads analyzers into the compiler of whichever IDE or SDK builds the project, from
Visual Studio 2022 to the newest SDK, and only a `netstandard2.0` analyzer loads in all of
them. An analyzer compiled against a newer Roslyn than the host's doesn't load.

## Decision

`TenantGuard.Analyzers` targets `netstandard2.0`, references `Microsoft.CodeAnalysis.CSharp`
4.8.0 (the lowest version with everything TG001 to TG004 use, which keeps Visual Studio
2022 working) with `EnforceExtendedAnalyzerRules`, and ships two ways: on its own, and
bundled in the runtime package under `analyzers/dotnet/cs`. It needs no reference to the
runtime package, so it recognizes `[AllowCrossTenantAccess]` by its full name.

## Consequences

- Installing the runtime package turns the analyzers on with no further setup.
- A rename of `AllowCrossTenantAccessAttribute` would switch TG001 and TG003 off silently,
  so the architecture tests check the name the analyzers match against the real attribute.
