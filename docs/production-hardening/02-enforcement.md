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

## Validation loop for this phase

The gates are the deliverable, so the validation is adversarial:

1. Open a scratch PR that violates each gate deliberately (an unpinned action, a tag-referenced
   action, an untested public method).
2. Confirm each intended check goes red, and that the red check is _required_ rather than advisory.
3. Remove the violations, confirm green.
4. Record in the phase notes which checks are required versus advisory -- `main` currently requires
   only `Backend: Build` and `Frontend: Build`; `development` requires four. That asymmetry is
   itself worth fixing here.
