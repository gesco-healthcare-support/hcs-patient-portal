---
feature: availabilities-patient-chips
date: 2026-06-19
status: in-progress
base-branch: feat/frontend-rework
lane: Session A
backlog-item: 2
protocol: 2026-06-19-parallel-build-protocol.md
---

## Goal

Rework the internal Doctor Availabilities week view to show per-slot patient-name chips
(who is booked/reserved on each slot), backed by a new read-only endpoint -- no schema
change.

## Context

Backlog #2. The week grid (`internal-availabilities.component`) already shows per-day
available/booked/reserved counts computed FE-side, but patient names per slot are not in
the DTO. Session A's lane (migration-free); Session B is on #9 (the migration lane), so
this item adds NO EF migration -- no `DbContext` snapshot clash. Proxy regen is Session A's
lane.

Research (Explore map, 2026-06-19): `Appointment.DoctorAvailabilityId` links a slot to its
appointments; the bookable set excludes the 5 terminal statuses (Rejected,
CancelledNoBill/Late, RescheduledNoBill/Late), matching
`EfCoreAppointmentRepository.GetActiveCountForSlotAsync`. Patient name =
`Patient.FirstName` + `LastName`. Both entities are `IMultiTenant` (auto tenant filter).
The week view is internal-only (`[Authorize(DoctorAvailabilities.Default)]`), so showing
patient names to staff is HIPAA-allowed.

## Approach

Chosen: a new BULK read endpoint `GetSlotPatientNamesAsync(List<Guid> slotIds) ->
List<SlotPatientNamesDto>` (slotId + names), called once after `getList()` with the
visible slot ids (avoids N+1). The FE attaches the names to each grid slot and renders
chips reusing the existing `.lw-chips` / `.lw-chip` styles.

Rejected: (a) expanding `DoctorAvailabilityWithNavigationPropertiesDto` with patient names
-- couples the list query and makes it heavier for callers that don't need names; (b) a
per-slot endpoint -- N+1 calls across a 7-day grid.

## Tasks

- T1 (backend): add `GetSlotPatientNamesAsync(List<Guid> slotIds)` to
  `IDoctorAvailabilitiesAppService` + impl; query non-terminal Appointments whose
  `DoctorAvailabilityId` is in the set, hydrate `Patient.FirstName/LastName`, group by
  slot. New `SlotPatientNamesDto { SlotId, Names }`. `[Authorize(DoctorAvailabilities.Default)]`.
  - approach: test-after
  - acceptance: returns correct names per slot for seeded data; excludes terminal-status
    appointments; tenant-filtered; empty/extra slot ids handled.

- T2 (proxy): `abp generate-proxy -t ng -u http://localhost:44377`; commit only the
  doctor-availabilities `models.ts` + `doctor-availability.service.ts` + `generate-proxy.json`.
  - approach: code

- T3 (FE): after `getList()`, call the new endpoint with the visible slot ids; store a
  `slotId -> names` map; add `patientNames` to the grid-slot model (`avail-grid.util.ts`);
  render chips in `internal-availabilities.component.html` (`.av-slot` -> `.lw-chips`/`.lw-chip`,
  with a "+N" overflow when a slot has many). Available slots show none.
  - approach: test-after
  - acceptance: booked/reserved slots show the right patient-name chips; available slots
    show none; screenshot-verified.

- T4 (verify): live on Falkinstein as staff supervisor; screenshot the week grid with chips.

## Risk / Rollback

- Blast radius: additive read endpoint + FE chips; no migration, no mutation. Confined to
  `doctor-availabilities/*` + a proxy regen. No Session B file overlap (B is on
  appointments/attorneys for #9).
- Rollback: revert the commits; the endpoint is additive.

## Verification

Live on Falkinstein: open Doctor Availabilities (week view), confirm booked/reserved slots
show the correct patient-name chips and available slots show none; plus a backend test for
the grouping/terminal-exclusion query.

## Build status (2026-06-19)

DONE -- commit 0f904e7 (T1-T4). Verified end-to-end: GET-less bulk endpoint returns 200 and
"Daniel Harper" for his booked slots (2026-07-03 + 07-14, Demo Clinic North); the week grid
renders a "Daniel Harper" chip on the reserved 10:00 slot (Jul 3); available slots show no
chips. API + Angular both compile clean (0 errors). Note: the seeded booked slots are weeks
out, so the chips are only visible on those future weeks; the current week is all-available.

