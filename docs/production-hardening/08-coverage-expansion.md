# Phase 8 -- Coverage expansion

**Change class:** pure addition. New tests only; no production code changes in this phase. If
writing a test reveals a defect, that is a finding -- log it and fix it as its own task, do not fold
a behaviour change into a coverage commit.

**Baseline: 52.4% across 116,210 lines. Target: as high as reached before handoff.**

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

```
dotnet test
npx ng test --watch=false --browsers=ChromeHeadless
```

Re-measure:

```bash
curl -s "https://sonarcloud.io/api/measures/component?component=gesco-healthcare-support_hcs-patient-portal&metricKeys=coverage,ncloc"
```

Baseline: `coverage=52.4`, `ncloc=116210`.
