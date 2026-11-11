# 0033. Field-trial messages and an EF Core 10.0.0 floor

- Status: Proposed
- Date: 2026-11-11

## Context

The field trial (#28) found messages that say something untrue, a validate tool that names
the wrong cause, and a dependency floor that gets in the way (#72):

1. **TGM009** says each query "reads past other tenants' rows". That isn't true of lookups by key or
   by another indexed column. On tables read only by key, the trial classed every TGM009 as
   noise.
2. **TGM005** says rows with no tenant are "invisible to every tenant and bypass the write
   guard". A context with no current tenant reads them, because EF Core compares nulls as
   C# does. A filter with an `e.TenantId == null` disjunct shows them to every tenant. And
   the write guard refuses them from a context with a current tenant.
3. **TGM007** says "the operand could not be classified" when a filter passes the tenant
   property to a function, such as ABP's `DbFunction`, and never compares it.
4. **The validate tool:**
   - It said an app "doesn't reference Microsoft.EntityFrameworkCore.Design" when the app
     did, at an older patch. The payload is compiled against Design 10.0.14, EF Core's
     assembly version carries the patch, and loading it on 10.0.13 fails.
   - It advised adding a design-time factory when the app had two.
   - Under `Enforcement.Log`, it printed every finding twice.
5. **The EF Core floor:** the runtime package depends on EF Core 10.0.14, so restore fails
   with NU1605 on 10.0.13, and an app that restores gets mixed EF Core patches.
6. **The probe:** `ProbeReport.ToString()` prints the type name, and NavigationLeak skips a
   unique foreign key with a raw database error.
7. **TG002 and the tenant column:** an app that names its tenant property `CompanyId` gets
   TG002 on every statement until `tenantguard.tenant_column` is set too.

Messages, the public API's behavior and the supported versions are the contract
([0006](0006-the-public-contract.md)).

## Decision

- **TGM005, TGM007 and TGM009 get new messages,** as rules.md gives them:
  - TGM005 has a second message for a filter with a null disjunct.
  - TGM007 has three messages: an operand that can't be classified, the tenant property
    passed to a method, and a comparison only with null.
  - The rule IDs, names and severities don't change.
- **TG002's message ends with a hint** to set `tenantguard.tenant_column` when the key isn't
  set.
- **The packages depend on EF Core 10.0.0 or later for net10.0,** and the tool's payload is
  compiled against Design 10.0.0, so it loads on any 10.0.x. The net11.0 build depends on
  the EF Core 11 release it's built with while that is a preview, then on 11.0.0. CI keeps
  building and testing on the latest patch, and adds a job that runs the unit and
  integration tests on 10.0.0.
- **The validate tool reports a load failure as itself.** It names the missing Design
  reference only when the app's `.deps.json` lists none, and names duplicate design-time
  factories. It gives the "add a factory" hint only when there's no factory. It sets an
  internal switch while it runs, under which TenantGuard doesn't log the model findings, so
  each prints once.
- **`ProbeReport.ToString()`** returns the table of every row, under a summary.
- **NavigationLeak skips a unique foreign key** before trying, with a message that says why.

## Alternatives considered

- **Keep the 10.0.14 floor, and have the tool report the mismatch.** Every app on an older
  patch would still be moved up, or fail to restore.
- **Document the double printing,** rather than muting TenantGuard's log while the tool
  runs.
- **Give `ToString()` only a summary line.** The rows are what a reader wants when a test
  prints the report.
- **Add only documentation for the tenant column,** not the TG002 hint.

## Consequences

The messages change, so a test that asserts on them, or a log query that matches them,
needs updating. The 10.0.0 floor means TenantGuard can't use an API added in a later 10.0.x
patch without raising it, and the floor CI job catches that. The muting switch is internal:
only the tool sets it.
