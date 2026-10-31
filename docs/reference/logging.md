# Logging

TenantGuard logs under the category `TenantGuard`, through the `ILoggerFactory` the context
uses: the one set with `UseLoggerFactory`, or else the application's, which
`AddDbContext` passes to EF Core. Without either, it logs nothing.

| ID | Event | Level | Message | When |
|---|---|---|---|---|
| 1 | `ModelValidated` | Information | `TenantGuard checked the model: {TenantScopedEntities} tenant-scoped entity types, {Errors} errors, {Warnings} warnings` | EF Core built a model and the rules ran |
| 2 | `ModelFinding` | Warning, or Error | `{RuleId}: {Finding}` | For each warning, and for each error under `Enforcement.Log`; under `Enforcement.Throw` the errors are in the exception instead |
| 3 | `NoTenantAllowed` | Information | `TenantGuard let {Context} save without a current tenant (NoTenantPolicy.Allow)` | Once per `SaveChanges` that writes tenant-scoped rows with no current tenant, under `NoTenantPolicy.Allow` |
| 4 | `Bypass` | Warning | `TenantGuard bypass: {Reason} ({Context})` | `context.TenantGuard().Bypass(reason)` opened a bypass scope |
| 5 | `ViolationBlocked` | Error | `TenantGuard blocked SaveChanges on {Context}; cross-tenant writes: {Count}` | Just before `TenantIsolationViolationException` is thrown |

The messages carry counts, names, rule IDs and bypass reasons, never entity values. The
exceptions carry tenant ids and key values, and no other values.

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
