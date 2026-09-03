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

## RESUME HERE (2026-09-03)

Read this section first. It is maintained so that a restart costs minutes rather than an hour.

### In flight

| Item     | What                       | State                                                                |
| -------- | -------------------------- | -------------------------------------------------------------------- |
| **2.10** | Separate CI coverage check | PARTIAL -- `feat/ci-coverage-floors` @ `d2d9a0d9`, pushed, **no PR** |

**Epic head `fcc529dc`. 0 behind `main`, 42 ahead, `main` a genuine ancestor.**

The catch-up merged as `d2dc0233` (#524); the row that used to sit here said "the PR you are
reading" and described nothing once that PR landed. 2.12 merged as `855a5f7c` (#529) and 2.3 as
`fcc529dc` (#530).

> **This block was wrong on three of twelve statuses until 2026-09-03**, reporting 2.3 as
> "researched", 2.10 as "not started" and 2.12 as "unblocked" after all three had moved. It was found
> by the incoming implementer, whose first action on a stale instruction was to push a closed PR.
> **A block whose stated purpose is that a restart costs minutes rather than an hour, reporting
> current without being current, is the same shape as a check reporting success without having run.**
> Correcting it is the supervisor's job, by Adrian's ruling, because the implementer holds the shared
> index and a 77KB file is not worth racing over.

### Next, in order

1. **2.10** -- the separate CI coverage check. **Partially built and pushed**, not merged. The gate
   script and `.coverage-exclusions` are done and independently re-proved (69.46%, 1713/2466 over 82
   files; PASS at floor 69.0, FAIL exit 1 at 70.0). Remaining: the `ci.yml` wiring, artefact uploads,
   the changed-lines check, both gitignores and the README badge. Its section below holds the trap
   that will make a naive version pass while measuring nothing.
2. **2.6** -- fail the build on a model change with no migration. Mechanism already proven per
   context; only the ONE-SIDED poison remains. Not batched with 2.10 -- see 2.10's validation loop.
3. **2.2** -- pin actions to commit SHAs. Not started; re-derive its counts first.

### Blocked on a decision, not on work

- **2.11** (require the checks in branch protection) is deliberately last, and now has a hard
  prerequisite as well as an ordering. Do not start it until the gates it would require are
  individually proven -- turning it on early converts a false alarm into a hard block on all work,
  and there is no second engineer to unblock it. The prerequisite: five required checks on
  `production` are conditional on a classifier job that is not itself required, so they skip and
  report Success. See the `production` section.
- **Observed-red versus configuration argument** for the four gates that cannot be cheaply poisoned.
  Asked in 2.8, still open, and 2.11 and the `production` fix both depend on the answer.

### The one lens that has paid off more than any other

A check that reports success without having run. **Eighteen** instances are catalogued at the end of
this file, nine found in this epic's own work -- and two of those CORRECTED earlier entries rather
than adding to them. **Assume the class exists in whatever you are about to trust until you
have disproven it**, including in your own prior conclusions.

---

## Numbering map -- one number, one thing

Until 2026-09-03 this file used `2.1` for two different pieces of work and `2.2` for two others,
which made the phase unreadable from the outside. Numbers are now unique. Old references resolve
through this table.

| Old reference in earlier notes and commits  | Now | What it is                                 |
| ------------------------------------------- | --- | ------------------------------------------ |
| `TASK 2.1` (merged 2026-09-02, #514/#518)   | 2.8 | Make the existing gates capable of failing |
| `TASK 2.2 RESULT` (merged 2026-09-02, #516) | 2.9 | Measure true coverage                      |
| plan item `2.1` (SonarCloud ratchet)        | 2.1 | SUPERSEDED -- cancelled, see its section   |
| plan item `2.2` (pin actions to SHAs)       | 2.2 | unchanged                                  |

Commit messages and PR titles from 2026-09-02 say "Phase 2.1" where this file now says 2.8. That is
expected; the commits are immutable and this table is the bridge.

## Item status at a glance

| Item     | What                                               | Status                                 |
| -------- | -------------------------------------------------- | -------------------------------------- |
| **2.1**  | Make the SonarCloud new-code coverage gate real    | **SUPERSEDED** -- cancelled 2026-09-02 |
| **2.2**  | Pin GitHub Actions to commit SHAs                  | NOT STARTED                            |
| **2.3**  | Restrict workflow token permissions                | **MERGED** (`fcc529dc`, #530)          |
| **2.4**  | Decide the fate of the stub Documentation Check    | **DONE** via #514                      |
| **2.5**  | Promote cleared rule families to build errors      | DEFERRED to phase 7 by design          |
| **2.6**  | Fail the build on a model change with no migration | NOT STARTED                            |
| **2.7**  | Audit `sonar.coverage.exclusions`                  | **MERGED** (`2d3f656b`, #519)          |
| **2.8**  | Make the five existing gates capable of failing    | **MERGED** (#514, #518)                |
| **2.9**  | Measure true coverage                              | **MERGED** (#516)                      |
| **2.10** | Build a separate CI coverage check                 | **IN PROGRESS** -- partial, pushed     |
| **2.11** | Require the checks in branch protection            | **DELIBERATELY LAST**                  |
| **2.12** | Make `Lint: Markdown` repo-wide blocking           | **MERGED** (`855a5f7c`, #529)          |

---

## A STANDING RULE FOR THIS RECORD: no bare percentage, no bare count

Adopted 2026-09-03 after a figure in this file was read as the repository's coverage when it was a
projection of a pull request that had not merged.

- **Every coverage figure carries its scope** -- `main`, or `PR #N`. A bare percentage is a defect
  in this record.
- **Every count carries the command AND the ref it was measured on.** A count without its tree is
  the same defect as a percentage without its scope: a stale worktree produces a correct-looking
  number about the wrong thing. This happened on 2026-09-03 and is instance 13 in the catalogue.

Why it matters more here than it sounds: SonarCloud analyses **only `main`** in this project. There
is no branch analysis of `feat/production-hardening`, so any figure quoted from a PR analysis
describes a state that does not exist anywhere yet.

```bash
curl -sS "https://sonarcloud.io/api/project_branches/list?project=gesco-healthcare-support_hcs-patient-portal"
# -> exactly one branch: main, isMain=true
```

### Three more method rules, each from a real error on 2026-09-03

**`git branch -r --contains` is a point-in-time check.** It cannot see a branch cut afterwards from a
pre-merge base. #519 was squashed after a clean `--contains` result, and #523 -- which did not yet
exist when the check ran -- needed a rebase because of it. **While a peer session is working in the
shared worktree, a clean `--contains` is not sufficient grounds to squash.** Coordinate the merge or
expect the rebase.

**Measure a conflict set; do not infer one.** Both sessions predicted the catch-up's conflicts from
change lists -- one said four files, one said nine. **The measured answer was two.**
Both-sides-changed is an upper bound, not a conflict set: git merges non-overlapping hunks fine.

```bash
git merge-tree --write-tree --name-only <ours> <theirs>   # touches no working tree
```

**A merge's acceptance is that the merged result passes its checks**, not that git reported no
conflicts. See catalogue instance 17, where a clean auto-merge duplicated 96 lines of this record.

---

## 2.1 Make the SonarCloud new-code coverage gate real -- SUPERSEDED

**CANCELLED BY ADRIAN ON 2026-09-02. Do not touch SonarCloud's quality gate or its 80% threshold.**
Its replacement is 2.10, a separate CI coverage check. This section is kept rather than deleted
because the reasoning is what stops the idea being re-proposed.

Adrian's ruling, in his words:

> "the 80% requirement for sonarcloud cannot be changed and should not be. We should have a
> separate CI to check coverage and passing and we can add the Coverage thing in the repo readme."

**What the original design was.** Lower the `new_coverage >= 80%` condition to a threshold that
would be honoured rather than admin-overridden on every PR -- the override was confirmed on PR #359
(`actual=0.0 op=LT threshold=80`) and again on #414 (77.8%).

**Why it was wrong independently of the ruling.** Three concrete faults, and they are the reusable
part:

1. **It needed an admin credential nobody had.** The threshold lives in SonarCloud's quality-gate
   settings, not in this repository.
2. **It lived outside version control.** A gate you cannot diff is a gate nobody can review, and its
   history is whatever the service remembers.
3. **It could not be reviewed in a pull request.** Every other gate in this phase arrives as a diff
   with a poison test attached. This one would have arrived as an assertion.

The premise that a routinely-bypassed gate is worse than no gate still holds, and it is why 2.10
blocks from day one rather than warning first. What changed is where the gate lives: in
`ci.yml`, in version control, reviewable, next to the tests it measures.

**The original acceptance criterion is retained by 2.10**, restated there against the new mechanism.

---

## 2.2 Pin GitHub Actions to commit SHAs

**Scope:** 97 `PinnedDependenciesID` findings. Re-verified 2026-09-03:

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/code-scanning/alerts?per_page=100&state=open" \
  --paginate --jq '.[].rule.id' | sort | uniq -c | sort -rn
# -> 97 PinnedDependenciesID
```

**The file count is 17 or 16 depending on which tree you measure, and both are right.** Scorecard
scans `main`, which carries 17 workflow files. The epic branch carries 16, because #514 deleted
`doc-check.yml` (see 2.4). Neither number is an error; a count without its tree is meaningless.

```bash
git ls-tree -r --name-only origin/main -- .github/workflows/ | wc -l              # 17
git ls-tree -r --name-only origin/feat/production-hardening -- .github/workflows/ | wc -l  # 16
```

**Re-derive the 97 before acting on it.** It was measured against `main`'s 17 files, so it includes
findings on a file this branch has deleted. The number will drop on its own when the epic lands.

An action referenced by tag (`@v4`) is mutable -- the tag can be repointed at new code by anyone
who controls the action repository. That is a live supply-chain path into CI, which holds repository
write and secrets. Pinning to a full commit SHA makes the reference immutable.

**Mechanical, but not thoughtless:** pin to the SHA that the current tag resolves to today, and
record the tag in a trailing comment (`uses: actions/checkout@<sha> # v4.2.2`) so future upgrades
are legible. Dependabot can keep pinned SHAs updated if `.github/dependabot.yml` includes the
`github-actions` ecosystem -- check whether it does, and add it if not, or the pins will rot.

### ROUTED HERE FROM #529: `githubactions:S6505` at `lint-meta.yml:77`

SonarCloud went red on #529 with `new_security_rating 3 > 1`, from a single MAJOR vulnerability:
**`npx` installs packages on demand and runs their lifecycle scripts.**

**It is PRE-EXISTING, not introduced.** The identical line sat at line 78 before #529 and was only
moved, which is the basis #529 was merged on.

**It belongs to 2.2 because it is the same supply-chain class as an unpinned action**, and it needs
stating explicitly that **a pinned version tag does not remove it** -- `npx` still resolves and
executes from the registry at run time. Fix it here rather than leaving it sitting in Sonar, where it
will be re-triaged by whoever meets it next.

**Acceptance (EARS):** WHEN a workflow references a third-party action, THE SYSTEM SHALL reference
it by full commit SHA.

---

## 2.3 Restrict workflow token permissions -- MERGED 2026-09-03

**Scope:** 7 `TokenPermissionsID` findings (severity high). **Landed as `fcc529dc` (#530).**

### What proved it, and what did NOT

**Six files changed; only three could be exercised by the pull request.** `codeql-pr.yml` and
`pr-title.yml` reported green on #530 itself. `security.yml` was proven by a `workflow_dispatch` run
on the PR branch, **run `33794718492`, all four jobs green by name** -- Secret Detection, CodeQL
Analysis, npm Vulnerability Audit, .NET Vulnerability Audit. That run was still in flight when the
previous session handed over, and confirming it was the last open piece of this item.

**`scorecard.yml`, `deploy-dev.yml` and `release.yml` were NOT exercised**, because they trigger only
on `schedule`/`dispatch`, push to `development`, and push to `production` respectively.

**Merged anyway, on a mechanism rather than on optimism.** A job-level `permissions:` block REPLACES
the top-level one for that job, and every job in `deploy-dev.yml` and `release.yml` declares its own.
So the new top-level `contents: read` is a default that no job in either file inherits -- inert at
runtime, and incapable of producing the silent 403 this item's own research warns about.
`release.yml` correctly retains `contents: write` at job level, which is the "narrow, not remove"
prescription applied rather than quoted.

**`scorecard.yml` carries `workflow_dispatch` and so could still be turned into an observation.**
Recorded as an accepted residual, not as done.

### Eight unflagged `pull-requests: write` grants

Recorded in #530's body and nowhere else until now. **Scorecard does not flag that scope** and
several of the eight genuinely need it, so 2.3 correctly did not touch them. Noted here so the next
reader does not mistake the silence for absence.

**ONE ITEM, TWO PARTS -- and the second part was missed on first reading.** The 7 findings are two
different defects with two different fixes. An earlier version of this section described only the
first, which would have shipped half the item and left the other half looking done.

```bash
gh api "repos/gesco-healthcare-support/hcs-patient-portal/code-scanning/alerts?per_page=100&state=open" \
  --paginate --jq '.[] | select(.rule.id|test("TokenPermissions")) |
    "\(.most_recent_instance.location.path):\(.most_recent_instance.location.start_line)"'
```

### Part A -- no top-level `permissions:` block at all (4 findings)

`security.yml`, `scorecard.yml`, `deploy-dev.yml`, and `doc-check.yml`. The last of those is already
deleted on this branch by #514, so **Part A is three files here and will close the fourth finding on
its own** when the epic lands.

Verified on the epic branch:

```bash
cd .github/workflows && for f in *.yml; do \
  if ! awk '/^permissions:/{found=1} /^jobs:/{exit} END{exit !found}' "$f"; then echo "MISSING: $f"; fi; done
# -> deploy-dev.yml, scorecard.yml, security.yml
```

**All three already declare permissions at job level, covering every job in the file.** So the
top-level block is a least-privilege default, not a functional change:

| File             | Jobs | Jobs with their own `permissions:` |
| ---------------- | ---- | ---------------------------------- |
| `deploy-dev.yml` | 2    | 2                                  |
| `scorecard.yml`  | 1    | 1                                  |
| `security.yml`   | 4    | 4                                  |

**Do not trust that table as the check.** Two independent greps of this produced two different job
counts on 2026-09-03, because naive job-matching patterns also catch top-level keys like `on:` and
`env:`. **The check is the per-file diff, read before it is written**, not a count. A wrong
`permissions:` block breaks a workflow silently via a 403 that reads like an unrelated failure.

### Part B -- a top-level block exists but grants `write` (3 findings)

| File            | Line | Finding                                     |
| --------------- | ---- | ------------------------------------------- |
| `release.yml`   | 8    | topLevel `contents` permission set to write |
| `pr-title.yml`  | 13   | topLevel `statuses` permission set to write |
| `codeql-pr.yml` | 19   | topLevel `security-events` set to write     |

**"Narrow", not "remove", and for at least one of them not even that.** `release.yml`'s
`contents: write` is almost certainly _required_ to publish a release. Narrowing here means moving
the scope down to the job that needs it so the default for other jobs is read-only -- not deleting
it.

**Treat all three as per-file judgements.** If any one of them cannot be narrowed without breaking
the workflow, **that is a finding for this record, not a failure.** Write down which one and why.

### Why this stays one item

Same Scorecard rule id, same acceptance criterion, one validation command. Splitting it creates two
changes against one rule where the second gets forgotten -- which is precisely how Part B came to be
missing in the first place. The two parts touch disjoint files, so it can still be split later if
Part B turns out to need per-file argument.

**Acceptance (EARS):** WHEN a workflow runs, THE SYSTEM SHALL grant the minimum token scopes that
workflow requires, declared explicitly, AND no workflow shall grant a `write` scope at top level
that only one of its jobs requires.

**Validation:** the Scorecard `TokenPermissionsID` count falls from 7 to 0 on the next Scorecard run
against `main`, and every touched workflow completes a real run without a 403.

---

## 2.4 Decide the fate of the stub Documentation Check -- DONE via #514

**Resolved by deletion.** This section read as an open item until 2026-09-03; it was in fact closed
on 2026-09-02 as part of 2.8, and only the cross-reference in 2.8's own table recorded it. Corrected
here so the item list and the work agree.

```bash
git show --stat 8096966d -- .github/workflows/   # -> doc-check.yml | 24 ------
```

**What it was.** `.github/workflows/doc-check.yml` was a no-op: its real steps were commented out
pending `ANTHROPIC_API_KEY`, and the job echoed a message and exited 0. It reported as a passing
check named "Documentation Check" on every PR. A permanently green check that verifies nothing is an
anti-gate -- it manufactures confidence. Deleting was the honest default until someone wants to fund
the key, and it was verified not to be a required check on any branch before removal.

### STILL LIVE ON `main` -- this is not fully closed until the epic lands

The deletion exists **only on the epic branch.** `main` still carries the file, so the
permanently-green "Documentation Check" still reports on every pull request targeting `main`.

```bash
git ls-tree origin/main -- .github/workflows/doc-check.yml   # -> one line: still present
git ls-tree HEAD -- .github/workflows/doc-check.yml          # -> empty: deleted here
```

Nothing to do about it -- it closes when the epic merges. Recorded because "2.4 DONE" read on its
own would tell a successor the anti-gate is gone from the repository, and it is not.

**A verification note that belongs with this entry**, because the obvious way to check the above is
broken in this environment: `git cat-file -e origin/main:.github/workflows/doc-check.yml` does
**not** work in Git Bash. MSYS rewrites the argument when both sides of the colon contain a slash
(`origin\main;.github\workflows\doc-check.yml`) and it exits 128. Wrapped in `2>/dev/null && echo
PRESENT || echo ABSENT` it reports a confident, wrong ABSENT. Use `git ls-tree`, which takes no
colon. This is instance 10 in the catalogue at the end of this file.

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

## 2.8 -- MERGED 2026-09-02. Five gates proven; four settings left unflipped with reasons

**This was called "TASK 2.1" until 2026-09-03.** Renumbered because plan item 2.1 is a different
thing. Commits and PR titles from 2026-09-02 still say 2.1; see the numbering map at the top.

**Landed as `8096966d` (#514).** The dependency-review flip followed separately as `f41b6954`
(#518). Three throwaway probe PRs -- #513, #515, #517 -- held the deliberate violations and are all
CLOSED; their branches were kept, not deleted.

### THE GATE TALLY -- how many checks can actually fail, before and after

Measured 2026-09-03. `continue-on-error: true` is the setting that makes a check incapable of
failing the run, so counting it counts anti-gates. The `grep -vE` drops comment lines that merely
mention the setting; without it the answer is 12 and 6, which is the difference between two earlier
conflicting counts.

```bash
# before 2.8
git grep -h "continue-on-error" 8096966d^ -- .github/workflows/ \
  | grep -vE "^\s*#" | grep -c "continue-on-error"                      # -> 11

# after 2.8 and #518, on the epic branch
git grep -h "continue-on-error" origin/feat/production-hardening -- .github/workflows/ \
  | grep -vE "^\s*#" | grep -c "continue-on-error"                      # -> 5
```

**11 anti-gate settings before, 5 after.** The 5 that remain are exactly the ones this task
deliberately reverted, minus `dependency-review` which #518 then flipped for real:

| File                | Line | Why it is still there                                     |
| ------------------- | ---- | --------------------------------------------------------- |
| `codeql-pr.yml`     | 26   | needs a real vulnerability to poison                      |
| `commitlint.yml`    | 30   | needs a malformed commit message; local hooks prevent one |
| `pr-title.yml`      | 20   | needs a malformed PR title; same                          |
| `sonarcloud.yml`    | 32   | pointless to poison as configured -- see the note below   |
| `trufflehog-pr.yml` | 29   | must never be poisoned -- **but see the production note** |

**Measure this against a ref, never against a worktree.** On 2026-09-03 the same count taken in the
shared worktree returned 6, because that worktree was one commit behind the epic branch and
predated #518. The command was right and the answer was wrong. Instance 11 in the catalogue.

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
exactly what this phase exists to remove. They went back to `continue-on-error: true`.

**Five of those six are still reverted.** `dependency-review` was flipped for real in #518 once it
had been poisoned -- adding `pm2@7.0.4` (SPDX `AGPL-3.0`) produced
`##[error]Dependency review detected incompatible licenses.` So read this list as six at the time of
writing and five today; the tally above is the current figure.

**#518 also recorded a KNOWN GAP that phase 6 needs and would not otherwise find.** The deny-list
matches SPDX expressions only, so a package that declares its licence as a URL passes: `itext7` on
NuGet declares `https://www.gnu.org/licenses/agpl.html` and the check **PASSED it while listing it
in the diff.** That is an AGPL dependency in a proprietary product going through a licence gate
undetected. The mechanism is documented in the workflow comment, which is the right place for it,
but the consequence belongs where phase 6 will look -- so it is repeated here and routed to
[06-dependencies.md](06-dependencies.md).

Two of them CANNOT be poisoned from a Claude session: `Commitlint` and `PR Title` need a
deliberately malformed commit message / PR title, and two local guardrails prevent creating one
(`validate-commit-message.sh`, and `block-dangerous-commands.sh` refusing `--no-verify`). Producing
that proof needs one command run outside Claude. The other four (`CodeQL`, `SonarCloud`,
`Dependency Review`, `TruffleHog`) need a real vulnerability, gate breach, denied-licence dependency
and verified secret respectively -- slow, and two involve committing something deliberately nasty.
**Open question for Adrian: is observed-red required for those four, or is the configuration
argument enough given three were proven?**

### One finding for a LATER task, not touched here

A **twelfth** masking construct at `ci.yml:121`, `check-links.py || true`, self-documented as
deliberate for pre-existing broken links. Same class as the eleven; left alone.

### 2.9 preview -- the expectation was backwards

Removing the front-end coverage exclusion is expected to move the number **UP, not down**. Angular
is at 67.44% of lines against a back-end-only 52.4% (`main`); a rough line-weighted blend is ~54%.
Treat that as a direction only -- it uses karma's line counting, not Sonar's. 2.9 must produce the
real number.

---

## THE `production` BRANCH BLOCKER -- FIXED 2026-09-03

**Previous status in this file was "do NOT act on it, the fix is Adrian's choice." That is now out
of date: the decision was taken and Adrian applied it the same day.** `production` is mergeable
again. Two live consequences came out of unblocking it -- see the end of this section.

### The blocker

`production` branch protection requires seven checks, one of them named **"Secret Detection"**.

```bash
gh api repos/gesco-healthcare-support/hcs-patient-portal/branches/production/protection \
  --jq '.required_status_checks.contexts'
```

**Corrected mechanism (an earlier note in this file said "no check by that name exists" -- that was
WRONG).** The check DOES exist: `.github/workflows/security.yml:47` is literally
`name: "Secret Detection"`. The fault is that `security.yml` triggers on `schedule` (weekly, Mondays
06:00) and `workflow_dispatch` ONLY -- **zero `pull_request` triggers.** So the check exists,
produces exactly that name, and can never report on a pull request.

**Consequence: no PR to `production` could ever satisfy its required checks.** `staging` does not
require it and is unaffected. It has not bitten only because nothing has needed to go to production.

Why the precision matters: "no such check exists" sends someone hunting for a missing workflow; "the
check exists but never runs on PRs" sends them to add a trigger. Same symptom, different fix.

### The resolution: the protection was never missing, it named the wrong scan

There are **two** secret scans in this repository, and only one of them runs on pull requests:

| Workflow            | Check name               | Triggers                      |
| ------------------- | ------------------------ | ----------------------------- |
| `security.yml`      | `Secret Detection`       | `schedule` weekly, `dispatch` |
| `trufflehog-pr.yml` | `TruffleHog: PR commits` | **`pull_request`**            |

**Adrian's decision: point the requirement at the check that actually runs.** No workflow change.
The protection is preserved and arguably improved, because TruffleHog scans each PR's own diff
(`base`/`head` pinned to the PR's SHAs, `--only-verified`) rather than sweeping the repository
weekly. The weekly sweep stays as a backstop.

The branch-protection edit is a repository setting rather than a change to this repo, so Adrian
applied it directly rather than through a pull request.

### Applied, and verified end to end

`production` required checks after the change:

```text
Backend: Build, Frontend: Build, Backend: Test, Frontend: Lint,
Frontend: Test, Dependency Review, TruffleHog: PR commits
strict=true, approvals=2, enforce_admins=false, force-push and deletion disabled
```

`Secret Detection` is gone; nothing else in the protection was touched. **The verification went past
"the API returned 200":** all three workflows behind those seven checks list `production` in
`on: pull_request: branches:` -- `ci.yml`, `dependency-review.yml`, `trufflehog-pr.yml` -- so all
seven can actually report on a PR into `production`, which is the thing that was false before.
`trufflehog-pr.yml` was also checked for the two things that would have made it vacuous: **no
`paths:` filter and no job-level `if:`**, so that job cannot skip.

### A CORRECTION WORTH READING: job-level `continue-on-error` does NOT mask the check

**A precondition was nearly written into this record, and it would have been wrong.**
`trufflehog-pr.yml:29` does carry job-level `continue-on-error: true`, and the inference drawn was
that requiring the check would therefore produce a required gate incapable of failing. **That
inference is false, and this repository disproves it.**

Empirical proof -- PR #384, run `33538330456`, at a commit where `commitlint.yml:30` carried
job-level `continue-on-error: true`:

```text
run.conclusion                = success
job "Commitlint: PR commits"  = failure
check run reported to the PR  = FAILURE
```

**Branch protection evaluates the check context, not the run conclusion.** The runner sets the job
conclusion at completion and `continue-on-error` cannot override it, so the job's check run is still
`failure`. GitHub's documentation states this and warns specifically against using job-level
`continue-on-error` while expecting a required status check to pass.

**So job-level `continue-on-error` masks the workflow RUN conclusion only.** Requiring
`TruffleHog: PR commits` genuinely blocks. Line 29 is still worth removing -- it makes the Actions
tab show green while the check is red -- but that is a clarity fix and **its own small item**,
deliberately not bundled into this decision so that the open question at the end of 2.8 gets a clean
answer instead of being settled as a side effect of something unrelated.

### The two live consequences of unblocking `production`

**1. All five CI-based required checks are conditional on a job that is not itself required.**

```text
ci.yml:
  changes         name "Meta: Changed paths"        <- NOT a required check
  backend-build   needs: changes   if: backend=='true' || shared=='true'
  backend-test    needs: [changes, backend-build]   same condition
  frontend-build  needs: changes   if: frontend=='true' || shared=='true'
  frontend-lint   needs: changes   same condition
  frontend-test   needs: [changes, frontend-build]  same condition
```

Combined with instance 12 in the catalogue -- **a skipped job reports Success and does not block even
when required** -- if `Meta: Changed paths` fails or misclassifies, all five required checks skip,
all five report Success, and a pull request merges into `production` having run none of them. A
single point of failure whose failure mode is everything turning green, gating the most sensitive
branch, and the classifier's own failure blocks nothing because it is not itself required.

**Two honest qualifications, both of which belong here.**

- **This pre-dates the epic and was not created by it.** Those five were already required on
  `production`. It was moot while `production` was unmergeable; unblocking it is what made it live.
- **The skip-on-failed-`needs` path is NOT proven.** It follows from two documented facts -- a
  skipped job reports Success even when required, and a job whose `needs` failed is skipped -- so the
  conclusion follows, but it is an inference rather than an observation. **Proving it is a poison
  test worth running**, in the phase whose rule is that a gate nobody has watched fail is not a gate.

**This lands on 2.11**, which now has a prerequisite as well as an ordering: requiring a conditional
check buys nothing on the changes it skips. Either the classifier becomes required too, or the
conditions move inside always-running jobs that fail when their inputs are absent -- **the same shape
2.10 must be built in. Same defect, same fix, two places.**

**2. Two required checks have a null app binding.** `Backend: Build` and `Backend: Test` carry
`app_id: null` in the protection payload while the other five are bound to GitHub Actions
(`app_id: 15368`). A null binding means any integration posting a status of that name satisfies the
check. Almost certainly an artefact of how they were originally added, low severity and pre-existing
-- but a genuine least-privilege inconsistency on the `production` branch. Recorded, not actioned.

---

## REUSABLE PATTERN: land a gate BEFORE its backlog is cleared (2026-09-02)

Adrian's call, generalised from 2.8's markdown check. Several later phases have the same shape --
a large pre-existing backlog and a gate you want in place now -- so this is written once here.

**The problem.** A check with a big backlog has only bad options if you think in whole-repo terms:

| Option                        | Failure mode                                                         |
| ----------------------------- | -------------------------------------------------------------------- |
| Leave it advisory             | it never gates; the backlog grows                                    |
| Make it blocking now          | permanently RED; **teaches everyone to ignore red**                  |
| Wait until the backlog clears | the gate arrives months late, after the habit it prevents has set in |

The middle option is the trap, and it is not obviously worse than the first -- **a permanently red
check and a permanently green one are equally uninformative.** 2.8 produced one of each from the
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
made build-failing repo-wide, but they can be for changed files), the **changed-lines floor in 2.10**
(new-code coverage is already this pattern -- Sonar's `new_coverage` metric is changed-lines-only),
and the action pinning in 2.2 / 2.3 if the count makes a single sweep unsafe.

**The limit, stated so nobody over-applies it:** it does not work for whole-artifact properties. A
gate on "the build succeeds" or "no secret exists anywhere in history" cannot be scoped to changed
files, because the property is not per-file.

---

## 2.9 RESULT: the true coverage number is 55.4% on PR #516, and it went UP (2026-09-02)

**This was called "TASK 2.2 RESULT" until 2026-09-03.** Renumbered; plan item 2.2 is a different
thing.

Measured, not estimated. PR #516 removed `angular/src/**/*.ts` and `angular/src/**/*.html` from
`sonar.coverage.exclusions` and the analyser was allowed to report. Generated proxy code and
`*.module.ts` remain excluded.

**Read the scope column before quoting any of these.** "Before" is `main`, which is still the live
state of the repository. "After" is the analysis of PR #516 -- a projection of what `main` becomes
when the epic lands, not a figure `main` has ever reported.

| Metric            | `main` (back end only) | PR #516 (honest) | Delta    |
| ----------------- | ---------------------- | ---------------- | -------- |
| `coverage`        | **52.4%**              | **55.4%**        | **+3.0** |
| `lines_to_cover`  | 17,750                 | 20,065           | +2,315   |
| `uncovered_lines` | 8,450                  | 8,980            | +530     |

```bash
# main -- the repository's live figure
curl -sS "https://sonarcloud.io/api/measures/component?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage,lines_to_cover,uncovered_lines"
# PR-scoped -- add &pullRequest=516
```

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

**Consequence for the floor work in 2.10:** the front end is the stronger half rather than the
weaker one, so any argument that assumed "Angular is untested" starts from a false premise. **But do
not set a floor against 55.4% or 55.0%** -- both are PR-scoped projections. `main` reports 52.4%,
and 2.10's floors run on branches, not on the projection. See 2.10.

`new_lines_to_cover` was 0 on that PR -- it changes only a workflow file -- so this run says nothing
about the new-code gate. That gate is separate and is reported by the external
`SonarCloud Code Analysis` status, not by the `SonarCloud: Analysis` Actions job.

---

## 2.7 Audit `sonar.coverage.exclusions` (added 2026-09-02)

**Own item, not part of the threshold task.** Raised because the exclusion list has never been
audited and 2.9 proved it was wrong in a way that mattered.

Until 2026-09-02 it excluded `angular/src/**/*.ts` and `angular/src/**/*.html` -- the entire front
end -- which turned out to be the BETTER-tested half at 77.1%. The reported 52.4% was a measurement
hiding its own subject. That exclusion is gone; the rest of the list has never been checked:

```text
**/Program.cs,**/*Module.cs,**/*DbContext*.cs,**/Migrations/**,**/TenantMigrations/**,
angular/src/app/proxy/**,**/*.module.ts
```

**Why it matters NOW rather than later.** With a 90% changed-lines floor arriving in 2.10, every
file that is wrongly INCLUDED becomes a source of false failure, and every file wrongly EXCLUDED
inflates the number the floor is set against. At 80% there was slack for both errors; at 90% there
is not.

**One list, two consumers.** The same exclusion list must feed both SonarCloud and 2.10's check. Two
lists drift, and a drift here means the two numbers disagree with nobody able to say which is real.

**Ask of each entry:** is this genuinely untestable, or merely untested? `Program.cs` and generated
proxy code are defensible. `*Module.cs` covers ABP module classes that contain real configuration
logic -- some of which this epic has already changed and tested. `*DbContext*.cs` is a broad wildcard
that may catch more than intended.

**Acceptance (EARS):** WHEN a path is excluded from coverage, THE SYSTEM SHALL exclude it because the
code is not meaningfully testable, and each entry shall carry a recorded reason.

### 2.7 AUDIT RESULT (2026-09-02) -- one dead pattern, two over-broad, three defensible

File reading only; no credential, no stack, no gate touched. Every count below is reproducible with
the command shown.

| Pattern                    | Matches | Verdict                                                             |
| -------------------------- | ------- | ------------------------------------------------------------------- |
| `**/*.module.ts`           | **0**   | **DEAD** -- remove                                                  |
| `**/*Module.cs`            | 16      | **OVER-BROAD** -- excludes real, branching, security-relevant logic |
| `**/*DbContext*.cs`        | 8       | **OVER-BROAD** and partly redundant                                 |
| `**/Program.cs`            | 4       | defensible -- thin host bootstrap                                   |
| `**/Migrations/**`         | 181     | defensible -- generated                                             |
| `**/TenantMigrations/**`   | 33      | defensible -- generated                                             |
| `angular/src/app/proxy/**` | 218     | defensible -- generated                                             |

```bash
find src test -name '*Module.cs' | wc -l          # 16
find src test -name '*DbContext*.cs' | wc -l      # 8
find angular/src -name '*.module.ts' | wc -l      # 0
```

**`**/\*.module.ts` matches NOTHING.\*\* This app uses standalone components; there are no NgModules.
Harmless but dead, and dead config is misleading -- someone reading the list believes Angular modules
are being excluded for a reason.

**`**/\*Module.cs` is the one to worry about.\*\* These are not thin registration files:

| File                                 | Lines | Branch constructs |
| ------------------------------------ | ----- | ----------------- |
| `CaseEvaluationHttpApiHostModule.cs` | 1,477 | **42**            |
| `CaseEvaluationAuthServerModule.cs`  | 591   | **14**            |

`CaseEvaluationAuthServerModule.cs` contains the ADR-006 tenant-resolver rebuild -- the code that
clears ABP's default resolver chain so a caller cannot switch tenants with `?__tenant=GUID`. That is
**explicitly HIPAA-relevant** by its own comment, it has 14 branches, and it is excluded from
coverage. "Genuinely untestable, or merely untested?" answers itself here.

10 of the 16 are production; 6 are test infrastructure, which would not count toward coverage anyway.

**`**/_DbContext_.cs`is over-broad AND partly redundant.** Of its 8 matches, 2 are the EF model
snapshots -- and those are already excluded by`**/Migrations/**`and`**/TenantMigrations/**`, so
the wildcard adds nothing for them. The 6 it uniquely excludes include `CaseEvaluationDbContext.cs`(175 lines) and`CaseEvaluationTenantDbContext.cs` (129).

**Those types are exercised by the repo's own tests.** Seven test files reference them, including
`MultiOffice/MultiOfficeIsolationMatrixTests.cs` -- tenant-isolation tests, the most
HIPAA-consequential suite in the repo. The tests run; their coverage of this code is discarded.

### THE INTERACTION THAT MATTERED FOR THE RATCHET -- SUPERSEDED, kept for its transferable half

**The ratchet this section reasons about was cancelled on 2026-09-02 (see 2.1). There is no 54%
threshold and there will not be one.** The sequencing argument below is dead. The measurement
underneath it is not, and it applies unchanged to 2.10's floors, which is why this is not deleted.

**The live part.** Narrowing these two patterns brings roughly **3,193 raw lines** of production code
into the coverage denominator (2,750 from `*Module.cs` in `src/`, 443 from the real DbContext files).
Coverable lines are a fraction of raw lines, so treat that as an order of magnitude, not a figure.
Most of it is uncovered, so fixing the exclusion list honestly LOWERS the reported percentage.

**The dead part, stated so nobody revives it.** The argument was that setting a 54% ratchet before
fixing the exclusions would make the gate fail on the honesty fix rather than on a regression, so the
exclusions should be fixed first. The reasoning was correct and the conclusion was acted on -- 2.7
landed before any threshold work. It is superseded only because the threshold it protected no longer
exists.

**The transferable rule, which outlives both:** never set a numeric gate against a measurement you
already know is about to change. Fix the measurement, re-measure, then set the gate. That rule is
carried into 2.10.

### 2.7 MEASURED RESULT (2026-09-02): 55.4% -> 55.0% on PR analyses, and the composition is the finding

The exclusion narrowing landed and the analyser re-measured. It fell 0.4 points -- far less than the
"substantial fall" predicted, and the reason it barely moved is the interesting part.

**Both columns are PR-scoped.** Neither figure is `main`, which still reports **52.4%** and will keep
reporting it until the epic lands. Re-verified against the API on 2026-09-03.

| Metric            | PR #516 | PR #519 | Delta |
| ----------------- | ------- | ------- | ----- |
| `coverage`        | 55.4%   | 55.0%   | -0.4  |
| `lines_to_cover`  | 20,065  | 20,623  | +558  |
| `uncovered_lines` | 8,980   | 9,324   | +344  |
| covered (derived) | 11,085  | 11,299  | +214  |

**OF THE 558 COVERABLE LINES THAT CAME INTO SCOPE, 214 WERE ALREADY COVERED -- 38.4%.** The existing
suite was testing that code all along and the exclusion list was discarding the result. That is the
"tests run, their coverage is thrown away" claim measured rather than argued.

**MY OWN ESTIMATE WAS AN ORDER OF MAGNITUDE OUT AND THE CAVEAT IS WHY IT DID NOT MISLEAD.** I
predicted "roughly 3,193 raw lines" and flagged that coverable lines are a fraction of raw. The
actual figure was **558 coverable, 17.5% of raw**. ABP module classes are largely declarative --
`DependsOn` attributes and `Configure<T>(options => ...)` lambdas -- so most of those raw lines are
not executable statements. Stating the estimate as an order of magnitude rather than a figure is the
only reason it was not a wrong prediction presented as a number.

**Floor sensitivity against the PR #519 figure** (line component, 11,299 / 20,623). This is the
shape of the curve, not a floor recommendation -- 2.10's floors are per-stack and must be measured
from the coverage reports, not from this blended number:

```text
floor 55.0%  ->  trips after ~0    additional uncovered lines
floor 54.5%  ->  trips after ~109
floor 54.0%  ->  trips after ~301
floor 53.0%  ->  trips after ~696
```

**The exclusion list is now seven specific decisions instead of three wildcards and a dead entry:**

| Exclusion                                    | Reason                                           |
| -------------------------------------------- | ------------------------------------------------ |
| `**/Program.cs`                              | thin host bootstrap                              |
| `**/Migrations/**`                           | generated                                        |
| `**/TenantMigrations/**`                     | generated                                        |
| `angular/src/app/proxy/**`                   | generated                                        |
| `**/CaseEvaluationDbContextFactory.cs`       | design-time only (`IDesignTimeDbContextFactory`) |
| `**/CaseEvaluationTenantDbContextFactory.cs` | design-time only                                 |
| `**/CaseEvaluationDbContextFactoryBase.cs`   | design-time only                                 |

**Why the two wildcards were REMOVED rather than narrowed**, since that was a deliberate choice:

- `**/*Module.cs` -- the ABP test modules `DependsOn` the production modules
  (`CaseEvaluationApplicationModule`, `CaseEvaluationDomainModule`,
  `CaseEvaluationEntityFrameworkCoreModule`), so those classes EXECUTE during the test run. Their
  coverage was real and discarded, which the +214 above confirms. The two large host modules are not
  booted by any test, so they now count as near-zero -- and that is a gap to reveal, not hide: the
  tenant-resolver rebuild in the AuthServer module is testable by asserting the resolver list.
- `**/*DbContext*.cs` -- two of its eight matches were the EF snapshots, already excluded by the
  Migrations patterns, so the wildcard added nothing for them. The rest are exercised by seven test
  files. Only the three design-time factories are genuinely unreachable at runtime, and they are now
  excluded BY NAME. A named exclusion states what it protects; a wildcard states nothing and grows.

---

## 2.10 Build a separate CI coverage check (added 2026-09-03)

**Replaces the cancelled 2.1.** Designed, not built. This is the next item to be executed.

### What Adrian decided, all settled -- do not re-open these

- **SonarCloud's 80% new-code gate stays exactly as it is.** Do not touch the quality gate.
- A **separate CI check** measures coverage and **blocks from day one.** Not warn-then-enforce.
- **Two floors, one per stack** -- one for the server half, one for the browser half.
- **90% on changed lines**, in addition to the overall floors.
- **The coverage figure goes in the root README.**
- Long-term target, his words: _"as close to 100% coverage on tests as possible ... not just stop at
  80%."_

### Where the coverage data already exists

Verified by reading the workflows. This shapes the design:

| Workflow         | Job              | Produces coverage?                               |
| ---------------- | ---------------- | ------------------------------------------------ |
| `ci.yml`         | `frontend-test`  | **YES** -- `yarn test --code-coverage`, line 257 |
| `ci.yml`         | `backend-test`   | **NO** -- plain `dotnet test`, line 160          |
| `sonarcloud.yml` | the analysis job | both, but for the scanner                        |

**So evaluate the floors inside `ci.yml`, where the tests already run.** The browser half is free --
the lcov already exists. The server half needs `dotnet-coverage collect` wrapped around the existing
`dotnet test`, exactly as `sonarcloud.yml` already does it, which adds a tool install to a job that
already runs rather than a third full test pass.

Artefact paths, verified:

- backend: `coverage.xml` at repo root, from `dotnet-coverage collect "dotnet test ..." -f xml`
- frontend: `angular/coverage/**/lcov.info`

### THE TRAP -- and it is this phase's signature defect

**`ci.yml`'s `frontend-test` job is gated on a `has_tests` condition and skips when unmet** -- the
guard is the `if:` at line 256, with the explaining comment at line 239.

**The mechanism is worse than "the file might be missing", and it is documented rather than
suspected.** Per instance 12 in the catalogue: **a skipped job reports Success and does not block a
merge, even when it is a required check.** So a floor check that inherits that gate does not fail for
lack of input -- it never runs, reports Success, and the merge proceeds. No file need be missing.
Nothing need fail.

Two rules follow, and they are the design constraint for this item:

1. **Never put a `paths:` filter or a conditional `if:` on a job whose check you intend to require.**
2. **Put the condition INSIDE an always-running job that fails when its inputs are absent.** Make
   missing or empty input a hard failure, never a pass.

Then prove it: delete the report and watch the check go red. **That proof is not optional** -- it is
the only thing separating this gate from the thirteen entries in the catalogue below. The same defect
and the same fix apply to 2.11's `Meta: Changed paths` hole; it is written up once in the
`production` section above and referenced from both.

### What is NOT yet measured

**The per-stack split.** The floors cannot carry real numbers without it.

**Measure it from the two coverage reports themselves, not from SonarCloud.** The entire point of
this check is that it stands on its own rather than depending on the thing it double-checks. It also
avoids inheriting SonarCloud's scope problem: SonarCloud analyses only `main`, so its figures cannot
describe the branch a floor actually runs on.

**Do not seed the floors from 55.0% or 55.4%.** Both are PR-scoped projections. `main` reports 52.4%.
The floors must come from the reports produced by the run being gated.

### The exclusion list

**One list, consumed by both SonarCloud and this check.** Two lists drift, and a drift means the two
numbers disagree and nobody knows which is real. The current list is the seven named decisions in
2.7 above; the reasoning for each is recorded there.

### The README badge -- decided, and smaller than it sounds

The root `README.md` carries a placeholder at line 14:

```text
[![Codecov](https://img.shields.io/badge/coverage-pending-lightgrey)](#known-issues-and-roadmap)
```

with a note at line 16 and a roadmap entry at line 575 both saying it is a placeholder. Replace it
with SonarCloud's real coverage badge and retire the note and the roadmap line with it. A
`Quality Gate` badge already exists at line 13 using the same `api/project_badges/measure` endpoint,
so the coverage badge is that URL with `metric=coverage`.

**It will read 52.4%, not 55.0%, and that is correct.** Verified live 2026-09-03 -- the endpoint
returns HTTP 200, `image/svg+xml`, rendering `coverage 52.4%`:

```bash
curl -sS "https://sonarcloud.io/api/project_badges/measure?project=gesco-healthcare-support_hcs-patient-portal&metric=coverage"
```

Adrian's decision, taken 2026-09-03: publish
it now at `main`'s honest number with a one-line note saying what it measures. The reasoning -- his
and the supervisor's independently agreed -- is that the README has carried a fake "coverage pending"
badge for months, and a label claiming a measurement nobody made is precisely the anti-pattern this
phase exists to kill. A README badge describes `main`. The number moves when the epic lands.

The note is what stops it reading as a regression this work caused, given the earlier figure was
never real.

### Acceptance (EARS)

- WHEN a pull request's server-side line coverage falls below the server floor, THE SYSTEM SHALL fail
  a blocking status check.
- WHEN a pull request's browser-side line coverage falls below the browser floor, THE SYSTEM SHALL
  fail a blocking status check.
- WHEN the lines a pull request changes are covered at less than 90%, THE SYSTEM SHALL fail a
  blocking status check.
- WHEN a coverage report is missing, empty, or was not produced because its job did not run, THE
  SYSTEM SHALL fail rather than pass.

### Validation loop

1. `dotnet-coverage collect` produces `coverage.xml`; confirm it is non-empty and parses.
2. `yarn test --code-coverage` produces `angular/coverage/**/lcov.info`; same.
3. Poison each floor: drop coverage below it, watch red, restore, watch green.
4. **Poison the input:** delete each report in turn and confirm the check goes RED, not green.
5. Confirm the check is genuinely blocking and not `continue-on-error`.

Copy the probe-PR pattern from 2.8 -- #513, #515, #517 held deliberate violations and were closed
afterwards.

---

## 2.11 Require the checks in branch protection -- DELIBERATELY LAST

**Do not start this until dispatched.** None of this phase's gates bite until the checks are marked
required in branch protection, so it is tempting to do it early. That is the trap: turning it on
before the gates are individually proven converts a false alarm into a hard block on all work, and
there is no second engineer to unblock it.

Two things must be true first:

1. Every check to be required has been **watched fail and watched pass** -- or has an explicitly
   accepted configuration argument, which is the open question below.
2. **The conditional-check hole is closed.** This is a hard prerequisite, not a preference.

### THE PREREQUISITE: requiring a conditional check buys nothing on the changes it skips

Found 2026-09-03 when `production` was unblocked. All five CI-based required checks on `production`
are conditional on `Meta: Changed paths`, **which is not itself a required check.** Because a skipped
job reports Success even when required (catalogue instance 12), a failure or misclassification in
that one classifier makes all five required checks skip, report Success, and let the merge through
having run none of them.

Full write-up, with the two honest qualifications -- that it pre-dates the epic, and that the
skip-on-failed-`needs` path is inferred rather than observed -- is in the `production` section above.

**Either the classifier becomes a required check too, or the conditions move inside always-running
jobs that fail when their inputs are absent.** The second is the same shape 2.10 must be built in.
**Same defect, same fix, two places** -- fix it once, apply it in both.

**Proving it is a poison test worth running**, and it belongs to this item: make `Meta: Changed
paths` fail, and confirm whether the five dependants report Success. This phase's own rule is that a
gate nobody has watched fail is not a gate; that applies to a hole nobody has watched open.

**The open question this depends on**, carried forward from 2.8 and still unanswered: for the four
gates that cannot be cheaply poisoned -- `CodeQL`, `SonarCloud`, `Dependency Review` (now proven, so
three), `TruffleHog` -- is observed-red required, or is the configuration argument enough?

**One asymmetry to fix while here:** `main` currently requires only `Backend: Build` and
`Frontend: Build`; `development` requires four. Re-derive both lists before changing either.

**Note on the SonarCloud gate, because it is two similarly named things.** The quality-gate result is
reported by the external `SonarCloud Code Analysis` status. The Actions job `SonarCloud: Analysis`
never evaluates it (`sonar.qualitygate.wait=false`). Making the quality gate blocking means REQUIRING
THE EXTERNAL STATUS -- not flipping that job, and not setting `wait=true`.

---

## 2.12 Make `Lint: Markdown` repo-wide blocking (added 2026-09-03)

**Satisfies the dangling note in 2.8** -- "it now reports honestly and still does not gate. Flipping
it needs its own item." This is that item.

**Why it was not possible before.** 2.8 fixed the globs and the check went from _linting zero files_
to reporting **5,271 violations across 431 files**. Blocking on that would have made every pull
request permanently red, which teaches people to ignore red just as effectively as permanent green.
The changed-files-only pattern was the way to land the gate anyway.

**Why it is possible now.** PR #522 took the backlog to zero on `main`, and it is careful work rather
than a relaxed bar:

- **`MD024` had never been configured at all.** The config said `"allow_different_nesting"`, which is
  not a markdownlint option, so it was silently ignored and the rule ran at full strictness --
  catalogue instance 14. Corrected to `"siblings_only": true`.
- **`MD025` was counting YAML front matter as a title.** 87 of 89 flagged files had front matter plus
  exactly one body H1. A config correction, measured, with the 7 genuine cases fixed by hand.
- **Three rules disabled with per-rule evidence** argued from document content, not blanket
  relaxation: list numbers that are identifiers rather than sequence, adjacent callouts that would
  merge into one quote, bold used as labels.
- **Verified against a `git archive` extract** -- what CI actually checks out -- 378 files, 0 errors.

**Precondition: the epic must carry main's config and fixed files.** Met by the catch-up merge that
this PR performs. Before it, the epic still had the broken `MD024` key.

**What this retires.** The changed-files-only scoping in `lint-meta.yml` becomes unnecessary for
markdown. **Do not remove the pattern from the record** -- it is documented as reusable above and
phase 7 will want it for rule families. Retire its use here, not the idea.

**Acceptance (EARS):** WHEN a pull request introduces a markdown violation anywhere in the
repository, THE SYSTEM SHALL fail a blocking status check.

**Validation:** lint the whole repository at zero errors first, then flip, then **poison it** -- add a
violation in a file the PR does not otherwise touch and confirm red, since that is the case
changed-files-only would have missed.

---

## THE LOOSE-FILE CLEARANCE (2026-09-03) -- recorded because the handoff described a tree that no longer exists

Eight untracked files under `docs/research/system-design-2026-08-28/` blocked the catch-up merge:
they exist on `main`, and git refuses to overwrite untracked files.

The handoff document claimed they were "already on main" and proved it with a zero-line
`git diff origin/main` on the README. **That proof only ever covered the README, and by 2026-09-03
five of the others DIFFERED from `main`** -- because #520/#522 reformatted them after they shipped.

**Verified before moving anything**, line by line rather than by summary. Every differing line was
one of: a fence gaining `text` (MD040), a bare URL gaining angle brackets (MD034), a bracket gaining
an escape, a heading losing a trailing period (MD026), or a blank line. **Zero substantive
differences, and the direction was that `main` held the FIXED versions while the loose copies were
the pre-fix originals.**

**Moved, not deleted**, to `C:\src\patient-portal\handoff\worktree-leftovers-2026-09-03\`.
Deleting untracked files is unrecoverable; a move costs nothing and stays reversible. `git clean` was
deliberately not used -- a blanket clean in a worktree two sessions share is the wrong instrument
regardless of what it would have caught.

**The transferable point:** a handoff document that describes a working tree is a claim with a
shelf life. This one was true when written and half-stale two days later, and the half that went
stale was the half nobody had re-checked.

---

## CATALOGUE: checks that reported success without having run

**This is the most useful artefact the epic has produced.** Eighteen instances in three days, nine
of them in this epic's own work -- in its tooling, its counts, its merges, its handoffs and its own prior claims. The lesson is not "tools lie" -- it is that a green result is evidence
only if you know what was examined.

| #   | Instance                                                               | How it presented             |
| --- | ---------------------------------------------------------------------- | ---------------------------- |
| 1   | A cancelled job read as a pass                                         | green                        |
| 2   | A run-level conclusion masking a failed job                            | green                        |
| 3   | `continue-on-error: true` -- **2 of 11, see the correction below**     | green                        |
| 4   | A validation hook failing open under load                              | green                        |
| 5   | `markdownlint` passing while linting **zero files**                    | green                        |
| 6   | A stub check green by construction (`doc-check.yml`, see 2.4)          | green                        |
| 7   | Four unit tests passing against a premise false at runtime             | green                        |
| 8   | A `\|\| true` on a test run                                            | green                        |
| 9   | A partial grep presented as an enumeration                             | plausible count              |
| 10  | `git cat-file -e <branch>:<path>` in Git Bash, stderr suppressed       | confident **false ABSENT**   |
| 11  | "These 11 checks cannot fail" -- itself unverified, and wrong for 9    | a confident **false alarm**  |
| 12  | **A SKIPPED job reports Success and does not block, even if required** | green                        |
| 13  | A correct command run against a **stale worktree**                     | correct-looking wrong answer |
| 14  | `"allow_different_nesting"` is not a markdownlint option -- ignored    | looked configured            |
| 15  | A background launcher's `exit 0`, unrelated to the job it launched     | reported success             |
| 16  | A **verified** claim restated wrongly from memory two messages later   | plausible, and wrong         |
| 17  | A **clean auto-merge** that silently duplicated 96 lines of the record | no conflict markers          |
| 18  | A **trigger-list claim in the handoff**, inherited and repeated twice  | a confident **false alarm**  |

**Instances 10, 11, 13, 15, 16, 17 and 18 are self-inflicted rather than inherited** -- committed by
the tooling and the sessions doing the verifying, not found in the repository. **Instance 12 is the
most consequential for design; instance 17 is the one that would have silently corrupted the
record; instance 18 is the one that survived a handoff and was repeated by two sessions.**

### THE CORRECTION TO INSTANCE 3 -- the headline framing was wrong and is now sharper

**"Eleven `continue-on-error` settings" was carried as eleven instances of a check reporting success
without having run. That is true of 2 of the 11, not 11.** The classification nobody had made:

```text
STEP  ci.yml:101                 <- genuinely masks; the check reports success
STEP  dependency-review.yml:35   <- genuinely masks; the one #518 removed
JOB   ci.yml:266, ci.yml:301, codeql-pr:26, commitlint:30,
      lint-meta:19, lint-meta:37, pr-title:20, sonarcloud:31, trufflehog-pr:29
```

**Step-level `continue-on-error` genuinely masks the check. Job-level masks the workflow RUN
conclusion only** -- the check still fails honestly. Proof is in the `production` section above:
PR #384, run `33538330456`, run `success` / job `failure` / **check `FAILURE`**.

The framing does not collapse; it sharpens in three ways worth keeping:

1. **#518 fixed a genuinely vacuous gate.** `dependency-review.yml:35` was step-level, so the
   deny-licences check really did report success while failing.
2. **The 9 job-level settings explain instance 2** rather than being a separate mystery. "A run-level
   conclusion masking a failed job" is precisely what job-level `continue-on-error` does.
3. **#514 was still correct work, but it fixed a REPORTING defect for 9 of them, not a GATING one.**
   Said plainly rather than letting the stronger claim stand.

**The tally of 11 -> 5 settings is unchanged and still correct** as a count of settings. What changed
is what the count means.

**10 -- the argument never reached git.** MSYS rewrites a `<rev>:<path>` argument when both sides of
the colon contain a slash: `origin/main:.github/workflows/doc-check.yml` becomes
`origin\main;.github\workflows\doc-check.yml` and exits 128. On its own it is a loud `fatal`. Wrapped
as `$(git cat-file -e ... 2>/dev/null && echo PRESENT || echo ABSENT)` the `2>/dev/null` swallows the
fatal and `||` converts exit 128 into a clean ABSENT. It reported a deleted file for one that was
plainly present.

Fixes, in strict order of preference -- **the order matters and the first beats the third**:

1. **Remove the trigger.** Use a form that takes no colon: `git ls-tree <rev> -- <path>` for
   existence, `git diff <rev> -- <path>` for content.
2. **Or disable the conversion:** `MSYS_NO_PATHCONV=1`, verified to return exit 0.
3. **Never put `2>/dev/null` on an existence check.**

**Why 3 is necessary but NOT sufficient, which is the easy thing to get wrong here:** with stderr
visible the command still exits 128, so `&& echo PRESENT || echo ABSENT` still prints ABSENT -- now
with a `fatal` above it that a reader has to notice and interpret correctly. Making the failure loud
does not make the answer right. **Removing the trigger does.**

`git show` is affected identically -- any subcommand taking a `<rev>:<path>` argument is. It was hit
twice in this epic on `git show origin/main:.github/workflows/<file>.yml` and worked around with
`MSYS_NO_PATHCONV=1` both times without being diagnosed.

Every branch in this repo has a slash and every workflow path has slashes, so the trigger condition
is met by default on exactly the checks this epic runs most. **Adrian has taken this machine-wide**:
the specific mangling is in his shell-environment rules and the general principle -- never hide error
output on an existence check -- is in his verification rules, so it now loads in every session on
every project. This copy stays because the record needs its own.

**11 -- a claim that checks could not fail, which was itself never verified.** The inverse of every
other entry, and the reason it belongs on the list. Eleven `continue-on-error` settings were reported
as eleven anti-gates. Nobody checked whether the setting does what it was assumed to do; when someone
finally did, 9 of the 11 turned out to fail honestly at the check level. **A false alarm is the same
defect as a false pass** -- an unverified claim about a gate -- and it costs more, because it sends
people to fix what is not broken. See the correction to instance 3 above.

**12 -- a skipped job reports Success and does not block, even when it is required.** From GitHub's
documentation. This is genuinely vacuous in the way job-level `continue-on-error` is not, and it is
**the real mechanism behind the trap in 2.10**: the risk was framed as "a coverage file might be
missing", but the actual risk is simpler and worse -- if the job skips, its required check reports
Success and the merge proceeds. No file need be missing. Nothing need fail.

- **The rule: never put a `paths:` filter or a conditional `if:` on a job whose check you intend to
  require.** On every pull request that does not match, the gate silently passes.
- **If a gate must be conditional, the condition belongs INSIDE an always-running job that fails when
  its inputs are absent.** That is the shape 2.10 must be built in, and the fix 2.11 needs for the
  `Meta: Changed paths` hole. Same defect, same fix, two places.

**13 -- the tree was not the tree the claim was about.** A `continue-on-error` count taken in the
shared worktree returned 6; the same command against `origin/feat/production-hardening` returned 5.
The worktree was one commit behind and predated #518. Worse, reading `dependency-review.yml` from
that stale tree showed a `continue-on-error: true` that #518 had already removed, which very nearly
became a published claim that a merged, poison-proven gate did not work. The check ran, the file was
real, the grep was correct -- and the answer was still wrong, because the tree was not the tree the
claim was about.

- **The rule: name the ref, not just the command.** Every count in this record cites the ref it was
  measured on. Policy alongside naming the scope on coverage figures -- see the standing rule at the
  top of this file.

**14 -- a setting that looks configured and is not.** `.markdownlint.json` carried
`"MD024": { "allow_different_nesting": true }`. **That option name does not exist in markdownlint**,
so it was silently ignored and MD024 ran at full strictness while appearing tuned. Found by a third
session on the same day as instances 5 and 6, which are the same species. **Three independent
instances of this in one repository in one day is not a coincidence; it is a thing to look for
deliberately.** `main`'s corrected value is `"siblings_only": true`, and it arrives with the
catch-up merge.

**15 -- `exit 0` from a launcher says nothing about the job.** Three times in twenty minutes while
measuring coverage: `nohup ... &` reported success while the script was still building; a relaunch
reported success having done **nothing at all** (`setsid: command not found`, which does not exist in
Git Bash); and piping build output through `tail` buffered until EOF so a working build looked hung.
Each time the reported status was success and the actual state was different.

- **The rule: assert on the artefact, not on the exit code.** All three were caught only because the
  expected output file was absent.

**16 -- a verified claim degraded in transit.** The conflict set for the catch-up was computed
correctly with one command, then restated from memory two messages later as a different, wrong
number. Distinct from every other entry: not an unverified claim, but a **verified one corrupted by
being retold.** Both sessions did this in the same exchange, in opposite directions -- one said four
files, one said nine; the measured answer was two.

- **The rule: re-run the command rather than quoting yourself.** A number is only as good as its
  most recent derivation.

**17 -- a clean auto-merge is not a correct one, and this one duplicated the record.**
`01-blockers.md` merged with **no conflict markers** and git reported success. The result contained
two sections twice -- `## Admitted from the system design research` and `## 1.7 ...` -- roughly 96
duplicated lines, because both branches had independently added sections with the same headings and
git aligned neither. **Nothing in git's output indicated a problem.** It was caught by `MD024`
(duplicate headings) when the merged result was linted, which is precisely why the acceptance
criterion for a merge is a passing linter and not the absence of conflicts.

- **The rule: the acceptance for a merge is that the merged result passes its checks.** "No
  conflicts" is not a result; it is the absence of one kind of evidence.

**18 -- a false claim about a trigger list, inherited from a handoff and repeated by two sessions.**
The implementer handoff stated that `feat/ci-coverage-floors` does **not** match `lint-meta.yml`'s
trigger list, so a probe based there "would prove nothing and look clean doing it". The supervisor
repeated it to the incoming implementer, which repeated it back in its own analysis. **Nobody read
the trigger list.** It is:

```bash
# both lint-meta.yml and ci.yml, measured on fcc529dc
# on: pull_request: branches:
#   [main, development, staging, production, chore/dependency-updates, "feat/**", "fix/**"]
```

`feat/ci-coverage-floors` matches `feat/**`. **A probe based there fires both workflows.**

**The diagnosis of #528 was right and only its generalisation was wrong**, which is what made it
survive. #528's base was `chore/markdown-gate-repo-wide`; the list carries `chore/dependency-updates`
as a LITERAL, not `chore/**`, so that base genuinely matched nothing. The true rule is "check the
base against the list", and it got compressed into a claim about one specific branch that was false.

- **The rule: a handoff is a claim with a shelf life, and an inherited claim is not a verified one.**
  This entry is the instance-11 shape -- a false alarm rather than a false pass -- and it costs the
  same way: it sends people to fix what is not broken. Re-derive, do not inherit.

**How to count this table:** the total is **the number of rows in it**. Do not increment from
memory -- that error has already happened once and vacated a slot behind it.

**Why nine became seventeen in a single session:** because the lens was being applied deliberately,
including to the epic's own prior conclusions. The class is not rare; it is invisible unless looked
for. **Two of the four new entries corrected earlier entries rather than adding new ones**, which is
the strongest argument for re-deriving a claim instead of inheriting it.
