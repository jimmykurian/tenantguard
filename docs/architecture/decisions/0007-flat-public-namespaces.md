# 0007. The public namespaces are TenantGuard and TenantGuard.Testing

- Status: Accepted
- Date: 2026-10-29

## Context

Users of a library type `using` lines for its public types. A namespace per folder
(`TenantGuard.Model`, `TenantGuard.Interception`) spreads the registration across several,
and names the library's internal layout instead of anything the user looks for. IDEs suggest
folder namespaces by default (ReSharper's namespace check, the IDE0130 rule).

## Decision

The runtime package's types are in `TenantGuard` and the test kit's in
`TenantGuard.Testing`, whatever folder they're in; the analyzers' are in
`TenantGuard.Analyzers`. The two packages' `.csproj.DotSettings` files and `.editorconfig`
turn the folder-namespace checks off for them (2026-11-01).

## Consequences

- One `using TenantGuard;` reaches `UseTenantGuard`, the options, the exceptions and the
  attributes.
- Folders only group files, so their direction is checked from the symbols instead
  ([0019](0019-runtime-folders-are-rings.md)).
- The architecture tests check where every public type lives.
