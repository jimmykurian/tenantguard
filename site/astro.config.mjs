// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.

// The docs site, served from GitHub Pages at https://jimmykurian.github.io/tenantguard/.
// Its pages come from docs/ through scripts/sync-docs.mjs; only the home page lives here.

import starlight from '@astrojs/starlight';
import { defineConfig } from 'astro/config';
import mermaid from 'astro-mermaid';
import starlightLinksValidator from 'starlight-links-validator';

export default defineConfig({
  site: 'https://jimmykurian.github.io',
  base: '/tenantguard',
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
      plugins: [starlightLinksValidator()],
    }),
  ],
});
