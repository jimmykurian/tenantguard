# 0005. Build settings

- Status: Accepted
- Date: 2026-10-29

## Context

TenantGuard is a library others depend on, so builds have to be reproducible, and warnings,
API changes and style drift have to be caught before they ship.

## Decision

- The .NET SDK is pinned in `global.json` with `rollForward: latestFeature`; C# latest.
- Central Package Management: every version lives in `Directory.Packages.props`.
- Nullable on, `TreatWarningsAsErrors`, deterministic builds and SourceLink.
- `PublicApiAnalyzers` tracks the public API in `PublicAPI.Shipped.txt` and
  `PublicAPI.Unshipped.txt`.
- StyleCop.Analyzers, configured in `stylecop.json` and `.editorconfig`, with
  `EnforceCodeStyleInBuild`.

## Consequences

A warning fails the build, so every one is fixed or turned off on purpose in
`.editorconfig`, and an accidental public API change fails `task lint:api`.
