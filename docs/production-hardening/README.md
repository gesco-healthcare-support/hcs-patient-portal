# Epic: production hardening

**This folder is committed, deliberately.** It is not a disposable plan. The epic is expected to
outlast the person who started it, so this is the handoff record: the ordered queue, the reasoning
behind the order, what has landed, and what has been ruled out. Keep it current as work lands --
a stale tracker is worse than none, because it will be trusted.

Ordinary RPE plans still belong in `docs/plans/` and stay gitignored. This is a different thing:
a living programme record, not a spec that dies when its work ships.

Branch `feat/production-hardening`, worktree `C:/src/patient-portal/feat-production-hardening`,
created off `main` at `a5234d25` on 2026-08-31.

## Progress

Update this table as each phase closes. It is the first thing a successor will read.

| Phase                    | Status      | Landed | Baseline delta |
| ------------------------ | ----------- | ------ | -------------- |
| 1 Blockers               | NOT STARTED | --     | --             |
| 2 Enforcement            | NOT STARTED | --     | --             |
| 3 Critical-path coverage | NOT STARTED | --     | --             |
| 4 CodeQL sensitive-info  | NOT STARTED | --     | --             |
| 5 Security hotspots      | NOT STARTED | --     | --             |
| 6 Dependencies           | NOT STARTED | --     | --             |
| 7 Rule families          | NOT STARTED | --     | --             |
| 8 Coverage expansion     | NOT STARTED | --     | --             |

**Open decision blocking phase 2:** the SonarCloud new-code coverage gate is set to 80% and is
currently admin-overridden on every PR. It must be either enforced at 80% or lowered to a threshold
that will be respected. See [02-enforcement.md](02-enforcement.md) 2.1.

## The goal, in Adrian's words

> "If I setup and have a strong base, the future developers working on this will always follow it
> and that will lead to less issues in future when this program is in production and way more
> complex to fix the foundation."

> "It is okay if I cannot complete it all and have to handoff but that is the direction we will go
> in. [...] it is not necessary that we will fix all of the issues 100%, some of them may be
> impossible to fix, but I want to fix as many as I can."

Two consequences that shape every decision below:

1. **A handoff is expected and acceptable.** So the ordering is chosen by what survives a handoff,
   not by what fits a calendar. Enforcement outranks volume, because a gate constrains every
   future change including ones made after this team turns over.
2. **Partial completion is a success condition, not a failure.** Every phase must leave the repo
   in a better and coherent state on its own. No phase may depend on a later phase to be safe.

## THE DECISION RULE: tests first, or fixes first?

Adrian's question, and it governs every task:

> "whether we write tests first or fix bugs and vulnerabilities first? I don't want the app to
> regress or cause new issues because we did not have enough tests to guard against that."

Neither answer is right globally. The rule is:

> **Write the test first when you intend to PRESERVE current behaviour.
> Write the test with the fix when you intend to CHANGE current behaviour.**

Applied to this epic:

| Change class                                                                               | Order                            | Why                                                                                                                                                                       |
| ------------------------------------------------------------------------------------------ | -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Security fixes that deliberately change behaviour (the 4 blockers)                         | Test **with** fix                | A test written first would encode the broken behaviour. Write the assertion for the DESIRED behaviour, watch it fail, then fix.                                           |
| Refactors that must preserve behaviour (S3776 cognitive complexity, S107 parameter counts) | Characterization tests **first** | You cannot safely restructure code whose behaviour you have not pinned down. This is the canonical legacy-code case.                                                      |
| Dependency bumps (87 advisories)                                                           | Broad coverage **first**         | The risk is unknown-unknowns. There is no targeted test for "this bump broke nothing"; only a suite catches it. **This is the highest-regression-risk work in the epic.** |
| Compiler-verified mechanical fixes (CA1861, CA1510, S125)                                  | Existing suite suffices          | Behaviour preservation is proven by the compiler and the type system. Adding tests first buys nothing.                                                                    |
| Template/accessibility fixes (330 items)                                                   | Existing suite suffices          | Risk is breaking component specs that query by selector, not app regression. The suite surfaces that immediately.                                                         |

**The practical upshot:** critical-path coverage moves ahead of the dependency bumps in the order.
That is a change from the first draft, made because of this question, and it is correct -- bumping
87 npm packages against 52.4% coverage is exactly the scenario Adrian is worried about.

## Ordering

| #   | Phase                                                       | Items                       | Why here                                                                                       |
| --- | ----------------------------------------------------------- | --------------------------- | ---------------------------------------------------------------------------------------------- |
| 1   | [Blockers](01-blockers.md)                                  | 4 real (of 6 flagged)       | Hours of work, security-relevant, includes an open redirect in the tenancy path                |
| 2   | [Enforcement](02-enforcement.md)                            | ~5 gates                    | Everything after lands behind a gate rather than in front of one. Highest durability per hour. |
| 3   | [Critical-path coverage](03-critical-path-coverage.md)      | 5 areas                     | The safety net for phases 4-8. Must precede the dependency bumps.                              |
| 4   | [CodeQL sensitive information](04-codeql-sensitive-info.md) | 19 alerts, 6 files          | Highest value per unit on a PHI system                                                         |
| 5   | [Security hotspots](05-security-hotspots.md)                | 31 to review (9 HIGH)       | Review-and-decide, not necessarily fix                                                         |
| 6   | [Dependencies](06-dependencies.md)                          | 87 patchable of 88          | Now guarded by phase 3                                                                         |
| 7   | [Rule families](07-rule-families.md)                        | ~1,249 in 100 families      | Bulk. Largest families first; 72% sits in the top 15.                                          |
| 8   | [Coverage expansion](08-coverage-expansion.md)              | 52.4% -> as high as reached | Open-ended. Degrades gracefully because phase 2 stops backsliding.                             |

Running record of what was NOT fixed and why: [00-triage-log.md](00-triage-log.md).

## THE GOVERNING LESSON: triage before fixing

Already proven on this dataset, twice, before any work started:

- Two of the six BLOCKERs are **false positives**. `dev-api.ps1:34` and `dev-authserver.ps1:37` were
  flagged as hardcoded SQL Server passwords. They are not. Both read `MSSQL_SA_PASSWORD` from the
  environment, fall back to parsing `.env`, and fail fast if absent -- the exact pattern the
  security rules require. Sonar's secret detector matched the literal string
  `"^MSSQL_SA_PASSWORD="` used as a `Select-String` search pattern. Verified: zero literal password
  assignments in either file.
- 109 of the 128 "CodeQL alerts" are **not CodeQL findings**. They are OpenSSF Scorecard results
  surfacing through the same API (97 `PinnedDependenciesID`, 7 `TokenPermissionsID`, and singles).
  The real code security alerts number 19.

So the first step of every rule family is **"is this real here?"**, not "fix it". A mechanical
sweep would have "fixed" two non-problems and taught the successor to trust the tool over the code.

The same lesson is already recorded in `docs/research/code-standard-2026-08-28/appendix-D-verification-record.md`
from the previous exercise. It keeps recurring because static analysis has no access to intent.

## Baseline measurements (2026-08-31)

Re-measure at the end of each phase; these are the numbers to beat.

| Metric                  | Value                                                      |
| ----------------------- | ---------------------------------------------------------- |
| Coverage                | 52.4%                                                      |
| Lines of code           | 116,210                                                    |
| Sonar issues open       | 1,280 (922 smell / 338 bug / 20 vulnerability)             |
| Sonar by severity       | 6 BLOCKER / 67 CRITICAL / 496 MAJOR / 387 MINOR / 324 INFO |
| Sonar rule families     | 100 (top 15 = 920 issues = 72%)                            |
| Security hotspots       | 31 TO_REVIEW (9 HIGH)                                      |
| CodeQL real code alerts | 19                                                         |
| Scorecard findings      | 109                                                        |
| Dependabot advisories   | 88 open (87 patchable, all npm, zero NuGet)                |
| Duplication             | 3.0%                                                       |

Query used for Sonar (no auth needed, project is public):

```bash
curl -s "https://sonarcloud.io/api/issues/search?componentKeys=gesco-healthcare-support_hcs-patient-portal&resolved=false&ps=1&facets=severities,types,rules"
```

## Working agreement

- **Task branches off `feat/production-hardening`**, descriptive names, merged back into it. One PR
  at the end: `feat/production-hardening` -> `main`. Then the normal cascade.
- **Each phase file gets researched and detailed before its work starts**, not now. This folder
  captures everything known on 2026-08-31; per-item anchors, EARS acceptance and validation loops
  are added at the point of execution.
- **Every task states its change class** (from the decision-rule table) so the test ordering is not
  re-derived each time.
- **Nothing is marked done without its validation loop run and its output shown.** Per
  `~/.claude/rules/testing.md`, the loop covers every layer the change touches -- a build is not a
  test.
- **Triage decisions go in `00-triage-log.md` with evidence.** That log is the most valuable
  artefact here for a successor. It is committed alongside the rest of this folder, so it needs no
  rescuing when the epic closes -- but it does need keeping current, because its whole value is
  that someone can trust a dismissal without re-deriving it.
- **Update the Progress table in this file whenever a phase closes**, with the commit and the
  measured baseline delta. That table is what makes this folder a handoff rather than a wish list.
  If it drifts out of date the folder becomes actively misleading, which is worse than not having
  written it.
