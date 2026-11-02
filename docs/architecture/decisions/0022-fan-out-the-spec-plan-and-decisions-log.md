# 0022. The docs replace the specification, the plan and the decisions log

- Status: Accepted
- Date: 2026-11-02

## Context

Three files carried the project: `docs/SPEC.md` (what to build), `docs/PLAN.md` (in what
order) and `docs/DECISIONS.md` (problems found with a decision). The reference pages had
grown to say what the specification said, word for word, so the same facts lived twice; the
plan's build was finished and its backlog had moved to GitHub issues; and the log mixed the
decisions made at the start with corrections found later, newest first. The maintainer asked
to fan them out like ci-parity's docs.

## Decision

The three files are gone:

- The specification's content lives in [docs/reference/](../../reference/README.md) (rules,
  analyzers, options, exceptions, logging, and new pages for the write guard, the probe and
  compatibility), [docs/architecture/](../README.md) (packages, vocabulary, quality goals,
  flows), [docs/testing/](../../testing/strategy.md) (the behavior pins),
  [docs/deployment/](../../deployment/releases-and-ci.md) (this repo's CI and releases) and
  [the "why" guide](../../guides/why.md).
- The plan's build is finished; git history and the changelog record it. Its backlog is
  issues #20 to #26 on the
  [roadmap](https://github.com/users/jimmykurian/projects/2), by milestone.
- The decisions log became these records, numbered in the order the decisions were made,
  together with the decisions `AGENTS.md` listed as already made.

## Consequences

The reference pages are the contract ([0006](0006-the-public-contract.md)); a feature issue
gets its reference page agreed before code. A decision that turns out wrong gets a Proposed
record ([0001](0001-record-architecture-decisions.md)).
