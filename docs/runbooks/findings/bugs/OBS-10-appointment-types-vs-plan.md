---
id: OBS-10
title: Seeded appointment types differ from the v2 plan's expectation (AME/QME/Re-Eval/Consultation)
severity: observation
status: documented
found: 2026-05-14
resolved: 2026-05-22
flow: data-seeding
---

> **Resolution 2026-05-22 (OLD parity verified):**
>
> OLD app does **not** ship a fixed appointment-type list. `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\AppointmentType.cs:14-22` shows the entity uses `DatabaseGeneratedOption.Identity` (auto-increment int PK), and OLD's Angular `appointment-types/add` + `appointment-types/edit` components provide an admin UI to manage them. There is no hardcoded enum or seed migration anywhere in `PatientAppointment.Infrastructure`. Each OLD tenant's list is whatever the admin entered.
>
> So the v2 plan's "AME / QME / Re-Evaluation / Consultation" list is **not** derived from OLD -- it was test-scenario shorthand. NEW's 6-type host-scoped seed (`src/HealthcareSupport.CaseEvaluation.Domain/AppointmentTypes/AppointmentTypeDataSeedContributor.cs:44-64`: QME, PanelQME, AME, RecordReview, Deposition, SupplementalMedicalReport) is a deliberate refinement that bakes in the canonical California workers'-comp medical-legal exam types instead of leaving them tenant-configurable.
>
> Adjacent OLD-only concept worth knowing about: `AppointmentType.cs:36-37` has a nullable `ReEvalId` FK back to the same table -- every type can point to its "re-evaluation" counterpart. So in OLD, **Re-Evaluation is a *relationship between types*, not a separate type**. Whether NEW models that relationship is a separate question, surface as a follow-up only if a Re-Eval workflow becomes a Phase 1 requirement.
>
> **No code/seed action taken or recommended.** Decision on whether to seed or modify the appointment-type list is owned by Adrian.

# OBS-10 — Appointment types: seeded vs plan

## Plan expectation
`docs/plans/2026-05-14-multi-user-workflows.md` Prep 2 lists *"AME / QME / Re-Evaluation / Consultation"* as the four appointment types to seed slots for.

## Actually seeded (Falkinstein tenant, fresh DB after `docker compose down -v && up`)
6 types in `AppAppointmentTypes`:
- Agreed Medical Examination (AME)
- Deposition
- Panel QME
- Qualified Medical Examination (QME)
- Record Review
- Supplemental Medical Report

**Missing: Re-Evaluation, Consultation.** Likely renamed or OLD-app types that didn't carry forward.

## Action this session
Substituted AME (June) + QME (July) for the workflow tests. 732 slots total via the Set Availability Slot UI.

## OLD parity check (to do)
Check `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentTypes` (or equivalent) for the canonical type list. If OLD has Re-Evaluation + Consultation, NEW's seed is missing parity rows.
