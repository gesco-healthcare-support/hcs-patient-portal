---
id: AF1
title: Reduce appointment types to AME, IME, PQME and flatten the booking horizon to 60 days
type: enhancement
components: [angular/src/app/appointments/sections/appointment-add-schedule.component.html, angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointment-types/, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/AppointmentTypeDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Domain/Data/CaseEvaluationSeedIds.cs, src/HealthcareSupport.CaseEvaluation.Domain/Locations/LocationDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentBookingValidators.cs, src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs]
related_known_bugs: [OBS-10, SEED-3, SEED-1]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Offer exactly three appointment types -- AME, IME, PQME -- and remove all others for now.
UI labels exactly: "Agreed Medical Examination (AME)", "Independent Medical Examination
(IME)", "Panel Qualified Medical Examination (PQME)". Internal code keys/abbreviations stay
AME/IME/PQME. IME is brand new; "Panel QME" is renamed to PQME; QME, Record Review,
Deposition, and Supplemental Medical Report are removed. Booking horizon becomes a flat
60 days from today for all types, replacing the name-substring horizon router.

## Current behavior (from investigation)
- Six types are seeded host-wide (skips when `context.TenantId != null`) in
  `AppointmentTypeDataSeedContributor.cs:44-64`: QME, Panel QME, AME, Record Review,
  Deposition, Supplemental Medical Report. Seed is idempotent upsert-by-ID
  (`AppointmentTypeDataSeedContributor.cs:34-40`).
- Stable seed GUIDs in `CaseEvaluationSeedIds.cs:20-29` (Qme, PanelQme, Ame, RecordReview,
  Deposition, SupplementalMedicalReport).
- `GetAppointmentTypeLookupAsync` (`AppointmentsAppService.cs:508-521`) returns every
  `AppointmentType` row matching the optional name filter -- no subset/allow-list. The
  booking-form dropdown (`appointment-add-schedule.component.html:16-22`, server-driven via
  `app-lookup-select`) renders whatever the lookup returns; the loader is
  `appointment-add.component.ts:315-327`.
- Admins can manage types through the generic ABP Suite CRUD at
  `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts`
  (route `appointment-type-routes.ts`, permission-guarded).
- Booking horizon is a name-substring router: `AppointmentBookingValidators.cs:62-88`
  (`ResolveMaxTimeDaysForType`) uppercases the type name and routes -- `name.Contains("AME")`
  and not StartsWith("PQME") -> `AppointmentMaxTimeAME`; `name.Contains("PQME")` ->
  `AppointmentMaxTimePQME`; everything else -> `AppointmentMaxTimeOTHER`. Today "Panel QME"
  has no "PQME" substring, so it falls to OTHER; renaming it to "PQME" silently flips it
  into the PQME bucket.
- Inbound seed FK references to types:
  - `LocationDataSeedContributor.cs:42,55` default both demo clinics to `Qme` (a REMOVED
    type) -- this is the breaking reference if QME is hard-deleted.
  - `JointDeclarationAutoCancelJob.cs:115` references `Ame` (a KEPT type) -- unaffected.

## Relevant code locations
- Seed: `AppointmentTypeDataSeedContributor.cs:44-64`; GUIDs `CaseEvaluationSeedIds.cs:20-29`.
- Location default FK: `LocationDataSeedContributor.cs:42,55` (must re-point off Qme).
- Lookup / allow-list: `AppointmentsAppService.cs:508-521`.
- Horizon router: `AppointmentBookingValidators.cs:62-88` (delete/replace).
- Booking-form dropdown: `appointment-add-schedule.component.html:16-22`;
  `appointment-add.component.ts:315-327`.
- Admin CRUD: `angular/src/app/appointment-types/appointment-type/components/appointment-type.component.ts`
  and `appointment-type-routes.ts`.
- Inbound DB FKs to handle for removed types (beyond seed): `Location.AppointmentTypeId`,
  `DoctorAvailability` M2M (`DoctorAppointmentType`), `Appointment.AppointmentTypeId`,
  plus seeded slots (SEED-3, keyed per date,time,type).

## Phase 3 cross-reference
- SEED-3 -- slots are seeded per (date, time, type); removing four types orphans/strands
  seeded slots that point at them. Fix the slot seed in the SAME change so removed types
  leave no bookable slots and the new IME type gets slots.
- OBS-10 -- documents the 6-type seed as a deliberate refinement (OLD had no fixed list) and
  that the list decision is owned by Adrian. Update/close OBS-10 to reflect the final three.
- SEED-1 -- precedent for trimming seed contributors (removed auto-seeded demo users); reuse
  that pattern for the type-seed trim.

## Research findings
- Internal patterns / prior art:
  - Seed is upsert-by-ID and "continue if exists" -- it never deletes. Simply dropping the
    four tuples from `Seeds` leaves existing rows in the DB on already-seeded environments;
    removal needs an explicit data step (migration or seed-side delete), not just editing the
    array (`AppointmentTypeDataSeedContributor.cs:32-41`).
  - Slot/type matching in the booking gate keys on `AppointmentType` rows; the comment on the
    horizon router (`AppointmentBookingValidators.cs:56-61,71-74`) confirms it is OLD-parity
    behavior, not a hard requirement -- safe to retire under the locked decision.
  - The lookup endpoint has no allow-list today, so the dropdown is purely seed-driven:
    trimming the seed is sufficient to trim the dropdown (`AppointmentsAppService.cs:508-521`).
- External docs (ABP / Angular / EF Core): EF Core code-first migration is the safe vehicle
  for the data delete + FK re-point (the seed contributor cannot delete). Standard ABP data
  seeding remains responsible for inserting the new IME row and renaming Panel QME -> PQME.

## Approaches considered (with tradeoffs)
1. Hard-delete the four removed type rows + re-point inbound FKs (CHOSEN). Cleanest demo
   surface: no hidden/orphan types, dropdown trims naturally, horizon logic simplifies.
   Cost: a migration to delete rows and fix `Location` defaults + seeded slots; must verify
   no live `Appointment` rows reference removed types in demo data.
2. Soft-hide removed types (keep rows, add an allow-list/IsActive filter on the lookup).
   Rejected: leaves dead data, requires net-new allow-list plumbing in
   `GetAppointmentTypeLookupAsync`, and the admin CRUD would still expose them. The locked
   decision explicitly says hard-delete from the seed.
3. Keep the name-substring horizon router and just add an "IME" bucket. Rejected: renaming
   "Panel QME" -> "PQME" already changes which bucket it lands in, and IME/QME fall to OTHER
   -- the substring approach is exactly what the rename breaks. A flat 60-day horizon removes
   the fragile name coupling entirely.

Note: renaming away from the "AME"/"PQME" name shapes is precisely what forces the router
rework; this is why the horizon change is bundled into AF1 rather than treated separately.

## Decision (locked 2026-06-03)
- Final three types only: AME (existing Ame row), IME (NEW row), PQME (renamed from the
  PanelQme row). UI labels exactly as specified; code keys AME/IME/PQME.
- Hard-delete QME, Record Review, Deposition, Supplemental Medical Report from the seed and
  the DB; handle inbound FKs (`Location.AppointmentTypeId`, `DoctorAvailability` M2M,
  `Appointment`) and seeded slots (SEED-3).
- Booking horizon: flat 60 days from today for ALL types -- replace
  `ResolveMaxTimeDaysForType` (`AppointmentBookingValidators.cs:62-88`). Keep any existing
  minimum lead-time.
- Conditional/type-keyed logic elsewhere (AF3/AF4 panel number) keys off the seed GUIDs, not
  name substrings.

## Implementation outline (no code)
1. Domain.Shared / seed IDs: add `Ime` GUID to `CaseEvaluationSeedIds.AppointmentTypes`
   (`CaseEvaluationSeedIds.cs:20-29`). Keep Ame and PanelQme constants; the four removed
   constants can stay as references for the migration delete, then be pruned.
2. Seed contributor (`AppointmentTypeDataSeedContributor.cs:44-64`): reduce `Seeds` to three
   tuples -- Ame (unchanged label), Ime (new), and PanelQme renamed to label "Panel Qualified
   Medical Examination (PQME)". Keep PanelQme's existing GUID for FK stability.
3. Location seed (`LocationDataSeedContributor.cs:42,55`): re-point both demo clinics' default
   `appointmentTypeId` off `Qme` to a surviving type (Ame or Ime) before the QME row is
   deleted.
4. Slot seed (SEED-3): regenerate seeded slots so removed types get none and IME gets slots
   (coordinate with SEED-3 owner; do not propose appointment-slot seed changes outside that).
5. MIGRATION (flag): EF Core migration to (a) insert/rename type rows if not handled by seed
   on existing envs, (b) re-point or null inbound FKs from removed types
   (`Location.AppointmentTypeId`, `DoctorAppointmentType` M2M, any `Appointment` rows in demo
   data), then (c) delete the four removed `AppointmentType` rows. The seed contributor
   cannot delete -- this delete MUST live in a migration or explicit data step.
6. Horizon (server enforcement): replace `ResolveMaxTimeDaysForType`
   (`AppointmentBookingValidators.cs:62-88`) with a flat 60-day max from today for all types;
   preserve the minimum lead-time check. Remove the now-unused `AppointmentMaxTimeAME/PQME/
   OTHER` routing (confirm whether the SystemParameter fields are still read elsewhere before
   removing them).
7. Lookup (no change needed for trimming): `GetAppointmentTypeLookupAsync`
   (`AppointmentsAppService.cs:508-521`) stays as-is; the trimmed seed yields the trimmed
   dropdown. Optionally add a defensive allow-list later if admin CRUD re-introduces types.
8. Admin CRUD (decision deferred to residual): leave the ABP Suite CRUD as-is for now; it can
   create arbitrary types but is permission-guarded and not a presented persona.
9. Angular: no template change required for the dropdown (server-driven). Verify the new IME
   label renders and the four removed types disappear after reseed. PROXY REGEN: not required
   for the type-set change (no DTO/contract shape change); only regen if DTOs change in AF3/AF4.
10. Server-vs-UI enforcement: the type SET and the 60-day horizon are SERVER-enforced (seed +
    booking validator). The dropdown is the UI mirror and is automatically consistent because
    it is server-driven.

## Dependencies
- Blocks AF3 (panel-number blocked for AME/IME) and AF4 (panel-number required for PQME):
  both must key off the final seed GUIDs decided here (PanelQme GUID = PQME identity; new
  Ime GUID).
- Coordinates with SEED-3 (slot seed) -- must land together so removed types have no slots
  and IME has slots.
- Touches OBS-10 (close/update the 6-type-seed observation).

## Residual open questions
- IME row: confirm IME is a brand-new row (new GUID) rather than a relabel of an existing
  removed type. Locked decision says "IME is NEW", so a new GUID is the default.
- Admin CRUD lockdown: should `/appointment-types` be restricted to the three for now, or
  left open (permission-guarded)? Minor; default is leave open since it is break-glass only.
- SystemParameter horizon fields (`AppointmentMaxTimeAME/PQME/OTHER`): confirm no other reader
  before deleting the columns vs. leaving them dormant. Default: leave dormant, stop reading.
