# TenantGuard documentation

TenantGuard proves an EF Core multi-tenant model leaks nothing: startup model validation, a
SaveChanges guard, Roslyn analyzers and an isolation test kit.

- [Guides](guides/README.md): how to put it to work, with
  [hand-rolled filters](guides/hand-rolled.md), with [Finbuckle](guides/finbuckle.md), in
  [tests](guides/testing.md), and in [an app that already runs](guides/migrating.md).
- [Reference](reference/README.md): every [rule](reference/rules.md),
  [analyzer](reference/analyzers.md), [option](reference/options.md),
  [exception](reference/exceptions.md) and [log event](reference/logging.md), with the
  messages word for word.
- [Specification](SPEC.md): the contract the reference describes.
- [Decisions](DECISIONS.md): the problems found with a decision, and what changed.
- [Plan](PLAN.md): the build order, and the backlog.
