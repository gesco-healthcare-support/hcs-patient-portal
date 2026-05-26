---
title: Demo data integrity verification
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Demo data integrity (snapshot 2026-05-25 23:46 PT)

Read-only SQL check of demo state on `CaseEvaluation` DB.

## Expected vs Actual

| Entity | Expected | Actual | Verdict |
|---|---|---|---|
| SaasTenants | 1 (Falkinstein) | 1 | OK |
| SaasTenantConnectionStrings | 0 (shared DB) | 0 | OK |
| @gesco.com users | 4 | 4 | OK |
| AppointmentTypes | 6 | 6 | OK |
| Locations | 2 | 2 | OK |
| DoctorAvailabilities | 252 | 252 | OK |
| Appointments | 3 (A00001/2/3) | 3 | OK |
| AppointmentDocuments | 1 | 1 | OK |
| AppointmentPackets total | 4 | 4 | OK |
| AppointmentPackets active | 2 | 2 | OK |
| AppInvitations | 1 (defatty1 pending) | 1 | OK |
| Patients | 2 | 2 | OK |
| HangFire.Job total | >=30 | 55 | OK |
| HangFire.Job Succeeded | all | 55 | OK |
| HangFire.Job Failed | 0 | 0 | OK |
| HangFire.Server running | 1 | 1 | OK |

## Tenant ID for reference

- Falkinstein: 89BCD46B-...

## Verdict

**Ready.** Every demo-script expected count is present and zero
Hangfire jobs are failed. The 23:29 log entry "Login failed ...
database 'Falkinstein'" is harmless: the Falkinstein tenant has no
`SaasTenantConnectionStrings` row, so ABP defaults to the shared DB
after a single transient miss.
