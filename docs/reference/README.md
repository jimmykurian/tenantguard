# Reference

What TenantGuard does, exactly. These pages list every rule, diagnostic, option, exception
and log event, with the messages word for word; [SPEC.md](../SPEC.md) is the contract they
describe. Messages are part of that contract, so a change to one is a breaking change.

- [Model rules](rules.md): TGM001 to TGM008, checked when EF Core builds the model
- [Analyzers](analyzers.md): TG001 to TG004, checked at compile time
- [Options](options.md): `UseTenantGuard`, startup validation, `TenantGuardModel.Validate`
  and the bypass scope
- [Exceptions](exceptions.md): what TenantGuard throws, and when
- [Logging](logging.md): the `TenantGuard` log category and its events

For how to put them to work, see the [guides](../guides/README.md).
