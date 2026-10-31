# Guides

How to put TenantGuard to work. The [reference](../reference/README.md) lists every rule,
option and message.

- [Hand-rolled query filters](hand-rolled.md): a context with its own tenant and a
  `HasQueryFilter` per entity
- [Finbuckle.MultiTenant](finbuckle.md): entities marked with `IsMultiTenant()`
- [Testing](testing.md): prove isolation per entity, with xUnit, NUnit, MSTest or TUnit
- [Migrating an existing app](migrating.md): turn TenantGuard on without stopping
  production

The [samples](../../samples) run what the first two guides show, each with a probe test.
