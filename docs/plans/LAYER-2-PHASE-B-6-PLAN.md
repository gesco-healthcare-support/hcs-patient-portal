# Layer 2 Phase B-6 -- Test Coverage

**Parent:** [LAYER-2-PHASE-B-PLAN.md](./LAYER-2-PHASE-B-PLAN.md)
**Active child:** [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md)

## Status summary

Expand SonarCloud overall coverage from ~7% baseline to >= 60%, new-code coverage >= 80%, Quality Gate green on new code. Three tiers organised by entity priority. Tier 1 covers the 5 critical-path entities + shared test infrastructure; 4 of 6 PRs merged, PR-1D (#129) landed 2026-04-24, PR-1E (ApplicantAttorneys) in progress.

Coverage baseline: SonarCloud overall coverage 7.3% (verified against `main@ab7461ad2c` on 2026-04-20). Tier 1 target: ~25-30% per the Tier-1 plan. Full B-6 target: >= 60% overall, >= 80% new-code, Quality Gate green on new code.

## Sub-items

| Tier | Scope | Plan | Status |
|---|---|---|---|
| **1** | **5 critical entities (Appointments, Patients, DoctorAvailabilities, Locations, ApplicantAttorneys) + PR-0 shared test infra** | [2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) | **IN PROGRESS** (4 merged PRs + PR-1D #129 merged + PR-1E in flight) |
| 2 | 3 secondary entities (AppointmentAccessors, AppointmentApplicantAttorneys, AppointmentEmployerDetails) | drafted after Tier 1 merges + coverage delta measured | NOT STARTED |
| 3 | 5 host-only lookup entities (States, AppointmentTypes, AppointmentStatuses, AppointmentLanguages, WcabOffices) | drafted after Tier 2 merges | NOT STARTED |

## Currently active

[2026-04-20-phase-b6-tier1.md](./2026-04-20-phase-b6-tier1.md) -- T6 / PR-1E (ApplicantAttorneys) is the last Tier-1 PR.

## Upcoming queue

1. Tier 2 -- draft after Tier 1 merges and SonarCloud coverage delta is measured.
2. Tier 3 -- draft after Tier 2 merges.
3. B-6 closure -- verify coverage targets; flip Quality Gate on new-code from informational to required.

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

2026-04-24 -- initial creation (Tier 1 in flight; PR-1D #129 merged; PR-1E in progress).
