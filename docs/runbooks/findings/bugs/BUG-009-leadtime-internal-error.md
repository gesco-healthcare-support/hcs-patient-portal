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

> **Verification 2026-05-22: OPEN (confidence 75%). Root cause in this doc is wrong -- see below.**
>
> The original hypothesis ("BusinessException auto-localization gap") is **incorrect**. The localization side is wired correctly:
> - Constant exists: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs:261` -- `AppointmentBookingDateInsideLeadTime = "CaseEvaluation:Appointment.BookingDateInsideLeadTime"`
> - Localization key resolves: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json:229` -- `"The selected appointment date is too soon. Choose a date at least {0} day(s) from today."`
> - Throw site is correct shape: `BookingPolicyValidator.cs:68-69` throws `new BusinessException(...AppointmentBookingDateInsideLeadTime).WithData("leadTimeDays", result.ThresholdDays)`
>
> **The actual gap is in the HTTP layer.** `CaseEvaluationHttpApiHostModule.cs:152-207` maps 11 error codes to non-default HTTP status codes, but **neither `AppointmentBookingDateInsideLeadTime` nor its sibling `AppointmentBookingDatePastMaxHorizon` is mapped.** Default ABP `BusinessException` -> HTTP 403, which the SPA renders as a generic "internal error" toast.
>
> This is the **same root cause** that BUG-022 (booking-horizon-rejects-within-range), BUG-024 (reject-accepts-empty-reason), and BUG-025 (document-upload-size-limit) already fixed: missing `options.Map(..., BadRequest)` entry in `AbpExceptionHttpStatusCodeOptions`.
>
> **Action: FIX, do not close.** Add two `options.Map(..., BadRequest)` lines for `AppointmentBookingDateInsideLeadTime` and `AppointmentBookingDatePastMaxHorizon` alongside the BUG-024 / OBS-23 entries. ~5-minute change. Update this doc's "Suspected root cause" -- the localization key is fine; the missing HTTP status mapping is the actual cause. Then live-verify by booking a date inside lead time and confirming HTTP 400 + localized message in the SPA toast.
>
> Cited files:
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` (lines 252-275)
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` (line 229)
> - `src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingPolicyValidator.cs` (lines 67-72)
> - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (lines 152-207 -- gap is here)

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
