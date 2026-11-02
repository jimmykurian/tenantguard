# Architecture

TenantGuard reads the model an app already builds and never changes it. Three packages work
at three moments: the analyzers when the code compiles, the runtime package when EF Core
builds the model and on every `SaveChanges`, and the test kit in the app's tests.

- [Packages](packages.md): the three packages, what each depends on, their namespaces, the
  rings inside the runtime package, the public API, and the repository's layout.
- [Flows](flows.md): what happens at build, when the model is built, on `SaveChanges`, and
  when the isolation probe runs.
- [Decisions](decisions/README.md): why each of these is the way it is.

## Vocabulary

- **Tenant property:** the property that holds the tenant key on an entity, CLR or shadow,
  found as the [rules reference](../reference/rules.md#which-entity-types-are-tenant-scoped)
  describes.
- **Tenant-scoped entity:** an entity type whose root type has a tenant property.
- **Shared entity:** an entity type declared tenant-free on purpose, such as a lookup table
  (`Shared<T>()`).
- **Current tenant:** the value the app says the context operates as, from the
  `CurrentTenant` resolver.
- **Guard:** the write guard (the SaveChanges interceptor) and the model rules together.

## Quality goals

- Model validation adds under 50 ms to model building for a 200-entity model.
- The write guard adds no allocations on a `SaveChanges` when the model has no tenant-scoped
  entity type, nothing is tracked, or a bypass is open, and O(entries) otherwise
  ([0013](decisions/0013-no-allocations-only-where-detectchanges-can-be-skipped.md)).
- Every message is deterministic and stable: messages are part of the public contract
  ([Compatibility](../reference/compatibility.md)).
- No reflection over user types at query time: per-model metadata is computed once and
  cached on the `IModel`.
