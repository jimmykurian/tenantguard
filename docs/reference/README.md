# Reference

What TenantGuard does, exactly. These pages list every rule, diagnostic, option, exception
and log event, with the messages word for word. They are the contract: a change to a rule
ID, a message, an option name or an exception type is a breaking change
([Compatibility](compatibility.md)).

- [Model rules](rules.md): TGM001 to TGM010, checked when EF Core builds the model
- [Analyzers](analyzers.md): TG001 to TG004, checked at compile time
- [Write guard](write-guard.md): what `SaveChanges` checks for each Added, Modified and
  Deleted entry
- [Isolation probe](probe.md): how the probe seeds rows and what each check proves
- [Validate tool](validate-tool.md): `dotnet tenantguard validate`, the model rules in CI
  without a database
- [Options](options.md): `UseTenantGuard`, startup validation, `TenantGuardModel.Validate`
  and the bypass scope
- [Exceptions](exceptions.md): what TenantGuard throws, and when
- [Logging](logging.md): the `TenantGuard` log category and its events
- [Compatibility](compatibility.md): what stays the same between releases

For how to put them to work, see the [guides](../guides/README.md); for how it's built, the
[architecture](../architecture/README.md).
