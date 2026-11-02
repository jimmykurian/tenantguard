# 0016. The first release is a preview until EF Core 11 is stable

- Status: Accepted
- Date: 2026-10-31

## Context

EF Core 11 has no stable version yet, so the `net11.0` build references `11.0.0-rc.3`
([0010](0010-target-net10-and-net11.md)). NuGet rejects a stable package that depends on a
prerelease (NU5104, an error under `TreatWarningsAsErrors`). Release Please cut `v0.1.0` on
2026-10-31, and the publish job then failed in `task pack`, so the tag and GitHub Release
existed with nothing on nuget.org.

## Decision

Ship `0.1.0-preview.N`, with both targets, as a NuGet prerelease and a GitHub prerelease.
The empty `v0.1.0` tag and Release were deleted and its `CHANGELOG.md` entry removed, since
that version never shipped. Release Please uses `"versioning": "prerelease"` with
`"prerelease-type": "preview"`, which bumps the preview number on each release (a pinned
`release-as` first kept naming `0.1.0-preview.1` after it shipped, and a second release PR
for it failed on the existing tag). Stable `v0.1.0` follows EF Core 11 GA. Rejected:
dropping `net11.0` from the release, and suppressing NU5104, which would ship a stable
package that pulls in a release candidate.

## Consequences

Installs take `--prerelease` until then. `CONTRIBUTING.md` lists what changes for the stable
release, and issue #27 tracks it.
