# 0011. Apache-2.0, with per-file copyright headers

- Status: Accepted
- Date: 2026-10-30

## Context

TenantGuard is licensed Apache-2.0, with one `LICENSE` at the root, and started with no
per-file headers. The owner wants the same per-file headers as their other .NET services,
which put StyleCop's XML copyright header in C# files and a `#` copyright line in YAML.

## Decision

C# files start with the StyleCop header built from `companyName` and `copyrightText` in
`stylecop.json`, enforced by SA1633 and the rules that follow it. YAML files, scripts and
the git hooks start with the same copyright line as a comment. Markdown, JSON, MSBuild and
ignore files have none. The text names Apache-2.0 instead of "All rights reserved".

## Consequences

SA1636 matches the text exactly, so changing the year means changing `copyrightText` and
every header together. `task lint:headers` checks the YAML files and hooks.
