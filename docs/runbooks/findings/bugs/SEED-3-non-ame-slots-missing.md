---
id: SEED-3
title: Only AME has seeded doctor-availability slots in the June test window; QME/Deposition/Record Review/SMR have none
severity: seed-gap
status: open
found: 2026-05-14 hardening Phase 3.17
flow: booking-form-slot-fetch
component: SQL-seeded inline (per SEED-2 follow-up: DemoDoctorDataSeedContributor still missing)
---

# SEED-3 - Non-AME appointment types have no seeded slots in the test window

## Symptom
On `/appointments/add`:
- Type = "Agreed Medical Examination (AME)" + Location = Demo Clinic North -> picker shows June dates enabled.
- Type = "Qualified Medical Examination (QME)" + same Location -> picker shows ALL June dates disabled; only July 1 onward enabled.

DB query confirms:
```sql
-- 42 slots seeded inline for the test stack, all bound to AME (AppointmentTypeId = a0a00002-0000-4000-9000-000000000003).
-- No slots for QME, Deposition, Panel QME, Record Review, Supplemental Medical Report.
```

`AppDoctorAvailabilities` schema is one-slot-to-one-AppointmentTypeId, so a slot must be seeded for every (date, time, type) tuple. The current ad-hoc SQL seed only covers AME.

## Impact on R1 hardening plan
Scenarios 3.17 (QME), 3.18 (Deposition), 3.19 (Record Review), 3.20 (Supplemental Medical Report) cannot exercise the happy path. The picker auto-navigates forward to whatever month has seeded slots, and once it lands in July, the server-side `BookingPolicyValidator` rejects most of those dates because OTHER has a 60-day horizon ([[BUG-022]] - separate question of whether the horizon math is right).

The four scenarios are **dropped from R1 until SEED-3 is resolved**.

## Recommended fix
Implement the SEED-2 follow-up (write `DemoDoctorDataSeedContributor`) and have it seed slots for every active AppointmentType x active Location combination over a 60-day rolling window. Avoid future-dating slots past the configured max horizon per-type so the picker stays consistent with the validator.

Slot generation per type per day:
- AME / AME-REVAL: 28 slots/day, 90-day window
- QME / Panel QME / PQME-REVAL: 28 slots/day, 60-day window
- Deposition: 28 slots/day, 60-day window
- Record Review: 28 slots/day, 60-day window
- Supplemental Medical Report: 28 slots/day, 60-day window

## To do
1. Decide whether SEED-3 lives in code (DemoDoctorDataSeedContributor) or stays SQL-inline for now.
2. If code: implement contributor, register in CaseEvaluationDataSeedContributor.
3. Re-run 3.17-3.20 after seed lands.

## Related
- [[SEED-2]] - "DemoDoctorDataSeedContributor missing" - same parent gap.
- [[BUG-022]] - max-horizon math; separate from seed but compounds the picker UX when seeds don't align with the horizon.
- Adrian's earlier observation (preserved across compaction): "AppDoctorAvailabilities schema binds slot to single AppointmentTypeId... no action to take, just making an observation." This SEED-3 is a downstream consequence of that schema choice.
