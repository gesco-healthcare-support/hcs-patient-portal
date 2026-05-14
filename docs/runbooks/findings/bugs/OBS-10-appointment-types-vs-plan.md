---
id: OBS-10
title: Seeded appointment types differ from the v2 plan's expectation (AME/QME/Re-Eval/Consultation)
severity: observation
found: 2026-05-14
flow: data-seeding
---

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
