# Logging

TenantGuard logs under the category `TenantGuard`, through the `ILoggerFactory` the context
uses: the one set with `UseLoggerFactory`, or else the application's, which
`AddDbContext` passes to EF Core. Without either, it logs nothing. `LogTo` doesn't receive
these events.

| ID | Event | Level | Message | When |
|---|---|---|---|---|
| 1 | `ModelValidated` | Information | `TenantGuard checked the model: {TenantScopedEntities} tenant-scoped entity types, {Errors} errors, {Warnings} warnings` | EF Core built a model and the rules ran |
| 2 | `ModelFinding` | Warning, or Error | `{RuleId}: {Finding}` | For each warning, and for each error under `Enforcement.Log`; under `Enforcement.Throw` the errors are in the exception instead |
| 3 | `NoTenantAllowed` | Information | `TenantGuard let {Context} save without a current tenant (NoTenantPolicy.Allow)` | Once per `SaveChanges` that writes tenant-scoped rows with no current tenant, under `NoTenantPolicy.Allow` |
| 4 | `Bypass` | Warning | `TenantGuard bypass: {Reason} ({Context})` | `context.TenantGuard().Bypass(reason)` opened a bypass scope |
| 5 | `ViolationBlocked` | Error | `TenantGuard blocked SaveChanges on {Context}; cross-tenant writes: {Count}` | Just before `TenantIsolationViolationException` is thrown; `{Count}` is 0 when the context's model was built for another tenant |
| 6 | `UnfilteredEntity` | Information | `TenantGuard left {Entity} unfiltered, as declared: {Reason}` | EF Core built a model with an entity type declared `Unfiltered<TEntity>` |
| 7 | `CrossTenantModel` | Information | `TenantGuard skipped the rules for {Context}, declared cross-tenant: {Reason}` | EF Core built a model under `CrossTenant`, or the no-tenant model under `CrossTenantWhenNoTenant` |
| 8 | `FindingSuppressed` | Debug | `{RuleId} suppressed for {Entity}: {Reason}` | For each finding that `Suppress` left out |

The messages carry counts, names, rule IDs, rule messages, and the reasons given to
bypasses and declarations. The only values in them are tenant ids, such as the constant a
TGM004 message quotes. The exceptions carry tenant ids and key values; a probe row can also
quote the error a provider or EF Core raised.

To see only problems, raise the category's level:

```json
{
  "Logging": {
    "LogLevel": {
      "TenantGuard": "Warning"
    }
  }
}
```
