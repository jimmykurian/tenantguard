# 0028. The docs site has a design of its own

- Status: Accepted
- Date: 2026-11-04
- Supersedes: [0023](0023-docs-site-uses-the-rapide-theme.md)

## Context

[0023](0023-docs-site-uses-the-rapide-theme.md) added the Rapide theme so the site wouldn't
look like ci-parity's. Rapide changes Starlight's colors, borders and spacing, but not its
layout: the header, the sidebar, the "On this page" column and the splash hero stay the
same, and the two sites still read as the same site. Every Starlight theme leaves that
layout as it is. Moving off Starlight would also give up Pagefind search, the link
validator, Mermaid and the `docs/` sync, so the maintainer chose to keep Astro and Starlight
and replace the parts that set the look (#77).

## Decision

The site drops `starlight-theme-rapide` and styles itself:

- **Palette and fonts.** `site/src/styles/custom.css` sets Starlight's color tokens from the
  shield logo's navy, green and blue, for the dark and light themes. Headings are in Space
  Grotesk, text in IBM Plex Sans and code in JetBrains Mono, self-hosted from the
  `@fontsource-variable` packages (OFL-1.1), pinned in `site/package.json` like the other
  site packages. Code blocks use the GitHub Dark Dimmed and GitHub Light themes.
- **Component overrides.** `site/src/components/` replaces Starlight's `Header` (the
  wordmark, a bar of the site's sections, search, GitHub and the theme switch), `Footer`
  (the edit link and previous and next pages, then the project's links and license) and
  `PageTitle` (hidden on the home page, whose hero carries the heading).
- **Home page.** `site/src/pages/index.astro` is the home page, built with Starlight's
  `StarlightPage` so it keeps the header, search and theme switch. It replaces
  `site/src/content/docs/index.mdx`, so every page in the content collection now comes from
  `docs/`, and `scripts/sync-docs.mjs` clears the whole collection on each sync.

The rest of [0021](0021-docs-site-in-astro.md) stands.

## Consequences

The site no longer looks like ci-parity's, and it has no theme plugin to keep in step with
Starlight's upgrades. The overrides do: a Starlight release that changes the `Header`,
`Footer` or `PageTitle` components, or the tokens `custom.css` sets, can need a change
here, and `task site:check` builds the site to catch it. The home page is an Astro page, so
the link validator doesn't check its links; they point only at section pages that the
validator checks exist. Every text color in the palette meets 4.5:1 on its background in
both themes.
