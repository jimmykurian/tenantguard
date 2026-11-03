// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.

// The docs site, served from GitHub Pages at https://tenantguard.is-cool.dev/, an Open Domains
// subdomain whose A records point at GitHub Pages.
// Its pages come from docs/ through scripts/sync-docs.mjs; only the home page lives here.

import starlight from '@astrojs/starlight';
import { defineConfig } from 'astro/config';
import mermaid from 'astro-mermaid';
import starlightLinksValidator from 'starlight-links-validator';
import starlightThemeRapide from 'starlight-theme-rapide';

export default defineConfig({
  site: 'https://tenantguard.is-cool.dev',
  integrations: [
    // Renders the ```mermaid diagrams in the browser, following the light or dark theme.
    // It must come before Starlight.
    mermaid({ theme: 'default', autoTheme: true }),
    starlight({
      title: 'TenantGuard',
      description: 'Prove an EF Core multi-tenant model leaks nothing: startup model validation, a SaveChanges guard, Roslyn analyzers and an isolation test kit.',
      logo: { src: './src/assets/logo.svg' },
      social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/jimmykurian/tenantguard' }],
      customCss: ['./src/styles/custom.css'],
      // favicon.svg is Starlight's default favicon; the PNG covers the rest.
      favicon: '/favicon.svg',
      head: [
        { tag: 'link', attrs: { rel: 'icon', type: 'image/png', sizes: '128x128', href: '/favicon.png' } },
        { tag: 'link', attrs: { rel: 'apple-touch-icon', href: '/favicon.png' } },
      ],
      lastUpdated: false,
      sidebar: [
        { label: 'Overview', link: '/overview/' },
        { label: 'Guides', items: [{ autogenerate: { directory: 'guides' } }] },
        { label: 'Reference', items: [{ autogenerate: { directory: 'reference' } }] },
        {
          label: 'Architecture',
          items: [
            'architecture',
            'architecture/packages',
            'architecture/flows',
            { label: 'Decisions', items: [{ autogenerate: { directory: 'architecture/decisions' } }] },
          ],
        },
        { label: 'Testing', items: [{ autogenerate: { directory: 'testing' } }] },
        { label: 'Releases and CI', items: [{ autogenerate: { directory: 'deployment' } }] },
        { label: 'Changelog', link: '/changelog/' },
      ],
      // Fails the build on a broken link or anchor between pages.
      // Rapide restyles Starlight, so this site doesn't look like ci-parity's.
      plugins: [starlightLinksValidator(), starlightThemeRapide()],
    }),
  ],
});
