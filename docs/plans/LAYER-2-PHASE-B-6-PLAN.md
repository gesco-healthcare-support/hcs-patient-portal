# Layer 2 Phase B-6 -- Test Coverage

**Parent:** [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md)
**Active child:** TBD -- draft Wave-2 follow-up plan at `docs/plans/YYYY-MM-DD-phase-b6-wave2.md` in the next plan-mode session (DoctorAvailability + Appointment full CRUD + IdentityUser flows; scope summary below).

## Status summary

Expand SonarCloud overall coverage from ~7% baseline to >= 60%, new-code coverage >= 80%, Quality Gate green on new code. Three tiers organised by entity priority. **Tier 1 COMPLETE 2026-04-24** (PR-0 #99, PR-1A #101, PR-1B #102, PR-1C #104, PR-1D #129, PR-1E #135 plus sub-PRs #103, #115). **Tier 2 COMPLETE 2026-04-24** -- all 4 PRs MERGED (PR-2A #141, PR-2B #142, PR-2C #143, PR-2D #144). **Tier 3 COMPLETE 2026-04-27** -- all 5 PRs MERGED (PR-3A #146, PR-3B #147, PR-3C #149, PR-3D #152, PR-3E #154). Per-tier entity coverage is now done; B-6 closure (60% overall) needs Wave-2 follow-up.

Coverage trajectory: 7.3% baseline (verified `main@ab7461ad2c` on 2026-04-20) -> 19.6% after PR-1D (#129, measured 2026-04-24 pre-PR-1E) -> 22.3% after PR-1E (#135, measured 2026-04-24 pre-Tier-2) -> 27.2% after PR-2D (#144, measured 2026-04-24 post-Tier-2) -> **35.2% after PR-3E (#154, measured 2026-04-27 post-Tier-3 via SonarCloud API on `main`); new-code coverage 42.13%**. Tier-3 target was 30-37%; landed at 35.2%, squarely in band (+8.0 pts from Tier-2 exit). Full B-6 target: >= 60% overall, >= 80% new-code, Quality Gate green on new code -- Wave-2 follow-up (DoctorAvailability + Appointment full CRUD + IdentityUser flows + AppointmentManager rules) is the next atomic unit.

## Sub-items

| Tier | Scope | Plan | Status |
|---|---|---|---|
| 1 | 5 critical entities (Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys) + PR-0 shared test infra | [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) | **MERGED 2026-04-24** (PR-0 #99, PR-1A #101, PR-1B #102, PR-1C #104, PR-1D #129, PR-1E #135 + sub-PRs #103, #115) |
| 2 | 3 secondary entities (AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails) + Wave-2 seed infra | [2026-04-24-phase-b6-tier2.md](./2026-04-24-phase-b6-tier2.md) | **MERGED 2026-04-24** (PR-2A #141, PR-2B #142, PR-2C #143, PR-2D #144) |
| 3 | 5 host-only lookup entities (States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices) | [2026-04-25-phase-b6-tier3.md](./2026-04-25-phase-b6-tier3.md) | **MERGED 2026-04-27** (PR-3A #146, PR-3B #147, PR-3C #149, PR-3D #152, PR-3E #154) |

## Currently active

None -- Tier 3 just closed. The next atomic unit is **drafting the Wave-2 follow-up plan** (`docs/plans/YYYY-MM-DD-phase-b6-wave2.md`) in a fresh plan-mode session. Tier 3 lifted overall coverage to 35.2% which is below the 60% B-6 target; Wave-2 closes the gap by adding full CRUD coverage for the two highest-LOC entities (DoctorAvailability, Appointment) plus the IdentityUser-dependent flows that were called out as a stretch tier in the kickoff.

## Upcoming queue

1. **Wave-2 follow-up planning** -- next atomic unit. Scope: DoctorAvailability full CRUD + Appointment full CRUD (both Wave-2 seeded since Tier-2 PR-2A; their AppService coverage is the remaining ncloc-heavy gap) + Patient.GetOrCreatePatientForAppointmentBookingAsync + AppointmentManager rules + ExternalSignup flow. Stretch-tier scope from `PHASE-B6-TEST-COVERAGE-KICKOFF.md` section 12.4. Decision points include: split-by-entity-PR vs single Wave-2 PR; production-code constraint (likely same test-data-only as Tier-2/3); IdentityUser-flow infra (ABP test-Identity infrastructure may need extension).
2. B-6 closure -- verify coverage targets (>= 60% overall, >= 80% new-code, Quality Gate green on new code); flip Quality Gate from informational to required.

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

2026-04-27 -- Tier 3 MERGED (PR-3E #154 closed the set); active-child pointer advanced to Wave-2 follow-up planning. Tier-3 added ~43 new live Facts + 2 Skip Facts across 5 host-only lookup entities; SonarCloud overall coverage 27.2% -> 35.2% (+8.0 pts), new-code coverage 42.13%.
