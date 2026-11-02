# 0010. Target net10.0 and net11.0

- Status: Accepted
- Date: 2026-10-30

## Context

The library first targeted `net8.0` (EF Core 8.0.x, also serving EF Core 9) and `net10.0`
(EF Core 10.x), built with the .NET 10 SDK. .NET 8, .NET 9, EF Core 8 and EF Core 9 reach
end of support on 2026-11-10, before v0.1.0 ships, so the `net8.0` build would serve only
unsupported versions. EF Core 11 requires .NET 11, so .NET 11 apps would load the `net10.0`
build, compiled against EF Core 10, and nothing would test that combination.

## Decision

Target `net10.0` (EF Core 10.x) and `net11.0` (EF Core 11.x), built with the .NET 11 SDK.
EF Core 11-only APIs sit behind `#if NET11_0_OR_GREATER`. `GetDeclaredQueryFilters` exists
on both targets, so no split is needed today. Until .NET 11 ships, `global.json` pins SDK
`11.0.100-rc.3.26522.148` with `allowPrerelease`, and `net11.0` references EF Core
`11.0.0-rc.3.26522.148`; at release, both move to the GA versions and `allowPrerelease` goes.

## Consequences

- Teams on .NET 8 or 9 can't use the library.
- Every test runs on both targets.
- A stable package can't depend on the EF Core 11 release candidate
  ([0016](0016-first-release-is-a-preview.md)).
