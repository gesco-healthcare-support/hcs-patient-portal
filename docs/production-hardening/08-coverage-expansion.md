# Phase 8 -- Coverage expansion

**Change class:** pure addition. New tests only; no production code changes in this phase. If
writing a test reveals a defect, that is a finding -- log it and fix it as its own task, do not fold
a behaviour change into a coverage commit.

**Baseline: 37.1% across 117,310 lines. Target: as high as reached before handoff.**

> **Corrected 2026-09-08. This said 52.4% across 116,210 lines, which is now wrong by fifteen
> points.** The number did not get worse -- the measurement got honest, and it happened in work that
> has already merged. Item 2.13 widened the Angular instrumentation so a source file with no spec is
> COUNTED AS UNCOVERED instead of being invisible to the report. 184 files left that blind spot and
> the front end fell 69.46% -> 20.97%; the combined figure followed it down.
>
> ```bash
> curl -s "https://sonarcloud.io/api/measures/component?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage,line_coverage,branch_coverage,ncloc"
> # coverage 37.1   line_coverage 41.0   branch_coverage 17.5   ncloc 117310     (2026-09-08)
> ```
>
> **The split matters more than the headline, because it says where this phase's work actually is:**
>
> | Half      | Covered  | Of           |
> | --------- | -------- | ------------ |
> | Back end  | ~72%     | 45,891 lines |
> | Front end | **~21%** | 9,062 lines  |
>
> **Branch coverage, 17.5%, is the weakest figure in the repository** -- the decision points, which is
> to say the error and edge paths, against 41.0% of lines. **Line coverage alone will overstate how
> much of this phase is done.**
>
> One artefact to carry so nobody reads it as gaming: roughly one line per newly visible Angular file
> counts as hit merely because karma executes a file's top-level code when it loads it as an entry
> point. Covered lines rose 1,713 -> 1,900 when 187 files became visible **with no new tests**. So
> real behavioural coverage on the front end is somewhat below 21%. Item 2.13 in
> [02-enforcement.md](02-enforcement.md) carries the measurement and the three readings that mislead
> when taken alone.

**Adrian's ruling, 2026-09-08: this phase stays UNTARGETED.** He was offered 90%+ overall, a
front-end-only target, and a branch-coverage target. Reaching 90% was measured at roughly **14,300
lines to bring under test, about 84% of everything currently uncovered** -- very likely larger than
the remaining phases combined. He chose to keep the stopping rule below and revisit when the phase
starts. **Do not seed a target from the baseline above; it is a measurement, not an objective.**

This is the open-ended phase and the one certain to be incomplete. That is by design and it is
safe, because phase 2 stops the number sliding backwards while this runs.

---

## Why this is last and why that is not a demotion

Every earlier phase produces a bounded, finishable unit of work. This one does not. Putting it last
means the epic accumulates completed, locked-in improvements rather than a half-finished sprawl.

More importantly: **the ratchet does most of the work here.** Once the new-code gate from phase 2 is
honoured, every future change carries its own tests automatically. Coverage then rises as a
by-product of normal development, forever, without anyone running a coverage campaign. This phase
is about buying down the _existing_ debt that the ratchet cannot reach.

A successor inheriting this at 60% with a working gate is in a far better position than one
inheriting 85% with a gate everyone bypasses.

---

## Ordering -- by consequence of failure, not by ease

Phase 3 already covered the five dangerous paths. This phase expands outward from them.

1. **Everything adjacent to phase 3's five paths.** Tenancy, authorization, PHI egress, packet
   generation, booking. The surrounding code, not just the core.
2. **Application services with no tests at all.** The coverage doc claims 8 of 15 entities covered;
   that document is badly stale (see below), so re-derive the real list before trusting it.
3. **Angular components on external-user flows.** These are what customers touch and where a
   regression is most visible. Integration-weighted per the testing rule -- render the component,
   drive it, assert what a user would see.
4. **The 16 skipped tests** (`xUnit1004`, phase 7 Tier C). Re-enabling a legitimate test is cheaper
   coverage than writing a new one, and each skip is a known hole.
5. **Everything else**, by file size descending -- large uncovered files carry the most risk per
   test written.

---

## 8.1 -- the denominator, SETTLED 2026-09-09

Before any figure in this phase means anything, the denominator had to describe application code.
**1,842 uncovered lines sat outside `src/` and `angular/`**, in `tools`, `scripts`,
`.claude/scripts`, `docker/packet-renderer` and `tests/e2e-demo`.

**The answer was mostly "it belongs", and that is the part worth recording** -- the cheap move was
to exclude all of it and watch the percentage rise. Adrian's ruling, on measurement:

| Verdict | Lines     | What                                                                                                                                        |
| ------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| **IN**  | **1,294** | `docker/packet-renderer/app.py` 44; `tools/packet-templates` 882; `scripts/coverage-gate.py` 223; `.claude/scripts/verify_structure.py` 145 |
| **OUT** | **548**   | `build-repo-map.py` 204; `import-issues.py` 244; `check-links.py` 74; `tests/e2e-demo` 26                                                   |

### Why the big items stayed IN

- **`docker/packet-renderer/app.py` is a production service**, not tooling.
  `docker-compose.prod.yml:144` builds it, `:286` reaches it at `PacketRenderer__Url`, and
  `Dockerfile:76` runs it under gunicorn. It renders the PHI-bearing packet PDFs.
- **`tools/packet-templates` is baked into that image.** `Dockerfile:4` says so outright --
  "COPIED in and RUN at image build" -- and `:60` copies it to `/app/generators`. Its output ships.
- **`scripts/coverage-gate.py` is the instrument** every figure in this phase comes from, and it has
  no tests. The catalogue already records three defects in it (instances 19, 20, 21), all found by
  ad-hoc harnesses. **A coverage gate with no tests is the shape phase 2 existed to remove.**
- **`.claude/scripts` split by whether it gates.** `verify_structure.py` runs at `ci.yml:309` with
  no `|| true` and can fail the build, so it is in. `check-links.py` at `:311` carries `|| true` by
  design, so testing it protects nothing.

**The 1,294 IN lines remain uncovered deliberately**, pending Python test infrastructure -- there is
none in this repository (`git ls-files | grep -iE "pytest|conftest|tox\.ini|pyproject"` returns
nothing). That is package **8.1b**, deliberately separated so a red CI has one candidate cause.
**Do not read those lines as forgotten.**

### One correction to the framing, and one measurement trap

**The PowerShell and shell were never in the denominator.** `scripts` holds 41 `.ps1` and 11 `.sh`;
Sonar reports `ncloc` for them and **no `lines_to_cover`**. This was always a Python and TypeScript
question.

**The per-file figures had to be queried file by file.** `api/measures/component_tree?ps=500`
returns exactly 500 components and silently truncates -- it omitted `coverage-gate.py`,
`verify_structure.py` and `check-links.py` entirely. A count read off that page would have been
confidently short. Query `api/measures/component` per file, or page properly.

### Where the exclusions live, and which consumer they reach

In `.coverage-exclusions` -- the one list with two consumers, each entry named with its reason.
**Neither consumer was assumed to read them:**

- **`sonarcloud.yml` APPLIES them.** Its list-building command was reproduced locally; all four new
  patterns appear in the string it passes to `sonar.coverage.exclusions`, and none of the four IN
  paths does.
- **`coverage-gate.py` READS them and they match nothing**, because it ingests only `--lcov`
  (karma, `angular/src`) and `--cobertura` (.NET). No Python enters either report. Proven by
  positive control rather than by inspection: a temporary `src/Beta/**` against a fixture moved it
  from 25.00% (1/4 over 2 files) to 50.00% (1/2 over 1 file), then was removed and the file
  verified byte-identical.

That distinction matters against the list's own rule that an entry which can never match is noise.
These match in one consumer, not neither -- which is precisely why they are needed: without them
Sonar's denominator counts tooling no runner will ever cover.

---

## Correct the coverage documentation first

`docs/testing/coverage-status.md` declares itself "the single source of truth for backend test
suite counts" and claims **115 backend test methods across 17 files**, last verified 2026-04-24.

The suite currently runs roughly **2,290 backend tests**. The document is off by a factor of twenty
and it is linked from `docs/INDEX.md`, so it actively misinforms anyone onboarding -- including the
successor this epic is being built for.

Fix this early in the phase. Either regenerate it from a real run or delete it and point at the
test command. A stale source of truth is worse than none, and this one is load-bearing for exactly
the audience that matters here. Already logged in `docs/backlog.md`.

---

## Re-derive the audit ratio before anything is sized off it

**Admitted from the system design research (2026-08-31).** Not coverage work, but it belongs with the
measurement tasks and it blocks a number being taken to the business. Evidence in
[10-research-corrections.md](10-research-corrections.md) section 2.4.

The research prices entity-history capture-at-source as **the cheapest order of magnitude available**
and states every downstream infrastructure cost is linear in the audit ratio. Its APP-OWN-07 asks for
capture to be made an explicit allow-list.

Reading source says it already is one. No `EntityHistorySelectors`, no `AddAllEntities`, no
`SaveEntityHistoryWhenNavigationChanges` and no `AbpAuditingOptions` anywhere in `src`; capture is
driven by 25 types carrying `[Audited]`, with zero `DisableAuditing`.

**So the saving priced as cheapest appears already taken, and the ratio it inherits from the brief is
unverified.** One query settles it - row counts of the five audit tables against appointment count,
per office:

```sql
SELECT OBJECT_NAME(p.object_id) AS table_name, SUM(p.row_count) AS rows
FROM sys.dm_db_partition_stats p
WHERE p.index_id IN (0,1) GROUP BY p.object_id ORDER BY rows DESC;
```

Run it per office database. It converts several `UNKNOWN` cells in the research's capacity model to
`MEASURED`, and it is the single number the 734-1,084 hour portfolio estimate is most sensitive to.
**Do not take that estimate to the business before this runs.**

---

## Method

- **Do not chase the percentage.** Coverage is a tool for finding untested code, never a quality
  score -- per `~/.claude/rules/testing.md`. A file at 90% with assertion-free tests is worse than
  one at 40% with real ones. `full-logout.spec.ts:47` is the proof this repo already has that
  problem.
- **Tests that resemble real usage**, not tests bound to implementation details. The latter make
  refactoring harder, which is the opposite of what this epic is for.
- **Verify substituted dependencies against production behaviour.** The two live traps in this
  codebase: NSubstitute auto-mocks interface-returning members, so an unconfigured `.Current` is a
  stub rather than null; and `ICurrentTenant.Change(id)` sets the id but leaves `Name` null. Both
  hid real defects behind green tests.
- **Synthetic data only.** No real or real-looking SSN, MRN, DOB, or address, per
  `~/.claude/rules/hipaa.md`.
- **Shape the suite by architecture:** unit-heavy for the .NET domain, integration-weighted for
  Angular, few end-to-end.

---

## Done bar

There is no completion criterion, so define the stopping rule instead:

- Stop when the horizon runs out, not when a number is hit.
- At that point, record in the handoff: the coverage figure reached, which of the five critical
  paths are fully covered, and the prioritised list of what remains.
- **The gate from phase 2 must be in place before this phase stops**, whatever the number. That is
  the difference between handing over a foundation and handing over a snapshot.

---

## Validation loop

```text
dotnet test
npx ng test --watch=false --browsers=ChromeHeadless
```

Re-measure:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage,line_coverage,branch_coverage,ncloc"
```

Baseline, MEASURED 2026-09-08: `coverage=37.1`, `line_coverage=41.0`, `branch_coverage=17.5`,
`ncloc=117310`. **Read all three coverage metrics, not just the headline** -- the headline blends
lines and branches, and this repository's branch figure is less than half its line figure, so the two
move at very different rates.
