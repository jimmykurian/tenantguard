# TenantGuard

EF Core packages that prove a multi-tenant model leaks nothing. Query filters isolate
tenants until someone forgets one, switches it off or writes around it; TenantGuard catches
each of those at build (Roslyn analyzers), at startup (model rules), on `SaveChanges` (a
write guard) and in tests (an isolation probe). It works on Finbuckle, any tenancy package
and hand-rolled query filters.

## Map

- `src/TenantGuard.EntityFrameworkCore`: the runtime package, in rings from the inside out:
  `Options/`, `Model/`, `Diagnostics/`, `Interception/`, `Hosting/`
- `src/TenantGuard.Analyzers`: TG001 to TG004, one file per rule
- `src/TenantGuard.Testing`: the isolation probe (`Probe/`, `Seeding/`, `Report/`);
  `src/TenantGuard.Testing.XunitV3` and `.TUnit`: each probe row as a test
- `src/TenantGuard.Tool`: `dotnet tenantguard validate`; `src/TenantGuard.Tool.Payload` runs
  inside the app
- `tests/unit/`, `tests/architecture/`, `tests/integration/`, `tests/e2e/`: one task each
- `samples/`: a hand-rolled app and a Finbuckle app; `site/`: the docs site, built from `docs/`
- `docs/reference/`: what TenantGuard does, word for word. It is the contract: rule IDs,
  messages, options, exceptions and the public API
- `docs/architecture/decisions/`: why it is built this way, one record per decision

## Verify

Everything runs through the Taskfile, and CI runs the same tasks.

- `task ci`: lint, build, test, pack, samples, scrub and the docs site. It must pass before
  any step is done.
- `task build`, `task test` (or `task test:unit`, `test:architecture`, `test:integration`,
  `test:e2e`), `task lint`, `task fmt`; `task --list` has the rest.

Formatting, headers, the public API files, commit messages and the ring dependencies are
checked by `task lint`, the git hooks and `tests/architecture/`: run them rather than
checking by hand.

## Read before you start

Read the ones that fit the task:

- [CONTRIBUTING.md](CONTRIBUTING.md): setup, every task, code layout and conventions, tests,
  commits, hooks, pull requests and releases
- [docs/architecture/README.md](docs/architecture/README.md): packages, flows and vocabulary
- [docs/architecture/decisions/README.md](docs/architecture/decisions/README.md): the
  decision index; read the records your change touches
- [docs/reference/README.md](docs/reference/README.md): the page for the rule, option or
  check you are changing
- [docs/testing/strategy.md](docs/testing/strategy.md): which test goes where, the test
  libraries and the EF Core behavior pins
- [docs/deployment/releases-and-ci.md](docs/deployment/releases-and-ci.md): the workflows
- [ACCESSIBILITY.md](ACCESSIBILITY.md): rules for messages, output and docs

## Rules

- Work one issue at a time. A feature issue (labelled `spec`) gets its reference page, and
  a decision record when it changes a decision, agreed before any code. Do not start a
  roadmap issue until asked.
- Do not add an option, rule or public type that `docs/reference/` doesn't list; propose it
  first.
- Decisions are settled. If one turns out wrong, write a record with status Proposed, then
  stop and ask. If a behavior pin fails on a new EF Core version, stop and ask.
- The runtime package depends on EF Core only. Finbuckle and other tenancy libraries appear
  only in `samples/` and `tests/e2e/`.
- Clean room: this is a from-scratch implementation. Do not read, copy or adapt code from
  any client codebase, audit or other repository on this machine. No client names, finding
  IDs, ticket IDs or internal URLs anywhere in the tree; tests and samples use `acme`,
  `globex` and `initech`. `task scrub` checks the tree against `$SCRUB_TERMS_FILE`.
- Branch and pull request, never `main`; fill in `.github/PULL_REQUEST_TEMPLATE.md`. When the
  acceptance criteria pass and `task ci` is green, commit, summarize in a few lines and
  stop.
- Keep `README.md` and `docs/` accurate with every change. Keep this file short: guidance
  for one kind of task goes in the doc that covers it, listed above.
- Do not add a `CLAUDE.md`. Claude Code (2.1.277 or later) reads `AGENTS.md` directly, but
  only when no `CLAUDE.md` exists.
