# Architecture decisions

Each record says why something about TenantGuard is the way it is: the context, the decision
and its consequences. Records are numbered in the order the decisions were made, and an
accepted record isn't edited later except to mark it superseded
([0001](0001-record-architecture-decisions.md)).

| Record | Decision | Status | Date |
| --- | --- | --- | --- |
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted | 2026-10-29 |
| [0002](0002-runtime-depends-on-ef-core-only.md) | The runtime package depends on EF Core only | Accepted | 2026-10-29 |
| [0003](0003-model-validation-as-an-ef-convention.md) | Model validation runs as an EF Core convention | Accepted | 2026-10-29 |
| [0004](0004-analyzers-on-netstandard2-and-roslyn-4-8.md) | The analyzers target netstandard2.0 on Roslyn 4.8.0 | Accepted | 2026-10-29 |
| [0005](0005-build-settings.md) | Build settings | Accepted | 2026-10-29 |
| [0006](0006-the-public-contract.md) | Rule IDs, messages, options, exceptions and the public API are the contract | Accepted | 2026-10-29 |
| [0007](0007-flat-public-namespaces.md) | The public namespaces are TenantGuard and TenantGuard.Testing | Accepted | 2026-10-29 |
| [0008](0008-startup-validation-needs-a-hosting-dependency.md) | Startup validation through a hosted service, with a hosting dependency | Rejected | 2026-10-29 |
| [0009](0009-startup-validation-through-validateonstart.md) | Startup validation through ValidateOnStart | Accepted | 2026-10-30 |
| [0010](0010-target-net10-and-net11.md) | Target net10.0 and net11.0 | Accepted | 2026-10-30 |
| [0011](0011-per-file-copyright-headers.md) | Apache-2.0, with per-file copyright headers | Accepted | 2026-10-30 |
| [0012](0012-public-api-for-options-and-severity.md) | Public API for options and severity | Accepted | 2026-10-30 |
| [0013](0013-no-allocations-only-where-detectchanges-can-be-skipped.md) | No allocations only where the guard can skip DetectChanges | Proposed | 2026-10-30 |
| [0014](0014-owned-entity-writes-checked-through-the-owner.md) | Writes through an owned entity are checked through its owner | Proposed | 2026-10-30 |
| [0015](0015-tgm003-treats-ef-context-replacement-as-the-context.md) | TGM003 treats an operand EF Core swaps for the context as the context | Proposed | 2026-10-30 |
| [0016](0016-first-release-is-a-preview.md) | The first release is a preview until EF Core 11 is stable | Accepted | 2026-10-31 |
| [0017](0017-end-to-end-tests-live-in-tests-e2e.md) | End-to-end tests live in tests/e2e, with Finbuckle | Accepted | 2026-11-01 |
| [0018](0018-test-libraries-beyond-xunit.md) | Test libraries beyond xUnit | Accepted | 2026-11-01 |
| [0019](0019-runtime-folders-are-rings.md) | The runtime package's folders are rings with an enforced direction | Accepted | 2026-11-01 |
| [0020](0020-readme-follows-ci-parity.md) | The README follows ci-parity's, with a package README of its own | Accepted | 2026-11-02 |
| [0021](0021-docs-site-in-astro.md) | A docs site in Astro, built from docs/ | Accepted | 2026-11-02 |
| [0022](0022-fan-out-the-spec-plan-and-decisions-log.md) | The docs replace the specification, the plan and the decisions log | Accepted | 2026-11-02 |
| [0023](0023-docs-site-uses-the-rapide-theme.md) | The docs site uses the Rapide theme | Accepted | 2026-11-02 |
| [0024](0024-a-dotnet-tool-for-model-validation.md) | A .NET tool package for model validation in CI | Proposed | 2026-11-03 |

## Proposing a change

When a decision turns out to be wrong, copy the shape below into the next number with status
**Proposed**, say what it changes and why, and stop: work on it waits until the maintainer
accepts or rejects it. A record that replaces an earlier one says so, and the earlier one's
status becomes "Superseded by NNNN".

```md
# NNNN. Title

- Status: Proposed
- Date: YYYY-MM-DD

## Context

What made the decision necessary: the forces, the constraints, what was tried.

## Decision

What changes, stated so it can be checked.

## Consequences

What becomes easier or harder, and what follows from it.
```
