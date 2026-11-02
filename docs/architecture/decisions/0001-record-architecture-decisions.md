# 0001. Record architecture decisions

- Status: Accepted
- Date: 2026-10-29

## Context

TenantGuard's decisions lived in three places: a specification, a build plan, and a
decisions log that recorded each problem found with a decision. The specification grew into
the reference pages, the build plan was finished, and the log mixed decisions made at the
start with corrections found later.

## Decision

Each architecture decision is an Architecture Decision Record in `docs/architecture/decisions/`,
numbered in the order it was made: `NNNN-title.md`, with a status (Proposed, Accepted,
Rejected, or Superseded by a later record), a date, and its context, decision and
consequences. The [index](README.md) lists them.

A decision that turns out wrong gets a new record with status Proposed, which says what it
changes; work on it stops until the maintainer accepts or rejects it. An accepted record
isn't edited later, except to mark it superseded.

## Consequences

Why a rule, a target or a dependency is the way it is can be found by number, and a change
to one starts from the record that made it.
