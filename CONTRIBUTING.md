# Contributing

Thanks for helping with TenantGuard. Bug reports, wrong findings and focused pull
requests are all welcome. Everyone taking part follows the
[Code of Conduct](CODE_OF_CONDUCT.md). Security issues go through
[SECURITY.md](SECURITY.md), not public issues.

Questions and early ideas go to
[Discussions](https://github.com/jimmykurian/tenantguard/discussions). The
[wiki](https://github.com/jimmykurian/tenantguard/wiki) is for notes from people using
TenantGuard; what TenantGuard does is in [docs/](docs/README.md), and the wiki doesn't
change it.

## Before you start

TenantGuard's behavior is a contract, so most changes start in the docs:

- [docs/reference/](docs/reference/README.md) says what TenantGuard does, word for word: the
  model rules, the write guard, the analyzers, the probe, their messages and the options;
  the `PublicAPI.Shipped.txt` files list the public API. A new option, rule, public type or
  dependency, or a change to a rule ID, a message, an option name or an exception type,
  needs agreement in an issue first, and updates the reference in the same pull request as
  the code ([Compatibility](docs/reference/compatibility.md)).
- [docs/architecture/](docs/architecture/README.md) says how it is built, and
  [its decision records](docs/architecture/decisions/README.md) say why. A change to a
  decision starts as a record with status Proposed; work on it waits until it is accepted
  or rejected.
- The [roadmap](https://github.com/users/jimmykurian/projects/2) says what's planned next,
  by milestone.
- [AGENTS.md](AGENTS.md) has the working rules for this repo, for people and coding
  agents alike.

For anything bigger than a small fix, open an issue first so we can agree on the
approach.

## Getting started

**Prerequisites:**

- The .NET SDK that `global.json` pins (the .NET 11 SDK, a release candidate until .NET
  11 ships), and the .NET 10 runtime for the samples, which target `net10.0`. The
  `net10.0` test builds roll forward to .NET 11 when the .NET 10 runtime is missing; CI
  installs both.
- [Task](https://taskfile.dev).
- For the git hooks: gitleaks, shellcheck, actionlint and zizmor. A hook check whose tool
  isn't installed is skipped with a warning.
- For `task test:providers` only: Docker.

**Setup:**

```sh
git clone https://github.com/jimmykurian/tenantguard.git
cd tenantguard
git config commit.template .gitmessage

task hooks:install
task ci
```

Every command runs through the [Taskfile](Taskfile.yml), and CI runs the same tasks:

| Task | What it does |
| --- | --- |
| `task build` | restores and builds the solution, warnings as errors |
| `task test` | runs the unit, architecture, integration and end-to-end tests |
| `task test:unit` | `tests/unit/`: the runtime and analyzer tests |
| `task test:architecture` | `tests/architecture/`: the dependency and public-surface rules over the shipped assemblies |
| `task test:integration` | `tests/integration/`: the EF Core behavior pins and the test kit's own tests |
| `task test:e2e` | `tests/e2e/`: the isolation probe against each sample app |
| `task test:providers` | the isolation probe on SQL Server and PostgreSQL in Testcontainers; needs Docker, and isn't part of `task test` |
| `task lint` | `lint:format`, `lint:api`, `lint:headers`, `lint:scenarios` and `metrics:check` |
| `task lint:format` | `dotnet format --verify-no-changes` on the solution, the samples, the end-to-end tests and `tools/` |
| `task lint:api` | fails when `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` is out of date |
| `task lint:headers` | checks the YAML files and git hooks start with the copyright line; StyleCop checks C# |
| `task lint:scenarios` | fails on LightBDD's `[Scenario]`, which xunit.v3 4 silently skips |
| `task fmt` | formats the solution, the samples, the end-to-end tests and `tools/` |
| `task metrics` | prints the code metrics: files, lines of code and method complexity, read by `tools/Metrics` |
| `task metrics:readme` | writes them into the README's METRICS block; run it after a code change, since `metrics:check` fails until you do |
| `task pack` | builds the NuGet packages into `./artifacts/`, then checks their metadata, symbols and docs with Meziantou's NuGet package validator |
| `task release:push` | pushes the packages to nuget.org with the key in `$NUGET_API_KEY`; the release workflow runs it |
| `task release:attach -- vX.Y.Z` | attaches the packages to that GitHub Release; the release workflow runs it |
| `task samples` | builds the samples and smoke-tests each one |
| `task site:install` | installs the docs site's packages from `site/pnpm-lock.yaml`; refuses a lockfile that records a registry mirror's URLs |
| `task site:check` | runs the docs sync script's tests, then builds the site, failing on a broken link or anchor |
| `task site:dev` | serves the docs site with live reload; run `task site:sync` after editing `docs/` |
| `task scrub` | greps the tree for the private terms in `$SCRUB_TERMS_FILE`; warns and passes when it isn't set |
| `task ci` | lint, build, test, pack, samples, scrub and the docs site, in order |
| `task hooks:install` | turns on the git hooks in `.githooks/` |

## How the code is organized

- `src/TenantGuard.EntityFrameworkCore`: the runtime package, in rings from the inside out.
  `Options/` holds the options, the builder and the attributes users put on their code,
  `Model/` the model rules, `Diagnostics/` the logging, `Interception/` the write guard, and
  `Hosting/` the composition root: `UseTenantGuard`, `AddTenantGuardStartupValidation`, and
  the EF convention that runs the rules. A ring depends only on rings further in, and
  `RingRules` in `tests/architecture/` fails on a dependency that points outward.
- `src/TenantGuard.Analyzers`: TG001 to TG004, one file per rule, on
  `Microsoft.CodeAnalysis.CSharp` 4.8.0. The runtime package carries them too.
- `src/TenantGuard.Tool`: `dotnet tenantguard validate`, which starts
  `src/TenantGuard.Tool.Payload` inside the app's own runtime and dependencies (ADR 0024).
- `src/TenantGuard.Testing`: the isolation probe. `Probe/` runs the checks, `Seeding/`
  creates the rows, and `Report/` holds the report and its table.
- `src/TenantGuard.Testing.XunitV3` and `src/TenantGuard.Testing.TUnit`: the test adapters,
  one attribute each, that make each probe row a test (ADR 0025).
- `tests/unit/`: the runtime, analyzer and tool tests, in process.
- `tests/architecture/`: [NetArchTest](https://github.com/NeVeSpl/NetArchTest.eNhancedEdition)
  rules over the shipped assemblies: what each may depend on, where public types
  live, sealed exceptions, and that every interceptor has both its sync and async path.
- `tests/integration/`: the EF Core behavior pins, the test kit's own tests, and the
  provider tests.
- `tests/e2e/`: a test project per sample app.
- `samples/`: a hand-rolled app and a Finbuckle app.

The public surface is small and documented with XML docs; everything else is `internal`,
with `InternalsVisibleTo` for the test projects. `PublicAPI.Shipped.txt` holds the
released API, and `PublicAPI.Unshipped.txt` every change since.

## Code conventions

What the analyzers and tests don't check for you:

- **Messages name the entity, the property and the fix.** An exception, log message or
  diagnostic is part of the contract, and reads as a sentence on its own
  ([ACCESSIBILITY.md](ACCESSIBILITY.md)). It never includes an entity's property values
  other than tenant ids and key values; `NoValueLeakTests` checks it.
- **No reflection over user types at query time.** Per-model metadata is computed once and
  cached on the `IModel`, as an annotation or in a `ConditionalWeakTable`.
- **No async-over-sync and no `Task.Result`.** Interceptors implement both the sync and the
  async path; `tests/architecture/` checks both exist.
- **EF Core 11-only APIs** sit behind `#if NET11_0_OR_GREATER`
  ([0010](docs/architecture/decisions/0010-target-net10-and-net11.md)).
- **Public types** live in the `TenantGuard` and `TenantGuard.Testing` namespaces, the test adapters' too, whatever
  their folder ([0007](docs/architecture/decisions/0007-flat-public-namespaces.md)).
- **File headers:** C# files start with the StyleCop header set in `stylecop.json`, and YAML
  files and scripts with the same copyright line as a comment. Other files have none
  ([0011](docs/architecture/decisions/0011-per-file-copyright-headers.md)).

## Tests

- **Tests** use xUnit v3 and assert with
  [AwesomeAssertions](https://awesomeassertions.org) (`x.Should().Be(...)`), not
  `Assert.*`. Where a rule holds for every input, a property test with
  [FsCheck](https://fscheck.github.io/FsCheck/) checks it; fakes of interfaces use
  [NSubstitute](https://nsubstitute.github.io/); long user-facing output is a
  [Verify](https://github.com/VerifyTests/Verify) snapshot (`*.verified.txt`, next to the
  test); [Bogus](https://github.com/bchavez/Bogus) fills entity values where a test checks
  that messages leave them out. Every model rule has a positive and a negative model in
  `tests/unit/TenantGuard.EntityFrameworkCore.Tests/Models/`.
- **Integration tests** read as [LightBDD](https://github.com/LightBDD/LightBDD) scenarios:
  the feature class derives from `FeatureFixture` with a `[FeatureDescription]`, and each
  test is a `[Fact]` that runs Given/When/Then step methods through
  `Runner.RunScenarioAsync`. Use `[Fact]`, not `[Scenario]`: LightBDD.XUnit3 3.12.1 does not
  load on xunit.v3 4, and the test platform then drops `[Scenario]` tests without a word
  ([LightBDD#409](https://github.com/LightBDD/LightBDD/issues/409)); `task lint:scenarios`
  catches it. LightBDD's HTML report lands in each project's `bin/.../Reports/`.
- **Analyzer tests** run each rule through `CSharpAnalyzerTest` and mark where a
  diagnostic lands with `{|TG001:...|}`.
- **Behavior pins** in `tests/integration/TenantGuard.BehaviorPins` pin the EF Core behaviors the
  design relies on ([the list](docs/testing/strategy.md#behavior-pins)). If one fails on a new EF Core version, stop and open an
  issue.
- **The test kit is tested against itself:** a deliberately leaky model must make the
  probe fail with the expected rows.
- **End-to-end tests** in `tests/e2e/` run the probe against each sample app's own
  model and registration, in `task test:e2e`.
- **Provider tests** in `tests/integration/TenantGuard.Providers.Tests` run the probe on SQL Server
  and PostgreSQL in Testcontainers, so they need Docker. `task test` leaves them out;
  `providers.yml` runs them on demand, nightly, and on pull requests that change the
  runtime or test kit packages, the shared probe test models, the package versions or
  the provider tests, and they are never required to merge.

Use synthetic names in tests and samples, with tenants such as `acme`, `globex` and
`initech`.

## Commits

Messages follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).
`.gitmessage` lists the types and scopes:

```text
feat(model): report TGM003 when a tenant filter captures a local
fix(interception): compare tenant values with the property's ValueComparer
test(pins): pin query filters applying to FindAsync
```

Rule IDs, messages, option names, exception types and the public API are public
contracts: changing one is a breaking change, marked with `!` or a `BREAKING CHANGE`
footer.

Commits should be signed so GitHub shows them as Verified. With an SSH key:

```sh
git config gpg.format ssh
git config user.signingkey ~/.ssh/id_ed25519.pub
git config commit.gpgsign true
```

Then add the same public key to GitHub as a signing key.

## Git hooks

`task hooks:install` points git at `.githooks/`:

- **pre-commit** refuses commits on `main`, then runs gitleaks and `task scrub`, and
  checks what's staged: the whitespace of C# files, the copyright line of YAML files and
  hooks, shellcheck on scripts and hooks, and actionlint and zizmor on workflows.
- **commit-msg** checks the header against Conventional Commits, with the types and
  scopes in `.gitmessage`.
- **pre-push** refuses pushes to `main`, then runs `task lint` and `task test` when the
  pushed commits change C# code, project or build files, the Taskfile or the workflows.
  It runs on the working tree, so the pushed commit has to be the clean checkout.

A check whose tool isn't installed is skipped with a warning; CI runs the rest.
`--no-verify` skips the hooks once.

## Clean room

TenantGuard is written from scratch. No code, names or data from a client codebase or an
audit go into it: keep client names, finding IDs, ticket IDs and internal URLs out of the
tree. `task scrub` checks the tree against a private list of terms when
`SCRUB_TERMS_FILE` points at one.

## Pull requests

- `main` takes changes only through pull requests: work on a branch, and the hooks
  refuse commits and pushes on `main`.
- Keep each pull request to one change, and fill in the
  [template](.github/PULL_REQUEST_TEMPLATE.md).
- The PR title follows Conventional Commits. Pull requests are squash-merged, so the
  title becomes the commit on `main`.
- `task ci` must pass. CI runs each of its tasks as its own step, plus ci-parity.
- The PR is labeled by the paths it changes (`.github/labeler.yml`) and assigned to
  whoever opened it; bots' PRs go to the owner. `.github/CODEOWNERS` requests review.
- Every `run:` step in this repo's workflows calls `task`. Setup goes in `uses:` steps
  pinned to a full commit SHA with a version comment.
- Update `README.md`, `docs/` and `AGENTS.md` when a command, option or decision
  changes.

## Releases

[Release Please](https://github.com/googleapis/release-please) cuts releases from the
Conventional Commits on `main` (`.github/workflows/release.yml`):

1. Each push to `main` opens or updates a release pull request. It bumps `<Version>` in
   `Directory.Build.props` and adds the release to `CHANGELOG.md`. `feat` commits raise
   the minor version and `fix` commits the patch, until 1.0.
2. Merging it tags `vX.Y.Z` and creates the GitHub Release.
3. The publish job then runs `task pack`, signs in to nuget.org with GitHub's OIDC token
   through `NuGet/login`, which needs no stored API key, and runs `task release:push` and
   `task release:attach`.

Publishing needs a trusted publishing policy on nuget.org for this repository and
`release.yml`, and the repository variable `NUGET_USER` set to the nuget.org account that
owns it.

Until EF Core 11 is generally available, releases are previews:
`release-please-config.json` sets `versioning` to `prerelease` with `prerelease-type`
`preview`, so each release PR bumps the preview number (`0.1.0-preview.2`, then
`0.1.0-preview.3`), and `prerelease` to true, so the GitHub Release is marked as a
prerelease.

Before the first stable release:

- In `.github/release-please-config.json`, replace `versioning`, `prerelease-type` and
  `prerelease` with `"release-as": "0.1.0"`, and remove `release-as` again once 0.1.0 is
  out. Release Please opens a release PR for the same version while `release-as` names it,
  and merging that PR fails because the tag already exists.
- Move `global.json` and the EF Core 11 packages off the release candidate, and drop
  `allowPrerelease`. A stable package can't depend on a prerelease one.
- Remove the preview note at the top of `README.md` and of `assets/nuget-readme.md`, the
  README nuget.org shows, and the `--prerelease` flags from the install commands in both and
  in `docs/guides/`.

## License

By contributing, you agree that your contributions are licensed under the
[Apache License 2.0](LICENSE).
