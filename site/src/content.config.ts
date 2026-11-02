// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.

// Starlight's docs collection: src/content/docs, filled by scripts/sync-docs.mjs.

import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({ loader: docsLoader(), schema: docsSchema() }),
};
