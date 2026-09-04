# Epic: production hardening

**This folder is committed, deliberately.** It is not a disposable plan. The epic is expected to
outlast the person who started it, so this is the handoff record: the ordered queue, the reasoning
behind the order, what has landed, and what has been ruled out. Keep it current as work lands --
a stale tracker is worse than none, because it will be trusted.

Ordinary RPE plans still belong in `docs/plans/` and stay gitignored. This is a different thing:
a living programme record, not a spec that dies when its work ships.

Branch `feat/production-hardening`, worktree `C:/src/patient-portal/feat-production-hardening`,
created off `main` at `5c83553c` on 2026-08-31.

> **Corrected 2026-09-03.** This said `a5234d25`, which is a real commit on `main` (#494) but not
> this branch's fork point. `a5234d25` was the base of `docs/production-hardening-record`, the
> branch that carried #496, and it was copied here by mistake. The actual fork point is `5c83553c`
> (#496, "commit the production hardening execution record").
>
> ```bash
> git merge-base feat/production-hardening origin/main   # -> 5c83553c
> ```
>
> Related loose end, explained so nobody re-investigates it: `origin/docs/production-hardening-record`
> shows as contained in no branch because it was squash-merged, so its original SHA is not an
> ancestor of `main`. That is expected. **Branches are not deleted here without explicit approval.**

## Progress

Update this table as each phase closes. It is the first thing a successor will read.

| Phase                    | Status                                                        | Landed                                                                                  | Baseline delta                                                                                                          |
| ------------------------ | ------------------------------------------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| 1 Blockers               | **COMPLETE (7/7)**                                            | 046f44f1, ad4cb0d7, fd875d67, 870f5ecf, 14c3f84b, dc134222, e91be9f1, df705ad8          | 5 of 6 flagged were false alarms; 3 real defects found UNFLAGGED. See the closing note.                                 |
| 2 Enforcement            | **COMPLETE (12/12)** -- 9 merged, 2.1 cancelled, 2.5 deferred | #514, #518, #516, #519, #529, #530, #532, #534, #538; 2.11 applied as branch protection | Anti-gate settings **11 -> 5**. 17 checks now REQUIRED on all four branches. Coverage honestly measured on both stacks. |
| 3 Critical-path coverage | **IN PROGRESS** -- 3.1 COMPLETE, 3.2-3.5 next                 | #686 `a8e221df`, #688 `d1d70938`, #689 `c9ad7de8` (on the epic branch)                 | Tenancy went from **0 of 323** backend test files asserting the resolver chain to pinned in BOTH processes, every test seen to fail. `FLOOR_BACKEND` 73 -> 72, honestly measured. |
| 4 CodeQL sensitive-info  | NOT STARTED                                                   | --                                                                                      | --                                                                                                                      |
| 5 Security hotspots      | NOT STARTED                                                   | --                                                                                      | --                                                                                                                      |
| 6 Dependencies           | NOT STARTED                                                   | --                                                                                      | --                                                                                                                      |
| 7 Rule families          | NOT STARTED                                                   | --                                                                                      | --                                                                                                                      |
| 8 Coverage expansion     | NOT STARTED                                                   | --                                                                                      | --                                                                                                                      |
| 9 System design intake   | TRIAGE IN PROGRESS                                            | report received 2026-08-31                                                              | 4 claims refuted, 3 confirmed                                                                                           |

## TRIGGER: when 2.13, 2.14 and phase 3 close, RE-RUN THE SWEEP IMPORT -- see issue #672

**Work tracking moved to GitHub Issues on 2026-09-04.** Phases 1-3 were deliberately EXCLUDED from
that import because this epic had them in flight; only phases 4+ became issues, under milestones
`Hardening phase N`.

**Roughly 200 static-analysis findings are held back because this epic owns their paths.** They are
tracked NOWHERE until released:

| Paths held                             | Findings | Released by     |
| -------------------------------------- | -------- | --------------- |
| `test/`, `tests/`                      | 93       | phase 3         |
| `scripts/`, `docker/`, `**/Dockerfile` | 104      | item 2.14       |
| `.github/`                             | 54       | items 2.2 / 2.6 |
| Angular build config                   | a few    | item 2.13       |

**`gh issue view 672` recovers the full procedure.** It lives on GitHub precisely so it cannot be lost
to a compaction in any session. Counts above were measured 2026-09-04 and move as work lands --
**re-derive them rather than trusting this table.**

**Two things that are easy to get wrong:**

- **`.github/` is gated separately** by 2.2 / 2.6, so it can be released on its own without waiting
  for phase 3.
- **"2.14 is done" does NOT mean those directories are clean.** Completing it fixes the PINNING
  alerts in `scripts/` and the Dockerfiles; the ordinary findings that also live there -- shell code
  smells, clear-text protocol warnings -- are untouched. Closing the item unblocks the paths; it does
  not clear them. That is the whole reason the re-run matters.

The import script is `scripts/maintenance/import-issues.py`, **MERGED to `main` as `2cf1904c`
(PR #671) on 2026-09-04.** #672 stands and is actionable.

> **Corrected 2026-09-04.** This paragraph said the PR was "open and ungated -- Adrian decides that
> merge", with a contingency for him declining it. That was true when written and false a few hours
> later. Nothing reported the change; it was noticed only because a session happened to check the PR's
> state for an unrelated reason. It is catalogue instance 29 -- a record entry correct when written and
> wrong after our own work, still reading plausibly -- occurring in the file that describes it.

**Two items were opened OUT of phase 2 and are not part of its 12:** **2.13** (instrument the Angular
sources coverage could not see -- merged `1ca6c078`, front-end floor 69 -> 20) and **2.14** (container
images, `curl | bash`, npm/pip). Both are in `02-enforcement.md`.

**Read the phase 1 delta carefully -- the BLOCKER count is a bad proxy for progress here.** It stands
at 5, down from 6, and that single drop was a False Positive marking, not a defect repaired. All six
are now triaged: **five issues (four distinct findings, since the PowerShell pair is one false
positive twice) were false alarms, and exactly ONE was a real defect.** In one case -- the packet
renderer -- doing what the scanner asked would have broken PDF generation.

The count will barely move as this phase closes, for two reasons that are both correct: dismissals
need an administrative marking that is batched to the end, and 1.2's issue stays open by design
because its rule fires on a code pattern that was made safe rather than removed. Anyone reading a
falling BLOCKER count as defects repaired will draw the wrong conclusion about this epic --
the triage log is the only honest ledger of what was real. See [00-triage-log.md](00-triage-log.md).

**The phase 1 denominator has changed twice, and neither change was an item quietly disappearing.**
7 -> 8 when 1.8 was added, spawned by 1.3's research rather than by a scanner. Then 8 -> 7 on
2026-09-01 when **1.6 was moved out of the epic entirely** by Adrian's decision -- it became its own
piece of work on `main`, written up at
[`docs/security/SESSION-KEY-ENCRYPTION.md`](../security/SESSION-KEY-ENCRYPTION.md). 1.6 was a REAL
finding; it left because fixing it requires designing certificate custody and a loss-recovery
override, which is a design exercise rather than a hardening task. Section 1.6 in
[01-blockers.md](01-blockers.md) is kept as a pointer rather than deleted, so the trail survives.

**The SonarCloud coverage-gate decision is CLOSED (2026-09-02) and phase 2 is not blocked.** This
paragraph previously said the 80% new-code gate was "the open decision blocking phase 2". Adrian
ruled: **the 80% threshold stays and must not be changed.** A separate, version-controlled CI
coverage check is being built instead -- [02-enforcement.md](02-enforcement.md) item **2.10**. The
cancelled design and the three reasons it was wrong are recorded under item **2.1**.

**Phase 2's live blockers are two decisions, neither of which blocks work:** whether observed-red is
required for the gates that cannot be cheaply poisoned, and the precondition on the `production`
branch requirement. Both are recorded in 02-enforcement.md; work continues around them.

**Phase 9 has no fixed slot.** It runs whenever the external system-design report arrives, takes an
hour or two of triage, and dissolves its findings into the phases above. It must not interrupt a
phase in flight, and it is not a reason to reorder -- see
[09-system-design-intake.md](09-system-design-intake.md).

## Two REQUIRED gates found measuring the wrong thing (phase 3, 2026-09-04)

Both were found by pushing on the gates rather than by any check reporting a problem, and both are
tracked rather than fixed here -- Adrian ruled that the class-level fixes get their own changes rather
than riding on a test PR.

| Issue | The gate                | What it examines instead                                            |
| ----- | ----------------------- | ------------------------------------------------------------------- |
| #683  | `Coverage: Floors`      | counts third-party SourceLink source as ours; a per-vendor denylist  |
| #687  | `Dependency Review`     | scans a committed lock file nothing verifies is current              |

**#683.** Second occurrence of the same class: FluentValidation and Mapperly cost 7.37 points in phase
2, MessagePack 5.36 points in phase 3, each fixed by naming that vendor's SourceLink root. The list
grows one vendor at a time and the next arrives with no signal, because the file count that would
reveal it is a comment rather than a check.

**#687.** `Directory.Build.props:29-31` says CI "can opt in via the dotnet restore flag for locked
mode". CI never opted in -- all five restore invocations are plain. A PR that adds a dependency can
commit a stale lock file, and the REQUIRED gate then scans a graph without the new packages and
reports green. Found live on #686 and fixed there; the hole remains.

**A third suspicion was investigated and is NOT a gap.** Four `Frontend:` checks stay required while
being able to skip, which would be dangerous since a skipped required check reports Success. Phase 2
item 0.5 had already replaced the path filter with a deny-by-default classifier whose `*)` arm runs the
full suite for any unrecognised path, and an empty file list is a hard error. Verified in source rather
than taken from the comment describing it.

## The goal, in Adrian's words

> "If I setup and have a strong base, the future developers working on this will always follow it
> and that will lead to less issues in future when this program is in production and way more
> complex to fix the foundation."
>
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

| #   | Phase                                                       | Items                                | Why here                                                                                       |
| --- | ----------------------------------------------------------- | ------------------------------------ | ---------------------------------------------------------------------------------------------- |
| 1   | [Blockers](01-blockers.md)                                  | 4 real (of 6 flagged)                | Hours of work, security-relevant, includes an open redirect in the tenancy path                |
| 2   | [Enforcement](02-enforcement.md)                            | 11 items                             | Everything after lands behind a gate rather than in front of one. Highest durability per hour. |
| 3   | [Critical-path coverage](03-critical-path-coverage.md)      | 5 areas                              | The safety net for phases 4-8. Must precede the dependency bumps.                              |
| 4   | [CodeQL sensitive information](04-codeql-sensitive-info.md) | 19 alerts, 6 files                   | Highest value per unit on a PHI system                                                         |
| 5   | [Security hotspots](05-security-hotspots.md)                | 31 to review (9 HIGH)                | Review-and-decide, not necessarily fix                                                         |
| 6   | [Dependencies](06-dependencies.md)                          | 87 patchable of 88                   | Now guarded by phase 3                                                                         |
| 7   | [Rule families](07-rule-families.md)                        | ~1,249 in 100 families               | Bulk. Largest families first; 72% sits in the top 15.                                          |
| 8   | [Coverage expansion](08-coverage-expansion.md)              | 52.4% (`main`) -> as high as reached | Open-ended. Degrades gracefully because phase 2 stops backsliding.                             |
| 9   | [System design intake](09-system-design-intake.md)          | Received 2026-08-31                  | No fixed slot. Triage and routing only; findings dissolve into the phases above.               |

Running record of what was NOT fixed and why: [00-triage-log.md](00-triage-log.md).

Repository verification of the system design research:
[10-research-corrections.md](10-research-corrections.md). **Read it before acting on any claim that
research makes about this code.** It had no repository access, and four of the claims checked so far
do not survive contact with source -- including the one its own cost model is most sensitive to.

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
| Coverage                | 52.4% (`main` -- the only branch SonarCloud analyses)      |
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
