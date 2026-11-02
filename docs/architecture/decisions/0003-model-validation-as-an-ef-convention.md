# 0003. Model validation runs as an EF Core convention

- Status: Accepted
- Date: 2026-10-29

## Context

The model rules need the finished model, once per model, and must not stop the app from
replacing EF Core's own services. EF Core offers `IModelValidator`, a single service an app
or provider may already replace, and model-finalized conventions, which any number of
plugins can add.

## Decision

`UseTenantGuard` adds an `IDbContextOptionsExtension` that registers an
`IConventionSetPlugin` with one `IModelFinalizedConvention`. The convention runs the rules
on the finalized model, stores the per-entity metadata as a model annotation
(`TenantGuard:Entities`), and throws or logs per `Enforcement`. TenantGuard doesn't replace
`IModelValidator`.

## Consequences

- The rules run once per model, and EF Core caches the model with the metadata the write
  guard reads.
- An app or provider that replaces `IModelValidator` keeps working.
- The model is built lazily, so a bad model surfaces on first use unless startup validation
  touches it ([0009](0009-startup-validation-through-validateonstart.md)).
