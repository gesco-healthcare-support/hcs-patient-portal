---
id: BUG-022
title: BookingDatePastMaxHorizon thrown for slot well within configured horizon
severity: medium
status: fixed
last-replayed: 2026-05-21
found: 2026-05-14 hardening Phase 3.17
flow: booking-policy
component: src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentBookingValidators.cs + BookingPolicyValidator.cs
---

> **2026-05-21 replay outcome: fixed.** Tested both sides of the
> 60-day QME horizon boundary against current code:
>
> Setup:
> - Tenant: Falkinstein. `AppSystemParameters.AppointmentMaxTimePQME = 60`.
> - Today: 2026-05-21. QME horizon ends 2026-07-20.
> - Seeded slot A at 2026-07-15 (55d ahead, **within** horizon).
> - Seeded slot B at 2026-07-25 (65d ahead, **past** horizon).
>
> Booking POST `/api/app/appointments` against the QME type:
>
> | Slot | Status | Body |
> |---|---|---|
> | 55d (within) | **200** | A00006 created |
> | 65d (past) | 403 | `code: BookingDatePastMaxHorizon`, `data.maxTimeDays: 60` |
>
> The validator (`AppointmentBookingValidators.IsSlotWithinMaxTime`
> + `ResolveMaxTimeDaysForType`) correctly distinguishes within-vs-past.
> The original symptom (rejection of within-horizon slots) does not
> reproduce on the current code. Closing as fixed.
>
> Note: the rejection returns 403 (not 400) -- this is the broader
> [[BUG-023]] pattern (business-rule rejections using 403 status code).
> Tracked separately under BUG-023; not a BUG-022 issue.
>
> Side observation: the within-horizon 55d slot booking by stafsuper1
> returned `appointmentStatus: 2` with `appointmentApproveDate: null`
> -- third instance of [[BUG-030]] confirmed (internal-staff auto-
> approve missing ApproveDate). Not new evidence; BUG-030 already
> open.

# BUG-022 - BookingPolicyValidator rejects dates that are within the configured horizon

## Note 2026-05-14 (Adrian)
The **rule itself** is intentional - OLD parity, keep it. This ticket is now specifically about whether the rule's arithmetic correctly admits dates that are within the configured horizon. The math below suggests it doesn't, but my analysis could be wrong; revisit when re-running with an instrumented build.

## Symptom
- Today (server UTC): 2026-05-15 (verified via `docker exec main-api-1 date` -> `Fri May 15 00:31:01 UTC 2026`).
- Tenant SystemParameters: `AppointmentMaxTimeAME=90, AppointmentMaxTimePQME=60, AppointmentMaxTimeOTHER=60`.
- Appointment type chosen: "Qualified Medical Examination (QME)" -> uppercase "QUALIFIED MEDICAL EXAMINATION (QME)".
  - Contains "AME"? NO (EXAMINATION has A-M-I, not A-M-E).
  - Contains "PQME"? NO.
  - Routes to OTHER = 60.
- Slot date selected: 2026-06-30. Days from server today: 46. Configured max: 60. 46 < 60.
- Expected: `IsSlotWithinMaxTime` returns `true`, no exception.
- Observed: POST `/api/app/appointments` -> **HTTP 403** with body `{"error":{"code":"CaseEvaluation:Appointment.BookingDatePastMaxHorizon","data":{"maxTimeDays":60}}}`.

## Why the response shape is wrong
1. **maxTimeDays = 60 indicates the validator routed to `AppointmentMaxTimeOTHER` (=60) or `AppointmentMaxTimePQME` (=60).** Reading `ResolveMaxTimeDaysForType`:
   ```csharp
   if (name.Contains("AME")) {
       if (!name.StartsWith("PQME")) return systemParameter.AppointmentMaxTimeAME;
   }
   if (name.Contains("PQME")) return systemParameter.AppointmentMaxTimePQME;
   return systemParameter.AppointmentMaxTimeOTHER;
   ```
   For "QUALIFIED MEDICAL EXAMINATION (QME)" the substring "AME" is present (inside "EXAMINATION") and "PQME" is not, so the code should return **AME (90)** -- not 60.
2. **Even with the wrong route (60 days):** today (May 15) + 60 = July 14. June 30 < July 14, so `IsSlotWithinMaxTime` returns `true` and the validator should NOT throw `PastMaxHorizon`.

Either the running container is stale relative to the source on disk, OR there is a second validator path overriding this one.

## Repro
1. Log in as Patient (SoftwareThree).
2. Navigate to `/appointments/add`.
3. Type = QME, Location = Demo Clinic North, Date = 2026-06-30, Time = 10:00 AM.
4. Fill remaining sections, submit.
5. Network: POST `/api/app/appointments` returns 403 with the body above.

## Counter-evidence (proves the rule is mis-firing, not a config issue)
- A00013 (booked 2026-05-14 LA time, slot 2026-06-25) by SoftwareFive: **succeeded**. 41 days from today (UTC May 15).
- A00014 (slot 2026-06-28) by SoftwareSix: **succeeded**. 44 days.
- 3.17 attempt (slot 2026-06-30): **failed**. 46 days.

So the threshold is somewhere between 44 and 46 effective days for QME. Configured max is 60. Off-by-mystery.

## Hypothesis (3 in priority order)
1. **Stale build**: container is running an older compiled validator that hardcodes a smaller horizon. Confirm by `docker exec main-api-1 ls -la /app | grep dll` modification time, OR force rebuild.
2. **Different validator chain**: there is another booking-policy gate (e.g., per-doctor or per-appointment-type max horizon) that uses a different limit. Search the codebase for a second `PastMaxHorizon` throw site.
3. **`ResolveMaxTimeDaysForType` route mismatch + arithmetic bug**: the `Contains("AME")` short-circuits to AME=90, but elsewhere the actual computation uses 60 from a separately-loaded source.

## To do
1. `docker compose restart api` and retry the same payload; if it now passes, hypothesis 1 confirmed -> rebuild fully (`docker compose up -d --build api`).
2. Grep `src/` for the second `PastMaxHorizon` throw site if exists.
3. Add a log/debug breakpoint into `ResolveMaxTimeDaysForType` to confirm the actual maxTimeDays it returns for QME on the running build.

## Functional impact
Medium: hard-blocks legitimate bookings 45+ days out for some appointment types. Workaround: book within ~40 days.

## Related
- [[BUG-009]] - earlier "BookingDateInsideLeadTime" misleading-error issue (sibling validator path).
- [[OBS-15]] - cumulative-injury radio persistence (different booking-form layer).
