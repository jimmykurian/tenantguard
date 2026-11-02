# Changelog

## [0.1.0-preview.6](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.5...v0.1.0-preview.6) (2026-11-02)


### Documentation

* add a walkthrough, coverage visuals and a use case to the site home ([#43](https://github.com/jimmykurian/tenantguard/issues/43)) ([924b6b2](https://github.com/jimmykurian/tenantguard/commit/924b6b2f41e219a0ea07a4059476011b5001f2f4))
* fan out the spec, plan and decisions log, with ADRs ([#41](https://github.com/jimmykurian/tenantguard/issues/41)) ([1a413f2](https://github.com/jimmykurian/tenantguard/commit/1a413f2423ab89e8dbe2adf51edd7db1c788cd7e))

## [0.1.0-preview.5](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.4...v0.1.0-preview.5) (2026-11-02)


### Documentation

* follow ci-parity's README, with CI badges and code metrics ([#38](https://github.com/jimmykurian/tenantguard/issues/38)) ([f6c6681](https://github.com/jimmykurian/tenantguard/commit/f6c6681527925cf782f60a1b805aeb8b7a3981fe))
* publish the docs as an Astro site on GitHub Pages ([#39](https://github.com/jimmykurian/tenantguard/issues/39)) ([2f7e5cc](https://github.com/jimmykurian/tenantguard/commit/2f7e5cc598b2aa35dd9b4d99f339e07380644d6a))

## [0.1.0-preview.4](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.3...v0.1.0-preview.4) (2026-11-02)


### Code Refactoring

* arrange the runtime in rings with an enforced inward direction ([#34](https://github.com/jimmykurian/tenantguard/issues/34)) ([3d30e21](https://github.com/jimmykurian/tenantguard/commit/3d30e212c71e96e2d67622e0591d30746759be62))

## [0.1.0-preview.3](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.2...v0.1.0-preview.3) (2026-10-31)


### Documentation

* drop spec section numbers from code comments ([#18](https://github.com/jimmykurian/tenantguard/issues/18)) ([ee02ee2](https://github.com/jimmykurian/tenantguard/commit/ee02ee2e77af26fcf6e61b4bee68a23d68278c39))

## [0.1.0-preview.2](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.1...v0.1.0-preview.2) (2026-10-31)


### Documentation

* tick the preview release item in the plan ([#14](https://github.com/jimmykurian/tenantguard/issues/14)) ([bbc5c49](https://github.com/jimmykurian/tenantguard/commit/bbc5c49371b5ec1fd13218cfcab8ddc7f02fd8a8))

## 0.1.0-preview.1 (2026-10-31)


### Features

* **analyzers:** flag IgnoreQueryFilters and raw SQL without a tenant predicate ([#6](https://github.com/jimmykurian/tenantguard/issues/6)) ([2d2e9ea](https://github.com/jimmykurian/tenantguard/commit/2d2e9eaa2e53f80ee2a043860cac958bdfb41a74))
* **interception:** block cross-tenant writes in SaveChanges ([#5](https://github.com/jimmykurian/tenantguard/issues/5)) ([80f158d](https://github.com/jimmykurian/tenantguard/commit/80f158d2fbf1228bfc25fd4f8c9817f549f47992))
* **model:** check the model against rules TGM001 to TGM008 ([dba3a5b](https://github.com/jimmykurian/tenantguard/commit/dba3a5b706394a0b2c22be12a7e7489dea3c4107))
* **model:** check the model against the tenant rules ([2ea5e45](https://github.com/jimmykurian/tenantguard/commit/2ea5e45d3c60c57c25261703ce43c4207ee61e5d))
* **probe:** prove tenant isolation per entity and finish both samples ([#7](https://github.com/jimmykurian/tenantguard/issues/7)) ([afae2b3](https://github.com/jimmykurian/tenantguard/commit/afae2b33ba4308dad4fa6dec43a81cdd1062a1f4))


### Documentation

* add MSTest to the test kit's documented frameworks ([c91358c](https://github.com/jimmykurian/tenantguard/commit/c91358c88be220bdb14480ae8bb0eec773d07cd5))
* record the startup check and options API decisions ([a3855f0](https://github.com/jimmykurian/tenantguard/commit/a3855f0afcf902afc31a1b0d2f1a9820986dc1d3))
* tick the model validation items in the plan ([d463dbf](https://github.com/jimmykurian/tenantguard/commit/d463dbff067f37788849dd70d6ef5ff6102a9bbc))

## Changelog

Release Please writes this file from the Conventional Commits on `main`; don't edit it by
hand. Each version's packages are on nuget.org and attached to its GitHub Release.
