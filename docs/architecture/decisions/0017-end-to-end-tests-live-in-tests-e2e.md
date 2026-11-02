# 0017. End-to-end tests live in tests/e2e, with Finbuckle

- Status: Accepted
- Date: 2026-11-01

## Context

No project outside `samples/` could depend on Finbuckle or any tenancy library, and each
sample's tests sat beside it. The test projects were split into `tests/unit/`,
`tests/integration/` and `tests/e2e/`, and the end-to-end tests are the sample tests, which
drive each sample app through its host. `Finbuckle.Api.Tests` uses Finbuckle directly (its
tenant context accessor) and references `samples/Finbuckle.Api`.

## Decision

A tenancy library may appear in `samples/` and `tests/e2e/`. `tests/e2e/` holds only tests
of the sample apps; the sample apps stay in `samples/`.

## Consequences

No shipped package and no unit or integration test depends on a tenancy library; the
architecture tests check the shipped packages.
