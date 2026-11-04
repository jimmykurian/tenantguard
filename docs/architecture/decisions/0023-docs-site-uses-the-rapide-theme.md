# 0023. The docs site uses the Rapide theme

- Status: Superseded by [0028](0028-docs-site-has-a-design-of-its-own.md)
- Date: 2026-11-02

## Context

[0021](0021-docs-site-in-astro.md) built the docs site on the packages ci-parity's site
runs, with Starlight's default theme, so the two sites looked the same. The maintainer
asked for a different theme to tell them apart, and chose a community theme over a custom
one. Five that support Starlight 0.42 were built from this site and compared: Rapide,
Flexoki, Black, Next and Catppuccin. Black overlapped the long sidebar titles of the
decision records; the maintainer picked Rapide from the rest.

## Decision

The site adds `starlight-theme-rapide` (by Starlight's maintainer HiDeoo, no dependencies of
its own) as a Starlight plugin in `site/astro.config.mjs`, pinned in `site/package.json` and
`site/pnpm-lock.yaml` like the other site packages. The rest of 0021 stands.

## Consequences

The site looks unlike ci-parity's, and `site/src/styles/custom.css` keeps working because
it styles through Starlight's tokens, which Rapide sets. Rapide's peer range,
`@astrojs/starlight >=0.42.0`, has to keep up with Starlight's upgrades; if it falls behind,
removing the plugin returns the site to the default theme.
