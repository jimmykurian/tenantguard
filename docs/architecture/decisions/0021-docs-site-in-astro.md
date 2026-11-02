# 0021. A docs site in Astro, built from docs/

- Status: Accepted
- Date: 2026-11-02

## Context

The docs were Markdown in `docs/`, read on GitHub, and the toolchain was .NET and Task only.
The maintainer asked for a documentation site like ci-parity's.

## Decision

`site/` is an Astro Starlight site, on the packages ci-parity's site runs (`astro`,
`@astrojs/starlight`, `astro-mermaid`, `starlight-links-validator`), pinned in
`site/pnpm-lock.yaml`. `docs/` stays the only source: `site/scripts/sync-docs.mjs` copies
each page in, takes its title and description from its H1 and first paragraph, and turns
links between pages into site routes and links out of `docs/` into GitHub links. The build
fails on a broken link or anchor. `site.yml` deploys it to GitHub Pages at
<https://jimmykurian.github.io/tenantguard/> from `main`, and `ci.yml` builds it on every
pull request.

## Consequences

Node.js 22+ and pnpm are needed for the site only. The lockfile keeps integrity hashes and
no tarball URLs, since a registry mirror's URLs don't resolve elsewhere, and
`task site:install` refuses one that has them.
