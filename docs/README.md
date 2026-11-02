# TenantGuard documentation

TenantGuard proves an EF Core multi-tenant model leaks nothing: startup model validation, a
SaveChanges guard, Roslyn analyzers and an isolation test kit.

- [Guides](guides/README.md): [why TenantGuard](guides/why.md), and how to put it to work
  with [hand-rolled filters](guides/hand-rolled.md), with [Finbuckle](guides/finbuckle.md),
  in [tests](guides/testing.md), and in [an app that already runs](guides/migrating.md).
- [Reference](reference/README.md): every [rule](reference/rules.md),
  [analyzer](reference/analyzers.md), [write guard check](reference/write-guard.md),
  [probe check](reference/probe.md), [option](reference/options.md),
  [exception](reference/exceptions.md) and [log event](reference/logging.md), with the
  messages word for word, and what stays [compatible](reference/compatibility.md).
- [Architecture](architecture/README.md): the [packages](architecture/packages.md), the
  [flows](architecture/flows.md), and the [decisions](architecture/decisions/README.md)
  behind them.
- [Testing strategy](testing/strategy.md): how TenantGuard tests itself, and the EF Core
  behaviors it pins.
- [Releases and CI](deployment/releases-and-ci.md): the workflows and how a release ships.

What's planned next is on the [roadmap](https://github.com/users/jimmykurian/projects/2), by
milestone.
