# 0025. A package per test framework for probe rows

- Status: Accepted
- Date: 2026-11-03

## Context

Issue #25 asks for each probe row to be a test result of its own, in xUnit v3 and TUnit.
Each framework has its own way to make test cases from data: a `DataAttribute` in xUnit v3,
a data source generator attribute in TUnit. `TenantGuard.Testing` works with any test
framework today and depends on none.

## Decision

- Two packages, `TenantGuard.Testing.XunitV3` and `TenantGuard.Testing.TUnit`, each
  depending on `TenantGuard.Testing` and its framework's extensibility package only. Each
  has one public type, `ProbeRowsAttribute<TSource>`, in `TenantGuard.Testing`
  ([0007](0007-flat-public-namespaces.md)).
- `TenantGuard.Testing` gains `IProbeSource`, the class the attributes create to run the
  probe, and `ProbeRow.AssertPassed()`, which a test calls on its row. Neither uses a test
  framework type, so both adapters, and any later one, share them.
- The probe runs once per source type per test process, and the attributes share the
  report.
- The [test adapters](../../reference/test-adapters.md) reference page is the contract.

## Consequences

A project picks the package for its framework and doesn't pull in the other's. Six
packages are packed, released and checked by the architecture tests. The adapters follow
their frameworks' extensibility APIs, so a framework's breaking release needs an adapter
release. In xUnit v3 the rows appear only once the theory runs; in TUnit the probe runs
when tests are discovered.
