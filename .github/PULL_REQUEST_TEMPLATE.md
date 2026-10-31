# Description

Summarize the change and why it's needed. Link the related issue.

Fixes # (issue)

## Type of change

Delete the options that don't apply.

- [ ] Bug fix: a wrong or missing model finding, analyzer diagnostic, write guard
      decision or probe row, or a crash
- [ ] New behavior within `docs/SPEC.md` (a rule case, an analyzer case, a probe check)
- [ ] Contract change: a rule ID, message, option name, exception type or public API
      (agreed in an issue first; `docs/SPEC.md` updated, and `docs/DECISIONS.md` when it
      changes a decision)
- [ ] Sample change
- [ ] Packaging or release change
- [ ] Build, CI or tooling change
- [ ] Documentation only

## How Has This Been Tested?

- [ ] `task ci` passes (lint, build, test, pack, samples, scrub)
- [ ] A model rule change has a positive and a negative model in
      `tests/TenantGuard.EntityFrameworkCore.Tests/Models/`
- [ ] An analyzer change has `CSharpAnalyzerTest` cases with `{|TG00n:...|}` markup
- [ ] A change that relies on new EF Core behavior pins it in
      `tests/TenantGuard.BehaviorPins`
- [ ] A write guard or test kit change passes the samples' isolation probe tests

## Checklist

- [ ] The PR title follows Conventional Commits, with a scope from `.gitmessage` where
      one fits
- [ ] Commits are signed (Verified)
- [ ] Tests and samples use synthetic tenants such as `acme`, `globex` and `initech`, and
      no client names, finding IDs, ticket IDs or internal URLs
- [ ] No new dependency, option, rule or public type unless agreed first, with
      `docs/SPEC.md` updated
- [ ] `PublicAPI.Unshipped.txt` lists every public API change (`task lint:api`)
- [ ] Error messages name the entity, the property and the fix, and messages and logs
      carry no entity values other than tenant ids and key values
- [ ] Every `run:` step in this repo's workflows calls `task`
- [ ] New third-party actions are pinned to a full commit SHA with a version comment
- [ ] `README.md`, `docs/` and `AGENTS.md` are updated if a command, option or decision
      changed

## Output

Paste the model report, analyzer diagnostics, exception message or probe table before and
after the change, or mark as N/A.
