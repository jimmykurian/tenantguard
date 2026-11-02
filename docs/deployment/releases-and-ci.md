# Releases and CI

How this repository checks every change and ships every release. Every `run:` step in its
workflows calls a task from the `Taskfile.yml`, and `ci-parity` checks that they do, so CI
and a local `task ci` run the same commands.

## Workflows

| Workflow | Runs | Does |
| --- | --- | --- |
| `ci.yml` | Every pull request, every push to `main` | One step per check: format, public API, license headers, LightBDD scenarios, code metrics, build, the unit, architecture, integration and end-to-end tests, pack, samples, scrub and the docs site, each running even after an earlier one failed. A `ci-parity` job checks the workflows. On a pull request, a `report` job posts a comment with a table of every check's result, and updates it on each push. On `main`, a `badges` job writes one shields.io endpoint file per check to the `badges` branch for the README's status table. |
| `providers.yml` | On demand, nightly, and on pull requests that change the runtime or test kit packages, the shared probe test models, the package versions or the provider tests | The isolation probe on SQL Server and PostgreSQL in Testcontainers; never required to merge |
| `release.yml` | Every push to `main` | Release Please, then the publish job for a new release |
| `site.yml` | Pushes to `main` that change `docs/`, `site/`, the logo or the changelog | Builds the docs site and deploys it to GitHub Pages |
| `labeler.yml`, `pr-assign-author.yml` | Pull requests | Labels by changed paths; assigns the author, or the owner for a bot's |

## Docs site

`site.yml` publishes the docs site at <https://tenantguard.is-a.dev/>, from the root of the
domain. The domain is a subdomain of [is-a.dev](https://is-a.dev): `domains/tenantguard.json`
in [is-a-dev/register](https://github.com/is-a-dev/register) points it at
`jimmykurian.github.io` with a CNAME record, and the repository's Pages settings name it as
the custom domain with HTTPS enforced. GitHub redirects the old
`jimmykurian.github.io/tenantguard/` URLs to it.

## Releases

Release Please keeps one release pull request open, bumping the `Version` in
`Directory.Build.props` and `CHANGELOG.md` from the Conventional Commits on `main`. Merging
it creates the tag and the GitHub Release; the publish job then runs `task pack`, signs in
to nuget.org with GitHub's OIDC token through NuGet Trusted Publishing (no stored API key),
pushes the packages with `task release:push`, and attaches the `.nupkg` and `.snupkg` files
to the GitHub Release with `task release:attach`.

Until EF Core 11 is generally available, releases are previews (`0.1.0-preview.N`), marked
as prereleases on NuGet and GitHub
([0016](../architecture/decisions/0016-first-release-is-a-preview.md)).
[CONTRIBUTING.md](https://github.com/jimmykurian/tenantguard/blob/main/CONTRIBUTING.md) lists
what changes for the first stable release.

## Branch protection

`main` takes changes through pull requests only, with the `ci` and `ci-parity` checks
passing; force pushes and deletion are blocked. Merged branches are deleted automatically.

## Security

GitHub runs secret scanning, with push protection that blocks a push carrying a secret, and
CodeQL's default setup, which scans the C#, the workflows and the docs site's scripts on
every pull request and weekly. Private vulnerability reporting takes reports as
[SECURITY.md](../../SECURITY.md) describes.

Dependabot raises alerts and opens security updates only; `.github/dependabot.yml` turns
version updates off. Its pull requests have Conventional Commit titles: `fix(deps):` for
NuGet packages, so a fix to a package TenantGuard ships with releases a patch, `build(deps):`
for the docs site's packages and `ci(deps):` for the workflows' actions.
