# Contributing

Thanks for helping with TenantGuard. Bug reports, wrong findings and focused pull
requests are all welcome. Everyone taking part follows the
[Code of Conduct](CODE_OF_CONDUCT.md). Security issues go through
[SECURITY.md](SECURITY.md), not public issues.

## Before you start

TenantGuard's behavior is a contract, so most changes start in the docs:

- [docs/SPEC.md](docs/SPEC.md) says what TenantGuard does: the model rules, the write
  guard, the analyzers, the test kit, their messages and the public API. A new option,
  rule, public type or dependency, or a change to a rule ID, a message, an option name or
  an exception type, needs agreement in an issue first, and updates the spec in the same
  pull request as the code.
- [docs/PLAN.md](docs/PLAN.md) says in what order it is built.
- [docs/DECISIONS.md](docs/DECISIONS.md) records each problem found with a decision and
  the change proposed for it. Work on a proposal waits until it is accepted or rejected.
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
| `task test` | runs every test project |
| `task test:unit` | the runtime and test kit unit tests |
| `task test:analyzers` | the analyzer tests |
| `task test:behavior` | the EF Core behavior pins (SPEC 9) |
| `task lint` | `dotnet format --verify-no-changes` on the solution and the samples, then `lint:api` and `lint:headers` |
| `task lint:api` | fails when `PublicAPI.Shipped.txt` or `PublicAPI.Unshipped.txt` is out of date |
| `task lint:headers` | checks the YAML files and git hooks start with the copyright line; StyleCop checks C# |
| `task fmt` | formats the solution and the samples |
| `task pack` | builds the NuGet packages into `./artifacts/` |
| `task samples` | builds the samples, smoke-tests each one and runs their isolation probe tests |
| `task scrub` | greps the tree for the private terms in `$SCRUB_TERMS_FILE`; warns and passes when it isn't set |
| `task ci` | lint, build, test, pack, samples and scrub, in order |
| `task hooks:install` | turns on the git hooks in `.githooks/` |

## How the code is organized

- `src/TenantGuard.EntityFrameworkCore`: the runtime package. `Options/` holds
  `UseTenantGuard` and its options, `Model/` the model rules and the convention that runs
  them, `Interception/` the write guard, and `Diagnostics/` the logging and
  `AllowCrossTenantAccessAttribute`.
- `src/TenantGuard.Analyzers`: TG001 to TG004, one file per rule, on
  `Microsoft.CodeAnalysis.CSharp` 4.8.0. The runtime package carries them too.
- `src/TenantGuard.Testing`: the isolation probe. `Probe/` runs the checks, `Seeding/`
  creates the rows, and `Report/` holds the report and its table.
- `tests/`: a test project per package, and `TenantGuard.BehaviorPins`.
- `samples/`: a hand-rolled app and a Finbuckle app, each with a probe test project.

The public surface is small and documented with XML docs; everything else is `internal`,
with `InternalsVisibleTo` for the test projects. `PublicAPI.Unshipped.txt` lists every
public API change.

## Tests

- **Unit tests** use xUnit v3. Every model rule has a positive and a negative model in
  `tests/TenantGuard.EntityFrameworkCore.Tests/Models/`.
- **Analyzer tests** run each rule through `CSharpAnalyzerTest` and mark where a
  diagnostic lands with `{|TG001:...|}`.
- **Behavior pins** in `tests/TenantGuard.BehaviorPins` pin the EF Core behaviors the
  design relies on (SPEC 9). If one fails on a new EF Core version, stop and open an
  issue.
- **The test kit is tested against itself:** a deliberately leaky model must make the
  probe fail with the expected rows.
- **The samples** run the probe against their own models in `task samples`.

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

Releases aren't automated yet. SPEC 12 describes the release workflow to come:
release-please, `task pack`, and NuGet Trusted Publishing.

## License

By contributing, you agree that your contributions are licensed under the
[Apache License 2.0](LICENSE).
