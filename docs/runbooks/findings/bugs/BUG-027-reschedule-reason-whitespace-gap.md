---
id: BUG-027
title: Reschedule-request endpoint accepts whitespace-only reason; returns HTTP 500 instead of 400
severity: low
status: fixed
fixed: 2026-05-20
fixed-on: feat/replicate-old-app
found: 2026-05-20 (triage of `_remaining-from-old-audit-2026-05-15.md` section 2.D BUG-024-pattern verification)
flow: appointment-reschedule
component: src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.cs:88-91
related:
  - BUG-024 (rejection-reason gap; fixed 2026-05-19)
  - BUG-023 (403-vs-400 mapping pattern)
---

> **Fixed 2026-05-20.** Added the missing `string.IsNullOrWhiteSpace(input.ReScheduleReason)`
> check at `AppointmentChangeRequestsAppService.cs:88`, mirroring the cancellation path's
> existing guard at line 60-63. The DTO's `[Required]` attribute continues to handle null
> and empty-string at the validation-interceptor layer; the new server-side check covers
> whitespace-only. No test added (matches BUG-024's pattern of "fix + manual verification");
> file once docker returns and live-test via SPA fetch with `{ "reScheduleReason": "   " }`
> to confirm HTTP 400 instead of HTTP 500.


# BUG-027 - Reschedule endpoint accepts whitespace-only ReScheduleReason

## Symptom

POST to `/api/app/appointment-change-requests/request-reschedule/{appointmentId}` with body:

```json
{
  "newDoctorAvailabilityId": "<some-guid>",
  "reScheduleReason": "     ",
  "isBeyondLimit": false
}
```

Expected: HTTP 400 with validation message "The ReScheduleReason field is required."

Observed: **HTTP 500** with `AbpException` from the entity constructor.

## Why the existing guards don't catch this

Three gates SHOULD reject empty reasons, but each has a hole:

1. **DTO `[Required]` attribute** at `RequestRescheduleDto.cs:26-28`. Catches null and empty string (`""`), but NOT whitespace-only (`"   "`). Standard .NET data-annotation behavior.
2. **DTO `[StringLength(ReasonMaxLength)]`** — only a max-length cap; no `MinimumLength`.
3. **AppService gate** at `AppointmentChangeRequestsAppService.cs:88-91`:
   ```csharp
   if (input == null)
   {
       throw new UserFriendlyException(L["The {0} field is required.", L["ReScheduleReason"]]);
   }
   ```
   Only checks `input == null`. Doesn't inspect `input.ReScheduleReason`.

Whitespace-only payload slips past all three. It reaches `AppointmentChangeRequestManager.SubmitRescheduleAsync` → entity constructor `AppointmentChangeRequest.cs:115` → `Check.NotNullOrWhiteSpace(reScheduleReason, ...)` → `AbpException`. The default exception handler maps `AbpException` to HTTP 500.

## Why this matters

Low severity — the UI never sends whitespace-only (clients trim before submit), so the production blast radius is near zero. But:

- Direct API callers (automation, security testers, malicious clients) get a HTTP 500. 500 leaks the implementation: "your code crashed", not "your input was bad."
- Audit-trail symmetry: BUG-024 fixed the equivalent path for appointment rejection. The reschedule path was the same shape and was missed in that PR.
- The cancellation path at `AppointmentChangeRequestsAppService.cs:60-63` ALREADY has the correct guard:
  ```csharp
  if (input == null || string.IsNullOrWhiteSpace(input.Reason))
  {
      throw new UserFriendlyException(L["The {0} field is required.", L["CancellationReason"]]);
  }
  ```
  Reschedule should mirror this.

## Fix

Single-line addition at `AppointmentChangeRequestsAppService.cs:88`:

```csharp
// Before
if (input == null)
{
    throw new UserFriendlyException(L["The {0} field is required.", L["ReScheduleReason"]]);
}

// After
if (input == null || string.IsNullOrWhiteSpace(input.ReScheduleReason))
{
    throw new UserFriendlyException(L["The {0} field is required.", L["ReScheduleReason"]]);
}
```

The localization key `ReScheduleReason` already exists; no new translation needed.

## Optional follow-up

Mirror BUG-024's DTO hardening: add `MinimumLength` to the DTO so a 1-character `"x"` is also rejected at the validation interceptor (HTTP 400) rather than passing through to the entity:

```csharp
// RequestRescheduleDto.cs
[Required]
[StringLength(AppointmentChangeRequestConsts.ReasonMaxLength, MinimumLength = 5)]
public string ReScheduleReason { get; set; } = null!;
```

This change is optional and matches BUG-024's resolution. If applied, also update the cancellation DTO (`RequestCancellationDto`) for symmetry.

## Test

Add to `test/HealthcareSupport.CaseEvaluation.Application.Tests/AppointmentChangeRequests/AppointmentChangeRequestsAppServiceTests.cs`:

```csharp
[Fact]
public async Task RequestRescheduleAsync_WhenReasonIsWhitespace_Throws400()
{
    var input = new RequestRescheduleDto
    {
        NewDoctorAvailabilityId = Guid.NewGuid(),
        ReScheduleReason = "     ",
        IsBeyondLimit = false
    };
    await Should.ThrowAsync<UserFriendlyException>(
        () => _appService.RequestRescheduleAsync(_appointmentId, input));
}
```

The test must run AFTER the audit-doc section 2.D is flipped to mark this verified.

## Audit doc cross-reference

`docs/parity/_remaining-from-old-audit-2026-05-15.md` section 2.D row "ReScheduleReason required (server-side)" — flip from **TO VERIFY** to **Partial -- whitespace-only slips through; see BUG-027**.
