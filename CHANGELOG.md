# Changelog

## [1.0.0](https://github.com/jimmykurian/tenantguard/compare/v0.1.0...v1.0.0) (2026-11-12)


### Features

* **interception:** check owned entity writes through their owner ([#113](https://github.com/jimmykurian/tenantguard/issues/113)) ([1b84037](https://github.com/jimmykurian/tenantguard/commit/1b84037ed56ed64a808eeeb3606cfa5d78d734e6))


### Documentation

* **interception:** specify checking owned entity writes through their owner ([#111](https://github.com/jimmykurian/tenantguard/issues/111)) ([c4fce4c](https://github.com/jimmykurian/tenantguard/commit/c4fce4cb7d07b225844eeffbe1987a6c11ccfe3c))

## [0.1.0](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.10...v0.1.0) (2026-11-11)


### Features

* **analyzers:** read SQL in locals, compare the tenant column, add tenant_filter_unnamed and TG005 ([#104](https://github.com/jimmykurian/tenantguard/issues/104)) ([d13c9e4](https://github.com/jimmykurian/tenantguard/commit/d13c9e46961022bf8675f2230d7fa5edc33146c1))
* **model:** check natural primary keys, tenant-free views, switchable filters and unscoped dependents ([#98](https://github.com/jimmykurian/tenantguard/issues/98)) ([17bd74c](https://github.com/jimmykurian/tenantguard/commit/17bd74ca2d000780980762ffc1fa5ebc7092ea3a))
* **model:** clearer field-trial messages, tool errors, ProbeReport.ToString and an EF Core 10.0.0 floor ([#106](https://github.com/jimmykurian/tenantguard/issues/106)) ([1f3a0c1](https://github.com/jimmykurian/tenantguard/commit/1f3a0c16554e0ad14e415f4d6cd245d3e88516ce))
* **probe:** add WithoutTenant and the NoTenantIsolation check ([#102](https://github.com/jimmykurian/tenantguard/issues/102)) ([a504293](https://github.com/jimmykurian/tenantguard/commit/a504293fc8e7a015fbea85f5669d65821e3a5df4))
* **seeding:** seed constructor-bound types, collections, JSON and tables of tenants ([#96](https://github.com/jimmykurian/tenantguard/issues/96)) ([64156fb](https://github.com/jimmykurian/tenantguard/commit/64156fbcc5105178a7d9ad8a6be2266a49ca6952))


### Bug Fixes

* **probe:** skip checks another filter makes pass, and seed bools false ([#94](https://github.com/jimmykurian/tenantguard/issues/94)) ([1387758](https://github.com/jimmykurian/tenantguard/commit/1387758a40134b1a4bcd9177a1c7cf6ddb8b716e))


### Documentation

* **analyzers:** specify SQL in locals, tenant comparisons, unnamed filters and TG005 ([#103](https://github.com/jimmykurian/tenantguard/issues/103)) ([8401f0f](https://github.com/jimmykurian/tenantguard/commit/8401f0fc034209fef42c84a570b24508580b914d))
* **model:** specify rules for natural keys, tenant-free views, switchable filters and unscoped dependents ([#97](https://github.com/jimmykurian/tenantguard/issues/97)) ([5549a2d](https://github.com/jimmykurian/tenantguard/commit/5549a2d144c32d5cf714b7099a70ceb809f182f4))
* **model:** specify the field-trial messages, tool errors, ProbeReport.ToString and an EF Core 10.0.0 floor ([#105](https://github.com/jimmykurian/tenantguard/issues/105)) ([ad855e7](https://github.com/jimmykurian/tenantguard/commit/ad855e7ba93904fd9ef77384a20911a74a138c26))
* **probe:** specify probing a context with no tenant ([#101](https://github.com/jimmykurian/tenantguard/issues/101)) ([adab644](https://github.com/jimmykurian/tenantguard/commit/adab644154cbd243260112481f837fd7a3b3a636))

## [0.1.0-preview.10](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.9...v0.1.0-preview.10) (2026-11-06)


### Documentation

* accept ADRs 0013, 0014 and 0015 ([#88](https://github.com/jimmykurian/tenantguard/issues/88)) ([99c5e63](https://github.com/jimmykurian/tenantguard/commit/99c5e63d9145c8f7ba37ee134825d104b09462c3))
* describe the wiki as a start page that points to the docs ([#92](https://github.com/jimmykurian/tenantguard/issues/92)) ([a9dfbe3](https://github.com/jimmykurian/tenantguard/commit/a9dfbe348925b991ae5b4458831f85d0861e78e2))
* drop the semicolons that broke three diagrams on the flows page ([#93](https://github.com/jimmykurian/tenantguard/issues/93)) ([8aa1cd0](https://github.com/jimmykurian/tenantguard/commit/8aa1cd08dfa94b6f5a63712629b3413953d4993d))

## [0.1.0-preview.9](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.8...v0.1.0-preview.9) (2026-11-05)


### Features

* **analyzers:** check raw SQL through Dapper, ADO.NET and configured methods ([#57](https://github.com/jimmykurian/tenantguard/issues/57)) ([8b05b0f](https://github.com/jimmykurian/tenantguard/commit/8b05b0f2121c5cd8c7f73d3138dd35c427e2de47))
* **model:** add AllowCapturedValues for a model per tenant ([#59](https://github.com/jimmykurian/tenantguard/issues/59)) ([18112c8](https://github.com/jimmykurian/tenantguard/commit/18112c8574f5ff1d8b5a55fad9868cb151849813))
* **model:** add TGM009 and TGM010 for the tenant index ([#56](https://github.com/jimmykurian/tenantguard/issues/56)) ([22ca3f0](https://github.com/jimmykurian/tenantguard/commit/22ca3f0aa532423e91ce3a7df96c78b4cdf4c800))
* **options:** declare cross-tenant data, each with a reason ([#87](https://github.com/jimmykurian/tenantguard/issues/87)) ([750dec7](https://github.com/jimmykurian/tenantguard/commit/750dec768854b60b15e2fe5118738a5067819188))
* **probe:** add the NavigationLeak check ([#55](https://github.com/jimmykurian/tenantguard/issues/55)) ([afebb55](https://github.com/jimmykurian/tenantguard/commit/afebb553e95e85acc4fa0299be15dfae4ea28301))
* **probe:** add xUnit v3 and TUnit adapters that make each probe row a test ([#58](https://github.com/jimmykurian/tenantguard/issues/58)) ([8f239b0](https://github.com/jimmykurian/tenantguard/commit/8f239b04b1bff6323632bef25938d8ffc9e8cd78))
* **tool:** add dotnet tenantguard validate ([#53](https://github.com/jimmykurian/tenantguard/issues/53)) ([fa54c21](https://github.com/jimmykurian/tenantguard/commit/fa54c216225ef99b123f809023287b24b5454dd0))


### Bug Fixes

* **analyzers:** skip TG001 on a type with no tenant member under tenant_members_only ([#85](https://github.com/jimmykurian/tenantguard/issues/85)) ([61b6a14](https://github.com/jimmykurian/tenantguard/commit/61b6a14cfc3f3b7dfd19006776105d01a4c96768))
* **model:** fold bool constants in the tenant filter before the rules look ([#83](https://github.com/jimmykurian/tenantguard/issues/83)) ([da67f12](https://github.com/jimmykurian/tenantguard/commit/da67f12a7e0b9df203aa4f39fb330594fbba48ae))
* **model:** resolve an EF.Property name held in a captured string ([#76](https://github.com/jimmykurian/tenantguard/issues/76)) ([020fd6e](https://github.com/jimmykurian/tenantguard/commit/020fd6e5fa7bc8e95f3ba2bd2f675d94dfe15fdf))
* **model:** skip an owned type's ownership key in TGM008 ([#79](https://github.com/jimmykurian/tenantguard/issues/79)) ([e23076a](https://github.com/jimmykurian/tenantguard/commit/e23076ab32062613f17a7b92465ac666dde1f4c3))
* **model:** skip TGM010 on a unique index over a tenant-scoped foreign key ([#80](https://github.com/jimmykurian/tenantguard/issues/80)) ([935855e](https://github.com/jimmykurian/tenantguard/commit/935855ec7ee21d7e0763aa5b46ef2d1fa7d88fd5))
* **probe:** pass a guard check the app's own SaveChanges blocks ([#81](https://github.com/jimmykurian/tenantguard/issues/81)) ([bff297b](https://github.com/jimmykurian/tenantguard/commit/bff297b64102ec6938f80f964647dd3d7d037f6c))
* **probe:** pass an insert the app's SaveChanges stamps with the current tenant ([#82](https://github.com/jimmykurian/tenantguard/issues/82)) ([87f7488](https://github.com/jimmykurian/tenantguard/commit/87f7488ef6997c103c591a81da67795238ac9722))
* **tool:** load the payload's own runtime package for an app without TenantGuard ([#84](https://github.com/jimmykurian/tenantguard/issues/84)) ([af8e9cf](https://github.com/jimmykurian/tenantguard/commit/af8e9cfde788d57c61998c4b62c328252135b6e2))


### Documentation

* give the docs site a design of its own ([#78](https://github.com/jimmykurian/tenantguard/issues/78)) ([82fdaf5](https://github.com/jimmykurian/tenantguard/commit/82fdaf554c1b242c5fa2633e26b96613a9347d37))
* move the docs site to tenantguard.is-cool.dev ([#52](https://github.com/jimmykurian/tenantguard/issues/52)) ([8003b18](https://github.com/jimmykurian/tenantguard/commit/8003b180670a92ba0a1396ea7f096e68b72e4412))
* name the TUnit adapter's real base class ([#60](https://github.com/jimmykurian/tenantguard/issues/60)) ([9ac4222](https://github.com/jimmykurian/tenantguard/commit/9ac4222f31b23d97a02acf7d561a0e609fca788b))
* **options:** specify declarations for cross-tenant data ([#86](https://github.com/jimmykurian/tenantguard/issues/86)) ([d963334](https://github.com/jimmykurian/tenantguard/commit/d963334425941c2bfa52bdeeb9480314ff519a23)), closes [#67](https://github.com/jimmykurian/tenantguard/issues/67)

## [0.1.0-preview.8](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.7...v0.1.0-preview.8) (2026-11-02)


### Documentation

* make the README heading a TenantGuard wordmark, and drop its license section ([#49](https://github.com/jimmykurian/tenantguard/issues/49)) ([7756594](https://github.com/jimmykurian/tenantguard/commit/7756594160e39298016522aa9a08daa2816b74eb))
* use the TenantGuard wordmark in the docs site's header ([#51](https://github.com/jimmykurian/tenantguard/issues/51)) ([187374c](https://github.com/jimmykurian/tenantguard/commit/187374c9be205cc54ba2d6731cec88d012afd204))

## [0.1.0-preview.7](https://github.com/jimmykurian/tenantguard/compare/v0.1.0-preview.6...v0.1.0-preview.7) (2026-11-02)


### Documentation

* add a site favicon and ACCESSIBILITY.md, and slim AGENTS.md ([#47](https://github.com/jimmykurian/tenantguard/issues/47)) ([619be25](https://github.com/jimmykurian/tenantguard/commit/619be25c655bdb99d267d85f399d2aa044bc13f9))
* give the docs site the Rapide theme ([#45](https://github.com/jimmykurian/tenantguard/issues/45)) ([031a6bb](https://github.com/jimmykurian/tenantguard/commit/031a6bb4165935ee19b43c9ebfdb1b0edb7c4579))

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
