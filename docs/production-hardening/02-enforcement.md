# Phase 2 -- Enforcement

**Change class:** configuration and CI. Existing suite suffices; the proof is that a deliberately
bad change now FAILS.

**This is the highest-value phase in the epic.** Every later phase produces fixes that decay --
someone reintroduces the pattern six months from now. A gate does not decay. It constrains every
future change, including changes made after this team turns over, which is exactly the "strong base
that future developers will always follow" this epic exists to build.

**Prove every gate by poisoning it.** A gate nobody has watched fail is a gate you are guessing
about. Introduce the violation, watch the build go red, remove it, watch it go green. The Pacific
epic did this for its clock and date-pipe gates and it is the only reason those are trustworthy.

---

## 2.1 Make the SonarCloud new-code coverage gate real

**Today:** the quality gate has a hard condition `new_coverage >= 80%`. It fails on any PR that
adds production code without tests. Adrian admin-overrides it. Confirmed on PR #359 (`actual=0.0
op=LT threshold=80`) and again on #414 (77.8%).

**Why it is the single most important item in the epic.** A gate that is routinely bypassed is
worse than no gate: it trains everyone, including the successor, to treat a red check as noise.
Meanwhile it is _already_ the mechanism that would deliver the stated goal -- every future
modification carrying its own tests -- if it were honoured.

**Decision needed from Adrian before this task starts:** enforce at 80%, or lower to a threshold
that will actually be respected. Either is defensible. Bypassing it is not, and neither is leaving
it red.

**Acceptance (EARS):** WHEN a pull request adds production code whose new-line coverage is below
the configured threshold, THE SYSTEM SHALL fail a required status check, AND no merge shall proceed
without an explicit recorded override.

**Note the interaction:** at 52.4% overall, early phases of this epic will themselves be gated by
this. That is intended -- the hardening work should meet the bar it sets. Expect the first two or
three PRs to feel slow.

---

## 2.2 Pin GitHub Actions to commit SHAs

**Scope:** 97 `PinnedDependenciesID` findings across 17 workflow files.

An action referenced by tag (`@v4`) is mutable -- the tag can be repointed at new code by anyone
who controls the action repository. That is a live supply-chain path into CI, which holds repository
write and secrets. Pinning to a full commit SHA makes the reference immutable.

**Mechanical, but not thoughtless:** pin to the SHA that the current tag resolves to today, and
record the tag in a trailing comment (`uses: actions/checkout@<sha> # v4.2.2`) so future upgrades
are legible. Dependabot can keep pinned SHAs updated if `.github/dependabot.yml` includes the
`github-actions` ecosystem -- check whether it does, and add it if not, or the pins will rot.

**Acceptance (EARS):** WHEN a workflow references a third-party action, THE SYSTEM SHALL reference
it by full commit SHA.

---

## 2.3 Restrict workflow token permissions

**Scope:** 7 `TokenPermissionsID` findings (severity high).

Workflows without an explicit `permissions:` block inherit the repository default, which is
typically far more than a given job needs. Set `permissions: contents: read` at the top of each
workflow and grant additional scopes per job only where required (for example `security-events:
write` for CodeQL upload, `pull-requests: write` for the labeler).

**Acceptance (EARS):** WHEN a workflow runs, THE SYSTEM SHALL grant the minimum token scopes that
workflow requires, declared explicitly.

---

## 2.4 Decide the fate of the stub Documentation Check

`.github/workflows/doc-check.yml` is a no-op. Its real steps are commented out pending
`ANTHROPIC_API_KEY`, and the job echoes a message and exits 0. It reports as a passing check named
"Documentation Check" on every PR.

A permanently green check that verifies nothing is an anti-gate: it manufactures confidence. Either
wire it up or delete the workflow. Deleting is the honest default until someone wants to fund the
key.

---

## 2.5 Consider promoting the remaining rule families to build errors

**Deferred until phase 7 is underway** -- you cannot make a rule build-failing while 253 instances
of it exist.

The pattern to mirror already exists in this repo and works: `Directory.Build.props:21` sets
`TreatWarningsAsErrors=true`, `:158` brings in `BannedApiAnalyzers`, and the repo-root
`BannedSymbols.txt` bans Scriban with a companion runtime test. The Pacific epic extended it to
`DateTime.UtcNow`/`.Now`/`.Today`.

As each rule family in phase 7 reaches zero, add it to the banned/error set in the same commit that
clears it. **Clearing a family without locking it closed is half a job** -- the count returns.

---

## 2.6 Fail the build on a model change with no migration (admitted 2026-08-31)

**Routed here from the system design research** (REQ-REL-06). It is a gate, it is one line, and it
closes a defect class this repo has already shipped.

An entity mapped in both DbContexts needs a migration in **both** migration sets. Forgetting one
produces an office database missing a table, which surfaces as a runtime exception in front of a
user rather than as a build failure. That has happened here before -- see the standing rule that a
dual-context entity needs both migration sets, and that a model change needs an empty-migration proof
because the build and the SQLite suite will both agree with a wrong model.

The framework ships a first-party command for exactly this check. Run it **independently for the
host schema and the tenant schema**, because a single combined check passes when one of the two is
missing.

**Acceptance (EARS):** WHEN the object-relational model contains a change not represented by a
committed migration in the corresponding migration set, THE SYSTEM SHALL fail the build.

**Prove it by poisoning**, per this phase's rule: add a model change with a migration in one schema
only, confirm the build goes red, then add the second migration and confirm green. A gate nobody has
watched fail on the _one-sided_ case is not testing the thing that actually breaks.

---

## Validation loop for this phase

The gates are the deliverable, so the validation is adversarial:

1. Open a scratch PR that violates each gate deliberately (an unpinned action, a tag-referenced
   action, an untested public method).
2. Confirm each intended check goes red, and that the red check is _required_ rather than advisory.
3. Remove the violations, confirm green.
4. Record in the phase notes which checks are required versus advisory -- `main` currently requires
   only `Backend: Build` and `Frontend: Build`; `development` requires four. That asymmetry is
   itself worth fixing here.

---

## TASK 2.1 -- MERGED 2026-09-02. Five gates proven; four settings left unflipped with reasons.

**Landed as `8096966d` (#514).** The dependency-review flip followed separately in #518. Three
throwaway probe PRs -- #513, #515, #517 -- held the deliberate violations and are all CLOSED; their
branches were kept, not deleted.

**Proven red by poisoning (5):** Backend: Format Check, Frontend: Format Check, Lint: YAML workflows,
Lint: Markdown (changed-files-only), Dependency Review.

**Deliberately NOT flipped (4), each for a stated reason:** `commitlint` and `pr-title` require a
malformed commit message or PR title, which two local guardrails prevent this tooling from creating
-- reported as configured-identically-but-not-observed rather than manufactured. `trufflehog-pr` must
never be poisoned, because a real secret committed to prove a scanner works outlives the proof.
`sonarcloud` is pointless to poison as configured -- see the note below.

**Two deletions:** the permanently-green `doc-check.yml` stub, and the duplicate `-warnaserror`
back-end build whose own comment set the condition for its removal (already met).

**A note that will otherwise be misread:** the SonarCloud QUALITY GATE result is reported by the
external `SonarCloud Code Analysis` status. The Actions job `SonarCloud: Analysis` never evaluates it
(`sonar.qualitygate.wait=false`). Two similarly named things. Making the gate blocking therefore means
REQUIRING THE EXTERNAL STATUS in branch protection -- not flipping that job, and not setting
`wait=true`.

### What is DONE and proven

| Change                                                           | State                                             |
| ---------------------------------------------------------------- | ------------------------------------------------- |
| Delete `doc-check.yml` (permanently green stub)                  | done; verified not a required check on any branch |
| Delete the duplicate `-warnaserror` build at `ci.yml:101`        | done, per its own removal condition               |
| Flip **Backend: Format Check** to blocking                       | done, **POISONED RED**                            |
| Flip **Frontend: Format Check** to blocking                      | done, **POISONED RED**                            |
| Flip **Lint: YAML workflows** to blocking                        | done, **POISONED RED**                            |
| Remove `\|\| true` masking the Angular suite in `sonarcloud.yml` | done (job still non-blocking)                     |
| Fix `markdownlint` globs                                         | done -- see the finding below                     |

### THE FINDING: `Lint: Markdown` was linting ZERO files

`markdownlint-cli2-action` needs `globs` NEWLINE-separated. As one space-separated string it matched
nothing: `Linting: 0 file(s) / Summary: 0 error(s)`. Permanently green while checking nothing --
the eighth instance of a check reporting success without running.

**Fixed globs -> `Linting: 431 file(s) / Summary: 5271 error(s)`.** So it was NOT flipped to
blocking: 5,271 pre-existing violations would turn every PR red, which this task is not scoped to
fix. It now reports honestly and still does not gate. Flipping it needs its own item.

### What is NOT done -- 6 settings deliberately REVERTED, not left half-flipped

`codeql-pr`, `commitlint`, `dependency-review`, `pr-title`, `trufflehog-pr`, and the `sonarcloud`
job were flipped and then **reverted**, because a gate made blocking and never watched fail is
exactly what this phase exists to remove. They are back to `continue-on-error: true`.

Two of them CANNOT be poisoned from a Claude session: `Commitlint` and `PR Title` need a
deliberately malformed commit message / PR title, and two local guardrails prevent creating one
(`validate-commit-message.sh`, and `block-dangerous-commands.sh` refusing `--no-verify`). Producing
that proof needs one command run outside Claude. The other four (`CodeQL`, `SonarCloud`,
`Dependency Review`, `TruffleHog`) need a real vulnerability, gate breach, denied-licence dependency
and verified secret respectively -- slow, and two involve committing something deliberately nasty.
**Open question for Adrian: is observed-red required for those four, or is the configuration
argument enough given three were proven?**

### Two findings for LATER tasks, not touched here

- **LIVE BLOCKER ON `production` -- phase 2 item, do NOT act on it, the fix is Adrian's choice.**
  `production` branch protection requires seven checks, one of them named **"Secret Detection"**.

  **Corrected mechanism (an earlier note in this file said "no check by that name exists" -- that
  was WRONG).** The check DOES exist: `.github/workflows/security.yml:47` is literally
  `name: "Secret Detection"`. The fault is that `security.yml` triggers on `schedule`
  (weekly, Mondays 06:00) and `workflow_dispatch` ONLY -- it has **zero `pull_request` triggers**.
  So the check exists, produces exactly that name, and can never report on a pull request.

  **Consequence: no PR to `production` can ever satisfy its required checks, so merges to
  `production` are blocked indefinitely.** `staging` does not require it and is unaffected. This
  plausibly explains why `production` has sat untouched since May; the standing decision to defer
  the staging/production reset until this epic lands means nobody has hit it recently, but it will
  bite the moment anyone tries.

  The fix is a choice between adding a `pull_request` trigger to that workflow and removing the
  requirement from branch protection. That is Adrian's call, not ours.

  Why the precision matters: "no such check exists" sends someone hunting for a missing workflow;
  "the check exists but never runs on PRs" sends them to add a trigger. Same symptom, different fix.

- A **twelfth** masking construct at `ci.yml:121`, `check-links.py || true`, self-documented as
  deliberate for pre-existing broken links. Same class as the eleven; left alone.

### Task 2.2 preview -- the expectation was backwards

Removing the front-end coverage exclusion is expected to move the number **UP, not down**. Angular
is at 67.44% of lines against a back-end-only 52.4%; a rough line-weighted blend is ~54%. Treat that
as a direction only -- it uses karma's line counting, not Sonar's. 2.2 must produce the real number.

---

## REUSABLE PATTERN: land a gate BEFORE its backlog is cleared (2026-09-02)

Adrian's call, generalised from task 2.1's markdown check. Several later phases have the same shape --
a large pre-existing backlog and a gate you want in place now -- so this is written once here.

**The problem.** A check with a big backlog has only bad options if you think in whole-repo terms:

| Option                        | Failure mode                                                         |
| ----------------------------- | -------------------------------------------------------------------- |
| Leave it advisory             | it never gates; the backlog grows                                    |
| Make it blocking now          | permanently RED; **teaches everyone to ignore red**                  |
| Wait until the backlog clears | the gate arrives months late, after the habit it prevents has set in |

The middle option is the trap, and it is not obviously worse than the first -- **a permanently red
check and a permanently green one are equally uninformative.** Task 2.1 produced one of each from the
same check within an hour: `Lint: Markdown` was green while linting zero files, and fixing that made
it red on 5,271 pre-existing violations.

**The pattern: scope the gate to what the submission CHANGES.**

Compute the changed files against the merge base and check only those. New and edited work must meet
the bar; the backlog is untouched and waits for the phase that owns backlogs.

```bash
base="origin/${{ github.base_ref }}"
files=$(git diff --name-only --diff-filter=ACMR "$base...HEAD" -- '<pattern>')
[ -z "$files" ] && { echo "Nothing to check."; exit 0; }
echo "$files" | xargs <the checker>
```

Requires `fetch-depth: 0` on checkout. The empty case must exit 0, or every submission that touches
nothing of that type fails.

**Why it is strictly better than the three options above:** the gate is honest (it reports real
violations), green (so red means something), and BLOCKING from day one (so it actually constrains
new work) -- and the backlog stops growing immediately even though nobody has touched it.

**Where this likely applies next:** the rule families in phase 7 (253 instances of one rule cannot be
made build-failing repo-wide, but they can be for changed files), the coverage threshold once 2.2
reports the true number (new-code coverage is already this pattern -- Sonar's `new_coverage` metric
is changed-lines-only), and the action pinning in 2.2/2.3 if the count makes a single sweep unsafe.

**The limit, stated so nobody over-applies it:** it does not work for whole-artifact properties. A
gate on "the build succeeds" or "no secret exists anywhere in history" cannot be scoped to changed
files, because the property is not per-file.

---

## TASK 2.2 RESULT: the true coverage number is 55.4%, and it went UP (2026-09-02)

Measured, not estimated. PR #516 removed `angular/src/**/*.ts` and `angular/src/**/*.html` from
`sonar.coverage.exclusions` and the analyser was allowed to report. Generated proxy code and
`*.module.ts` remain excluded.

| Metric            | Before (back end only) | After (honest) | Delta    |
| ----------------- | ---------------------- | -------------- | -------- |
| `coverage`        | **52.4%**              | **55.4%**      | **+3.0** |
| `lines_to_cover`  | 17,750                 | 20,065         | +2,315   |
| `uncovered_lines` | 8,450                  | 8,980          | +530     |

**IT ROSE. The original expectation was that it would fall, "probably substantially".** That
expectation was reasoned from file counts -- 667 specs across ~493 files sounded thin -- rather than
from the coverage report, which was sitting in the build output the whole time. Inferring where
observation was available.

**The Angular slice, derived from the deltas:** 2,315 lines entered the denominator and 530 of them
are uncovered, so Angular is at **77.1% line coverage as the analyser counts it** -- materially
better than the back end's 52.4%, which is why the blend rose.

**Two precisions so nobody re-derives these wrongly:**

- Sonar's `coverage` metric is a BLEND of line and condition coverage, not line coverage alone.
  Pure line coverage from the same numbers is 55.25%; the reported 55.4% includes conditions. Both
  are correct measures of different things.
- The analyser puts Angular at 77.1% while karma's own summary reported 67.44%. Not a contradiction:
  Sonar excludes files karma counts (the generated proxy, `*.module.ts`) and counts coverable lines
  differently. **Use the analyser's number for anything gate-related**, since the gate is the
  analyser's.

**Consequence for the threshold work:** the real baseline to set a threshold against is 55.4%, not
52.4%, and the front end is the stronger half rather than the weaker one. Any threshold argument that
assumed "Angular is untested" is starting from a false premise.

`new_lines_to_cover` was 0 on that PR -- it changes only a workflow file -- so this run says nothing
about the new-code gate. That gate is separate and is reported by the external
`SonarCloud Code Analysis` status, not by the `SonarCloud: Analysis` Actions job.

---

## 2.7 Audit `sonar.coverage.exclusions` (added 2026-09-02)

**Own item, not part of the threshold task.** Raised because the exclusion list has never been
audited and 2.2 proved it was wrong in a way that mattered.

Until 2026-09-02 it excluded `angular/src/**/*.ts` and `angular/src/**/*.html` -- the entire front
end -- which turned out to be the BETTER-tested half at 77.1%. The reported 52.4% was a measurement
hiding its own subject. That exclusion is gone; the rest of the list has never been checked:

```
**/Program.cs,**/*Module.cs,**/*DbContext*.cs,**/Migrations/**,**/TenantMigrations/**,
angular/src/app/proxy/**,**/*.module.ts
```

**Why it matters NOW rather than later.** With `new_coverage` moving to 90%, every file that is
wrongly INCLUDED becomes a source of false failure, and every file wrongly EXCLUDED inflates the
number that the ratchet is set against. At 80% there was slack for both errors; at 90% there is not.

**Ask of each entry:** is this genuinely untestable, or merely untested? `Program.cs` and generated
proxy code are defensible. `*Module.cs` covers ABP module classes that contain real configuration
logic -- some of which this epic has already changed and tested. `*DbContext*.cs` is a broad wildcard
that may catch more than intended.

**Acceptance (EARS):** WHEN a path is excluded from coverage, THE SYSTEM SHALL exclude it because the
code is not meaningfully testable, and each entry shall carry a recorded reason.
