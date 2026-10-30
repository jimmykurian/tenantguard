# TenantGuard

EF Core packages that prove a multi-tenant model leaks nothing: startup model validation,
a SaveChanges interceptor, Roslyn analyzers and a test kit. Library-agnostic: it works on
Finbuckle, on any of the tenancy packages, and on hand-rolled query filters.

`docs/SPEC.md` says what to build. `docs/PLAN.md` says in what order. This file says how
to work in this repo. Read all three before writing code.

## Commands

Everything runs through the Taskfile. CI calls the same tasks, and ci-parity enforces that.

- `task build` restores and builds the solution, warnings as errors
- `task test` runs every test project; `task test:unit`, `task test:analyzers`,
  `task test:behavior` run one group
- `task lint` runs `dotnet format --verify-no-changes`, the Roslyn analyzers, and
  `task lint:api` (PublicAPI.Shipped/Unshipped are up to date)
- `task fmt` formats
- `task pack` builds the NuGet packages into `./artifacts/`
- `task samples` builds and smoke-tests `samples/`
- `task scrub` greps the tree for terms in `$SCRUB_TERMS_FILE` (see Clean room)
- `task ci` runs lint, build, test, pack, samples, scrub

`task ci` must be green before you call any step done.

## Decisions already made (do not relitigate)

- Library targets `net10.0` and `net11.0`. The `net10.0` build references EF Core 10.x,
  the `net11.0` build references EF Core 11.x. EF 11 only APIs sit behind
  `#if NET11_0_OR_GREATER`.
- Analyzer targets `netstandard2.0`, references `Microsoft.CodeAnalysis.CSharp` 4.8.0
  (lowest version that supports what we use; keeps VS 2022 working), and is bundled into
  the runtime package under `analyzers/dotnet/cs`.
- .NET SDK 11 pinned in `global.json` with `rollForward: latestFeature`. C# latest.
- Central Package Management (`Directory.Packages.props`). Nullable on,
  `TreatWarningsAsErrors`, deterministic builds, SourceLink, `PublicApiAnalyzers`.
- Runtime package dependencies: `Microsoft.EntityFrameworkCore` only. No provider, no
  tenancy library, no logging package beyond what EF already brings. Tests use xUnit v3,
  `Microsoft.EntityFrameworkCore.Sqlite`, and `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.
  Finbuckle appears only in `samples/`.
- Model validation runs as an EF convention (`IModelFinalizedConvention` via an
  `IDbContextOptionsExtension` plugin), not by replacing `IModelValidator`.
- Rule IDs, messages, option names and exception types are fixed by `docs/SPEC.md`.
  Changing one is a spec change.
- License Apache-2.0. One LICENSE at the root; no per-file headers.

If a decision turns out to be wrong, write the problem and the proposed change in
`docs/DECISIONS.md`, then stop and ask. Do not quietly work around it.

## Code conventions

- `src/TenantGuard.EntityFrameworkCore`: `Options/`, `Model/` (conventions, rules),
  `Interception/`, `Diagnostics/`. `src/TenantGuard.Analyzers`: one file per rule.
  `src/TenantGuard.Testing`: `Probe/`, `Seeding/`, `Report/`.
- Public surface is small and documented with XML docs; everything else `internal`
  with `InternalsVisibleTo` for the test projects.
- Error messages name the entity, the property and the fix. Never include entity
  property values other than tenant ids and key values in exceptions or logs.
- No reflection over user types at query time. Per-model metadata is computed once and
  cached on the `IModel` (annotation or `ConditionalWeakTable`).
- No async-over-sync, no `Task.Result`. Interceptors implement both sync and async paths.

## Testing

- Every rule has a positive and a negative model in `tests/.../Models/`.
- `tests/TenantGuard.BehaviorPins` holds tests that pin the EF Core behaviors the design
  relies on (SPEC section 9). If a pin fails on a new EF version, stop and ask.
- Analyzer tests use the `CSharpAnalyzerTest` verifier with `{|TG001:...|}` markup.
- The test kit is tested against itself: a deliberately leaky model must make the probe
  fail with the expected rows.
- Samples are synthetic: `acme`, `globex`, `initech`. No real company or client names.

## Clean room

This is a from-scratch implementation. Do not read, copy or adapt code from any client
codebase, audit, or other repository on this machine. No client names, finding IDs,
ticket IDs or internal URLs anywhere in the tree. The README's audit story is told
generically ("an audit of a SaaS codebase").

`$SCRUB_TERMS_FILE` points at a private term list outside the repo. `task scrub` greps
for them and must pass before every commit; unset, it warns and passes.

## Workflow

- Work one phase of `docs/PLAN.md` at a time. Tick its checkboxes as you go.
- When a phase's acceptance criteria pass and `task ci` is green, commit, summarize in a
  few lines, and stop. Do not start the next phase until asked.
- Conventional commits with the types and scopes in `.gitmessage`, which
  `.githooks/commit-msg` checks. Small commits. Branch and PR; never commit to `main`.
- Every `run:` step in this repo's workflows calls `task`. Third-party actions pinned to
  a full SHA with a version comment.
- Keep this file under 150 lines and `README.md` accurate at the end of every phase.

## Do not

- Do not implement anything under Phase 6 (backlog) in `docs/PLAN.md`.
- Do not add an option, rule or public type the spec does not list. Propose it first.
- Do not take a dependency on Finbuckle or any tenancy library outside `samples/`.
- Do not add a `CLAUDE.md`. Claude Code (2.1.277 or later) reads `AGENTS.md` directly, but
  only when no `CLAUDE.md` exists.
