# Compatibility

What stays the same between TenantGuard releases, so you can depend on it.

## The contract

These are the public contract ([0006](../architecture/decisions/0006-the-public-contract.md)):

- the rule IDs TGM001 to TGM014 and the analyzer IDs TG001 to TG005, and their default
  severities;
- every message, word for word, as the [rules](rules.md), [analyzers](analyzers.md),
  [exceptions](exceptions.md) and [logging](logging.md) pages give it: messages are
  deterministic, so you can match them in logs and alerts;
- the option names in [Options](options.md) and the `.editorconfig` keys;
- the exception types;
- the public API in each package's `PublicAPI.Shipped.txt`.

Changing any of them is a breaking change, and needs an accepted architecture decision first.

## Releases

0.1.0 is the first stable release. The `0.1.0-preview.N` releases before it were previews,
while EF Core 11 was a release candidate
([0016](../architecture/decisions/0016-first-release-is-a-preview.md)). Until 1.0, a
breaking change to the contract above raises the minor version, as `0.2.0`; from 1.0, the
major version. The
[changelog](https://github.com/jimmykurian/tenantguard/blob/main/CHANGELOG.md) lists every
change between releases.

## Supported versions

The net10.0 build runs on EF Core 10, the net11.0 build on EF Core 11, with any provider
([0010](../architecture/decisions/0010-target-net10-and-net11.md)). The packages depend on
EF Core 10.0.0 or later for net10.0, so an app on any 10.0.x patch restores without being
moved up, and on EF Core 11.0.0 or later for net11.0
([0033](../architecture/decisions/0033-field-trial-messages-and-an-ef-core-10-0-0-floor.md)).
CI builds and tests on the latest patch, and runs the unit and integration tests on
10.0.0 too. The analyzers load in any
compiler on Roslyn 4.8.0 or later, Visual Studio 2022 included.

## Compiled models and Native AOT

EF Core 10 and 11 can't generate a compiled model for a model with a query filter:
`dotnet ef dbcontext optimize` stops with "query filters are not supported". Native AOT
needs a compiled model, so an app that isolates tenants with query filters can't run under
Native AOT yet, with or without TenantGuard. Behavior pin 9 checks it on each EF Core
version, and TenantGuard will add what a compiled model needs once EF Core supports it
([0027](../architecture/decisions/0027-no-source-generator-while-compiled-models-refuse-query-filters.md)).
