---
id: BUG-009
title: BusinessException auto-localization gap produces "internal error" for BookingDateInsideLeadTime
severity: medium
status: needs-rehydration
found: 2026-05-13
flow: appointment-booking
component: Application/Appointments/AppointmentAppService.cs + Domain.Shared/Localization
---

# BUG-009 — Generic "internal error" message for BookingDateInsideLeadTime

## Severity
medium

## Status
**Needs rehydration.** Documented in earlier session compact summary; full repro to be added when re-encountered.

## What's known from earlier session
- Booking a date inside the doctor's lead-time window returns 403 with `Appointment.BookingDateInsideLeadTime` + `leadTimeDays=3` + generic *"An internal error"* message.
- ABP's `BusinessException` auto-localization isn't resolving the error code to a localized user-friendly message; falls through to the generic exception-handler banner.
- Workaround: pick a later date.

## To do
- Re-trigger the flow on canonical ports with a date inside lead-time.
- Capture the exact error code and message returned.
- Compare against the localization resource at `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` — confirm the key exists.

## Suspected root cause
The `BusinessException` is thrown with an error code but no `WithData("DefaultMessage", L["..."])` call. ABP's localization pipeline requires either:
- A pre-defined message in `en.json` keyed by the error code, OR
- An explicit `.WithData("DefaultMessage", ...)` on the throw.

Team workaround precedent: throw `UserFriendlyException` with a localized message and a code (the path taken for [[BUG-001]] / [[BUG-003]]). Same pattern likely fixes this.

## Related
- [[BUG-014]] uses a similar "BusinessException doesn't auto-localize" pattern. The fix for that ticket may also fix this one's localization story.
