# 0020. The README follows ci-parity's, with a package README of its own

- Status: Accepted
- Date: 2026-11-02

## Context

The root `README.md` was the package README too (`PackageReadmeFile`), so it used absolute
links and nothing nuget.org can't render. The maintainer asked for the README to follow
ci-parity's: release and license shields, a demo, install, an architecture diagram in
Mermaid, a CI status table and a code metrics table. nuget.org renders no Mermaid and blocks
animated SVG, and a status table means nothing on a package page.

## Decision

The packages ship `assets/nuget-readme.md`, a focused README, and the root README follows
ci-parity's. The status table reads shields.io endpoint files that a `badges` job in
`ci.yml` commits to a `badges` branch on every push to `main`, one per check; the checks run
as separate steps so each badge is its own. The code metrics come from `tools/Metrics`, a
small console app on Roslyn's syntax API (ci-parity uses `scc` and Go's complexity, and
`Microsoft.CodeAnalysis.Metrics` only runs on Windows). The demo is an animated SVG terminal
in `docs/assets/`, built from real output.

## Consequences

`task lint` runs `task metrics:check`, so a change that moves the numbers updates them with
`task metrics:readme`.
