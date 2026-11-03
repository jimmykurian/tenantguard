# 0026. TGM003 allows a captured tenant in a per-tenant model

- Status: Accepted
- Date: 2026-11-03

## Context

TGM003 exists because EF Core caches one model per context type, so a filter that
captures the tenant when the model is built filters every tenant by the first (behavior
pin 4). An app can key the model cache by tenant with an `IModelCacheKeyFactory`. Each
tenant then gets a model of its own, and capturing the tenant is correct (behavior pin 8).
Issue #24 asks for a way to say so that can't hide a real TGM003.

## Decision

- An option, `AllowCapturedValues()`, which shapes the model like the other rule settings.
- Under it, the convention leaves TGM003 out only when the context building the model has a
  current tenant, its `IModelCacheKeyFactory` isn't EF Core's own `ModelCacheKeyFactory`,
  and a key that is a tuple includes the current tenant. TGM004 and the other rules don't change.
- The check sees one context, so it can't prove two tenants get two keys. A model that
  passes it records the tenant it was built for, and the write guard blocks a save of
  tenant-scoped rows when the current tenant is another one, with
  `TenantIsolationViolationException` and no `Violations`. The isolation probe, which
  creates a context per tenant, proves it in tests.
- `TenantGuardModel.Validate` has no context, so it keeps reporting TGM003. The validate tool
  runs the check on the context it creates.

## Consequences

An app with a model per tenant can run under `Enforcement.Throw`. A key factory that
returns a key of its own type that ignores the tenant passes the startup check, but its
first save as a second tenant is blocked, and so is the probe's seeding as one. The probe
looks entries up by name, since each tenant's context has a model of its own. EF Core builds and caches a model per tenant, and
TenantGuard checks each one as it's built.
