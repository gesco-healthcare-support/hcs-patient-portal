---
id: SEED-2
title: DemoDoctorDataSeedContributor doesn't exist yet
severity: n/a
status: superseded
found: 2026-05-13
resolved: 2026-06-05
flow: data-seeding
component: Domain/DoctorManagement/* (new seed contributor needed)
---

# SEED-2 — DemoDoctor seed contributor missing

> **SUPERSEDED / WON'T-FIX-BY-SEED (IP3, 2026-06-05).** The user-facing symptom (an empty
> Doctors page) is resolved by IP3, which hides the Doctors nav item and keeps the Doctor
> entity dormant -- NOT by seeding doctors. The "hard blocker" framing below is incorrect:
> nothing operational reads a Doctor row. Booking dropdowns query `AppointmentType` and
> `Location` directly; slot generation and the date/slot picker gate off
> `DoctorAvailability` (which carries no `DoctorId` FK), so zero `AppDoctors` rows never block
> slot generation or downstream booking (see the `slot-gen-no-doctor-required` decision). No
> demo-doctor seed contributor will be written. Revisit only if a multi-doctor-per-tenant
> model returns to scope (see ADR-004). Historical analysis retained below for context.

## Severity
n/a (data seeding gap)

## Status
**Needs rehydration / not yet implemented.**

## What's known from earlier session
- NEW has no `DemoDoctorDataSeedContributor`.
- Prior session inline-SQL-inserted 1 Demo Doctor + 42 availabilities; these get wiped on every `docker compose down -v`.
- Adrian wants doctors seeded automatically so testing workflows can begin from a known state without manual SQL.

## OLD parity (CONFIRMED 2026-05-14)
Per `InternalUsersDataSeedContributor.cs:19-21`: *"Doctor is a non-user reference entity managed by Staff Supervisor; no Doctor user role exists."*

**Confirmed via OLD source inspection on 2026-05-14:**
`P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\doctors\` contains ONLY:
- `doctors.module.ts`
- `doctors.routing.ts`
- `doctors.service.ts`
- `domain/`
- `edit/`

There is NO `add/` subfolder. **OLD never creates doctors via the UI** — only edits them. So NEW's Doctor Management UI also lacks a "+ New Doctor" button (`angular/src/app/doctors/doctor/components/doctor.component.html` has no `<abp-page-toolbar-container>` block, unlike `location.component.html:2-14`). The abstract component `doctor.abstract.component.ts:39` does define `create()` but the template doesn't expose it. **This is intentional parity, not a UI bug.**

The implication: doctors MUST come from a seed contributor at DB bootstrap, since there is no UI path to create them. Without SEED-2 written, fresh DBs have zero doctors and the entire booking flow is blocked.

## Recommended fix
Write `DemoDoctorDataSeedContributor : IDataSeedContributor` that:
1. Per-tenant only (gated on `context.TenantId != null`).
2. Development-only (gated on `ASPNETCORE_ENVIRONMENT=Development`).
3. Idempotent (skip if any AppDoctors row already exists in this tenant).
4. Creates 1-2 demo doctors with:
   - First Name / Last Name / License number / Email
   - Preferred Locations (link to seeded Demo Clinic North/South)
   - Appointment Types (AME, QME, Re-Evaluation, Consultation)
   - Availabilities: 5-10 dates per type, 2-3 slots per date, spread across the next 30-60 days

## Blocker scope
SEED-2 is now a hard blocker for the multi-user-workflow plan. Until the seed contributor lands:
- Prep 2 (Doctor Management UI walk) cannot complete via UI (no Add Doctor button by design).
- Workflow B (Patient books) cannot proceed (no doctor availabilities → empty date picker).
- Workflows C/D/etc are downstream of Workflow B.

## To do (for the fix session)
- Confirm with Adrian which appointment types and how many doctors to seed.
- Write `DemoDoctorDataSeedContributor : IDataSeedContributor` per the recommended fix above.
- Update `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` Part 4 with the seeded-doctor expectation.
