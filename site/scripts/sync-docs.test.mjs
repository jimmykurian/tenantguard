// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.

// Unit tests for sync-docs.mjs (task site:test).

import assert from 'node:assert/strict';
import { test } from 'node:test';
import { changelog, convert, outputPath, rewriteLink, route } from './sync-docs.mjs';

const noDirs = () => false;

test('a README becomes its folder index, and the top one the overview', () => {
  assert.equal(outputPath('README.md'), 'overview.md');
  assert.equal(outputPath('guides/README.md'), 'guides/index.md');
  assert.equal(outputPath('guides/testing.md'), 'guides/testing.md');
});

test('routes sit at the site root, lowercased, ending in a slash', () => {
  assert.equal(route('architecture/decisions/README.md'), '/architecture/decisions/');
  assert.equal(route('guides/WHY.md'), '/guides/why/');
  assert.equal(route('guides/README.md'), '/guides/');
  assert.equal(route('reference/rules.md'), '/reference/rules/');
});

test('links between docs pages become routes and keep their anchor', () => {
  assert.equal(rewriteLink('rules.md#tgm001', 'reference/analyzers.md', noDirs), '/reference/rules/#tgm001');
  assert.equal(rewriteLink('../architecture/README.md', 'reference/README.md', noDirs), '/architecture/');
});

test('links out of docs/ point at the file on GitHub', () => {
  assert.equal(rewriteLink('../../LICENSE', 'guides/testing.md', noDirs), 'https://github.com/jimmykurian/tenantguard/blob/main/LICENSE');
  assert.equal(rewriteLink('../../samples', 'guides/README.md', () => true), 'https://github.com/jimmykurian/tenantguard/tree/main/samples');
});

test('absolute links and anchors are left alone', () => {
  assert.equal(rewriteLink('https://example.com/a.md', 'README.md', noDirs), 'https://example.com/a.md');
  assert.equal(rewriteLink('#options', 'README.md', noDirs), '#options');
});

test('a page gets its title and description from its H1 and first paragraph, and fenced code is untouched', () => {
  const page = convert('# Testing\n\nThe [probe](a.md) proves isolation.\n\n```md\n[x](a.md)\n```\n', 'guides/testing.md', noDirs);
  assert.match(page, /^---\ntitle: "Testing"\ndescription: "The probe proves isolation."\n/);
  assert.match(page, /\]\(\/guides\/a\/\)/);
  assert.match(page, /```md\n\[x\]\(a\.md\)\n```/);
});

test('a page without an H1 is an error', () => {
  assert.throws(() => convert('No title here.\n', 'guides/x.md', noDirs), /no H1/);
});

test('the changelog drops its H1 and has no edit link', () => {
  const page = changelog('# Changelog\n\n## 0.1.0\n');
  assert.match(page, /title: "Changelog"/);
  assert.match(page, /editUrl: false/);
  assert.doesNotMatch(page, /^# Changelog/m);
});
