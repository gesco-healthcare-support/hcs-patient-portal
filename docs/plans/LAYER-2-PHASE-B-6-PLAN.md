# Layer 2 Phase B-6 -- Test Coverage

**Parent:** [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md)
**Active child:** TBD -- draft Tier 2 plan at `docs/plans/YYYY-MM-DD-phase-b6-tier2.md` in the next plan-mode session (Tier 2 scope summary below; do NOT pre-create the file).

## Status summary

Expand SonarCloud overall coverage from ~7% baseline to >= 60%, new-code coverage >= 80%, Quality Gate green on new code. Three tiers organised by entity priority. **Tier 1 COMPLETE 2026-04-24** -- all 6 PRs MERGED (PR-0 #99, PR-1A #101, PR-1B #102, PR-1C #104, PR-1D #129, PR-1E #135, plus sub-PRs #103 identity-seed and #115 tenant cleanup). Tier 2 and Tier 3 not yet started.

Coverage trajectory: 7.3% baseline (verified `main@ab7461ad2c` on 2026-04-20) -> 19.6% after PR-1D (#129, measured 2026-04-24 pre-PR-1E). Post-PR-1E measurement pending SonarCloud refresh on `main@ca8af28`; expected 22-30%+ range. Tier-1 target was 25-30%. Full B-6 target: >= 60% overall, >= 80% new-code, Quality Gate green on new code.

## Sub-items

| Tier | Scope | Plan | Status |
|---|---|---|---|
| 1 | 5 critical entities (Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys) + PR-0 shared test infra | [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) | **MERGED 2026-04-24** (PR-0 #99, PR-1A #101, PR-1B #102, PR-1C #104, PR-1D #129, PR-1E #135 + sub-PRs #103, #115) |
| 2 | 3 secondary entities (AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails) | drafted after Tier 1 merges + coverage delta measured | NOT STARTED |
| 3 | 5 host-only lookup entities (States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices) | drafted after Tier 2 merges | NOT STARTED |

## Currently active

None -- Tier 1 just closed. The next atomic unit is **drafting the Tier 2 plan** (`docs/plans/YYYY-MM-DD-phase-b6-tier2.md`) in a fresh plan-mode session, using the Tier 2 scope summary below plus [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) sections 11 and 12 for context.

## Upcoming queue

1. **Tier 2 planning** -- next atomic unit. Prerequisite: SonarCloud coverage delta from Tier 1 should be verified (expected 25-30%+; readable on the SonarCloud project dashboard once the post-PR-1E analysis on `main` finishes). Tier 2 seed infrastructure open question: its 3 entities FK into `Appointment`, which is NOT yet seeded in the orchestrator -- Tier 2 drafting must decide between adding Appointment + DoctorAvailability Wave-2 seeds to the orchestrator vs. inline-seeding minimal Appointments in each test body. This decision shapes the PR count (1 big seed-infra PR + 3 entity PRs vs. 3 entity PRs with denser setup in each).
2. Tier 3 -- draft after Tier 2 merges.
3. B-6 closure -- verify coverage targets (>= 60% overall, >= 80% new-code, Quality Gate green on new code); flip Quality Gate from informational to required.

## Scope summary for un-drafted sub-items

### Tier 2 -- Secondary entities

From [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) sections 11 and 12:

- **AppointmentAccessors** -- View/Edit access grants per appointment; attorney-scoped filtering in `GetListAsync` relies on this.
- **AppointmentApplicantAttorneys** -- Join entity Appointment <-> ApplicantAttorney <-> IdentityUser.
- **AppointmentEmployerDetails** -- Employer info per appointment (name, occupation, address).

Each is tenant-scoped (IMultiTenant) and FK-depends on Appointment. Tier-2 drafting waits on (a) seed infrastructure for DoctorAvailabilities Wave 2 + Appointments Wave 2 if needed, and (b) the Tier-1 SonarCloud measurement to inform sequencing.

### Tier 3 -- Host-only lookup entities

From [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) sections 11 and 12:

- **States** -- most-referenced host-scoped lookup (5 inbound FKs).
- **AppointmentTypes** -- IME types (Orthopedic, Neurological, ...).
- **AppointmentStatuses** -- host-scoped status-label lookup (separate from the `AppointmentStatusType` enum used on `Appointment`).
- **AppointmentLanguages** -- interpreter language lookup.
- **WcabOffices** -- workers' comp board office lookup with Excel export.

All host-scoped, minimal business rules. Simplest tier; mostly CRUD + host-only scoping tests. Drafting waits until Tier 2 closes so the tier boundary remains clean.

### B-6 closure

When Tier 1 + 2 + 3 merge, the final B-6 PR verifies: SonarCloud overall coverage >= 60%, new-code coverage >= 80%, Quality Gate green on new code. On success, Phase B closes and the Layer-2 pointer moves to Phase C planning.

## When this level completes

Pop up to [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md); mark B-6 MERGED there. Phase B then completes and Level 1 pointer moves to drafting Phase C.

## Links

- Context + tier breakdown + external references: [PHASE-B6-TEST-COVERAGE-KICKOFF.md](./PHASE-B6-TEST-COVERAGE-KICKOFF.md) (section 12 tier breakdown, section 11 priority matrix, section 16 external references to study before drafting Tier 2/3).
- Test conventions: `test/CLAUDE.md`, `.claude/rules/test-data.md`.

## Last updated

2026-04-24 -- Tier 1 MERGED (PR-1E #135 closed the set); active-child pointer advanced to Tier 2 planning.
