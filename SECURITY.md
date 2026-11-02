# Security Policy

## About this project

TenantGuard is a set of NuGet packages for EF Core applications that serve several
tenants from one database. `TenantGuard.EntityFrameworkCore` checks the model at startup
and blocks a `SaveChanges` that writes outside the current tenant, and the analyzers it
bundles flag `IgnoreQueryFilters()` and raw SQL at compile time. `TenantGuard.Testing`
seeds a test database and proves, entity by entity, that one tenant cannot reach
another's rows. The runtime package runs inside your application with your `DbContext`,
the analyzers run in the compiler, and the test kit runs in your tests.

## Supported versions

TenantGuard is pre-release, and nothing is on NuGet yet. Fixes go to `main` and ship in
the next release; once releases start, only the latest one gets fixes.

## Reporting a vulnerability

**Please don't report security issues through public issues or pull
requests.** Use GitHub's private vulnerability reporting: on the repository's
**Security** tab, choose **Report a vulnerability**. If that isn't available,
email <hello@jimmykurian.me>.

Please include:

- The TenantGuard package and version, with the EF Core version and the database provider
- A minimal model, `UseTenantGuard` configuration and code that show it
- What an attacker could do with it

### What to expect

This is a one-person project, so these are goals rather than guarantees:

| Action                | Timeframe                         |
| --------------------- | --------------------------------- |
| Acknowledgment        | Within 5 days                     |
| Assessment and update | Within 14 days                    |
| Fix and release       | As soon as practical for severity |

There is no bug bounty. I'm happy to credit you in the release notes once it's
fixed, unless you'd rather stay anonymous.

### In scope

- A way for one tenant to read, change or delete another tenant's rows that the model
  rules or the write guard should stop and don't, in an app configured as
  [the reference](docs/reference/README.md) describes
- A way for the data a request sends to switch the write guard off, such as by opening
  a bypass or changing the current tenant
- An exception message or log entry that carries entity values other than tenant ids
  and key values
- Crashes, hangs or unbounded memory use in the model rules, the write guard or the
  analyzers on a crafted model or source file
- The release pipeline: a way to publish a package that wasn't built from `main`

### Out of scope

- A rule, analyzer or probe check that misreports code with no security impact; open a
  "Wrong Finding" issue instead
- Limits the docs record: raw SQL built at runtime, which TG004 can only point out,
  dependents a filtered required navigation drops (behavior pin 7), and the Proposed
  records in [docs/architecture/decisions/](docs/architecture/decisions/README.md), such as
  writes through an owned entity
- Code that opts out on purpose, with `[AllowCrossTenantAccess]`, a `Bypass` scope or
  `NoTenantPolicy.Allow`
- Vulnerabilities in EF Core, a database provider or a tenancy package such as
  Finbuckle.MultiTenant: report those to their maintainers

## How the project is secured

- **One runtime dependency:** `TenantGuard.EntityFrameworkCore` depends on
  `Microsoft.EntityFrameworkCore` only, and the analyzers have no runtime dependency.
  Adding one needs agreement in an issue first.
- **No network:** none of the packages make network requests.
- **Pinned EF Core behavior:** the behavior pins test what TenantGuard's guarantees rely
  on in EF Core, so an EF Core version that changes one fails the build.
- **Pinned CI:** third-party actions are pinned to full commit SHAs, and workflows run
  with `contents: read` unless a job needs more.
- **Secret scanning:** gitleaks runs on every commit in the pre-commit hook.
- **Signed commits:** commits are SSH-signed and show as Verified on GitHub.

## Responsible disclosure

Please give me reasonable time to release a fix before disclosing an issue. I
won't pursue action against anyone reporting in good faith.

## Attribution

Structure adapted from the [OpenSSF Vulnerability Disclosure
Guide][ossf-guide], licensed under [CC-BY-4.0][cc-by].

[ossf-guide]: https://github.com/ossf/oss-vulnerability-guide
[cc-by]: https://creativecommons.org/licenses/by/4.0/
