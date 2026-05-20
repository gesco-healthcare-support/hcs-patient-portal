---
id: BUG-024
title: Reject-appointment endpoint accepts empty rejectionNotes; UI requires it, server does not
severity: medium
status: fixed
fixed: 2026-05-19
fixed-on: feat/replicate-old-app
found: 2026-05-14 hardening R2.15
flow: appointment-rejection
component: src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/RejectAppointmentInput.cs + CaseEvaluationHttpApiHostModule.cs + AppointmentManager.cs
---

> **Fixed 2026-05-19**. The DTO field was named `Reason` (not
> `RejectionNotes` -- likely renamed between the bug-filing and now)
> and it was nullable with only `[StringLength(2000)]`. Applied:
>
> ```csharp
> [Required]
> [StringLength(500, MinimumLength = 5)]
> public string Reason { get; set; } = null!;
> ```
>
> 500 mirrors the UI's 0/500 counter; 5-char minimum keeps drive-by
> submissions like "x" out of the audit trail.
>
> **Same commit:** the `AppointmentInvalidTransition` 403→400 mapping
> flagged at the bottom of this doc landed in
> `CaseEvaluationHttpApiHostModule.cs` (now mapped to
> `HttpStatusCode.BadRequest`) and the throw site in
> `AppointmentManager.cs:206` now uses the new
> `CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition`
> const instead of the raw string literal.
>
> All three cases verified live via `fetch` from the logged-in SPA
> (token read from `localStorage.access_token`):
> - empty `reason`: HTTP 400, `"The Reason field is required."` +
>   `"min length of 5 and max length of 500"`
> - `reason: "no"`: HTTP 400, `"min length of 5 and max length of 500"`
> - reject on an already-Approved appointment with a valid reason:
>   HTTP 400, code `CaseEvaluation:AppointmentInvalidTransition`,
>   data `{from: 2, trigger: 2}`

# BUG-024 - Reject endpoint has no server-side validation on rejectionNotes

## Symptom
R2 hardening test: as Clinic Staff (SoftwareOne), POST to `/api/app/appointments/{id}/reject` with body `{ "rejectionNotes": "" }`.

Expected: HTTP 400 with validation error "Reason for rejection is required" (matching the UI modal which has the field marked `*` Required).

Observed: **HTTP 200**, appointment A00009 transitioned to Rejected (AppointmentStatus=3) with empty `RejectionNotes`. Verified in DB.

## Root cause hypothesis
The `RejectAppointmentInputDto` (or whatever the input shape is) does not have `[Required]` or a string-length-min annotation on `RejectionNotes`. The UI render template applies the `required` attribute on the textarea, so happy-path callers can't submit empty. But a direct API caller (e.g., automation, malicious client) bypasses that.

## Fix
In the reject input DTO:
```csharp
[Required]
[StringLength(500, MinimumLength = 5)]
public string RejectionNotes { get; set; } = null!;
```

The 500-char cap should match the UI's "0 / 500" character counter. A 5-char minimum prevents `"x"` -class submissions while not being arbitrary.

## Functional impact
Medium. An empty rejection reason creates an audit-trail gap: appointment is rejected but no reason recorded. Disputes between Clinic Staff and patients become hard to resolve.

Server data integrity: low - the rejection still records `RejectedById` + `AppointmentApproveDate` is null, so the rejection is attributable. Just no explanation.

## Verified during this R2 run
A00009 (Software Three, AME, 6/18/2026) was rejected by SoftwareOne@evaluators.com with `RejectionNotes = ''`. State changed Pending -> Rejected.

## Related R2 confirmations
While testing this bug:
- **Re-approving an already-approved appointment**: server correctly throws `CaseEvaluation:AppointmentInvalidTransition` with data `{ from: 2 (Approved), trigger: 1 (Approve) }`. State machine works.
- **Approving an already-rejected appointment**: same error with `from: 3 (Rejected)`. State machine works.
- Both transitions return HTTP **403** instead of 400 - same BUG-003 / BUG-023 pattern. Adding `AppointmentInvalidTransition` to the `AbpExceptionHttpStatusCodeOptions` map would be the same one-line fix.

## Related
- [[BUG-003]] (fixed PR #197) - the 403-vs-400 pattern for business exceptions.
- [[BUG-023]] - two more registration errors with the same status-mapping gap.
- [[OBS-19]] - internal staff have a separate CRUD modal that may also bypass these validations.
