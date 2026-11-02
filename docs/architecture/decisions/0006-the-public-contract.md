# 0006. Rule IDs, messages, options, exceptions and the public API are the contract

- Status: Accepted
- Date: 2026-10-29

## Context

Teams suppress diagnostics by ID, match exception messages in logs and alerts, and write
`UseTenantGuard` configuration against the option names. A change to any of them breaks
them, even when no signature changes.

## Decision

The rule IDs (TGM001 to TGM008, TG001 to TG004), their messages, the option names, the
exception types and the public API are the public contract, listed word for word in
[docs/reference/](../../reference/README.md) and in the `PublicAPI.Shipped.txt` files.
[Compatibility](../../reference/compatibility.md) says what that promises. Changing any of
them is a breaking change and needs an accepted record first. Messages are deterministic
and stable.

## Consequences

- No option, rule or public type is added without a reference page first.
- Message tests assert the exact text.
