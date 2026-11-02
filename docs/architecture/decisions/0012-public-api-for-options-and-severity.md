# 0012. Public API for options and severity

- Status: Accepted
- Date: 2026-10-30

## Context

The public API first listed `Severity` and `TenantGuardOptions` without declaring them, and
gave no way to get a `TenantGuardOptions` for `TenantGuardModel.Validate` outside
`UseTenantGuard`.

## Decision

Add `public enum Severity { Warning, Error }`, an opaque `public sealed class
TenantGuardOptions`, and a public constructor and `Options` property on
`TenantGuardOptionsBuilder`, the way `DbContextOptionsBuilder` exposes its options:
`new TenantGuardOptionsBuilder().TenantPropertyNames("OrgId").Options`.

## Consequences

Unit tests and tooling run the rules on any model with options built the same way as in
`UseTenantGuard`.
