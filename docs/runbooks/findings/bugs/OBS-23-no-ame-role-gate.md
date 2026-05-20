---
id: OBS-23
title: No server-side gate prevents non-attorney external users from requesting AME / AME-REVAL appointments
severity: medium (auth-policy gap; not data exfiltration)
status: open
found: 2026-05-20 (triage of `_remaining-from-old-audit-2026-05-15.md` AME-role-gate items: lines 112, 525, 845, 847)
flow: appointment-booking
component: src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs CreateAsync (line ~600+)
related:
  - OLD `RoleAppointmentType` permission table (P:\PatientPortalOld OLD-side maps Role -> allowed AppointmentTypeIds)
  - Project Overview §3.3 (matrix of which role can request which type)
  - Audit doc rows: line 112, 525, 845, 847
---

# OBS-23 - Patient / Claim Examiner can book AME appointments via direct API call

## Symptom

The UI restricts which appointment-type buttons external users see (Patients
don't see AME / AME-REVAL; only Applicant Attorneys + Defense Attorneys do).
But the server does NOT enforce this on the API. A direct POST to
`/api/app/appointments` with `appointmentTypeId` pointing at an AME-type
row succeeds for any logged-in external role.

## Why the existing guards don't catch this

OLD had a `RoleAppointmentType` table (`RoleId x AppointmentTypeId`) that
listed which roles could request which types. The booking flow enforced
the policy by joining against this table at validation time.

NEW does not have this table. The booking flow's role-related logic is
limited to:

- `BookingFlowRoles.IsInternalUserCaller` -- internal vs external split
  (decides Pending vs Approved status; does NOT restrict types)
- `BookingFlowRoles.ResolveClaimExaminerEmail` -- claim-examiner email
  auto-fill
- `BookingPolicyValidator` -- lead-time + max-time gates (no role
  awareness)
- `AppointmentBookingValidators` -- AME / AME-REVAL share the AME time
  horizon (name-substring match), but this is a window check, NOT a
  role gate

`grep -r "AppointmentType.*role\|role.*AppointmentType\|AME.*role"` on
the source returns only the data-seed contributor and the accessor
rules -- no booking-time enforcement.

## Why this matters

Severity: medium. Not a data leak; not auth bypass. But:

- A Patient could POST an AME booking through the API and the appointment
  would land at `Pending`. Staff would have to reject it manually.
- A Claim Examiner could POST an AME-REVAL booking through the API and the
  appointment would land at `Pending`. Same.
- Real users hitting this requires direct API knowledge + intent; the UI
  doesn't expose the buttons. Low likelihood of accidental abuse.
- A security tester or automation script would surface this immediately.
- The OLD `RoleAppointmentType` table was the canonical mechanism; NEW
  inherits the surface area without the policy.

## Decision needed

Three paths:

- **A** -- Build the gate. Small AppService check at `CreateAsync`: load
  the appointment-type name + the caller's roles; reject AME / AME-REVAL
  when caller is Patient or Claim Examiner. ~10 lines + a helper +
  3 tests. Closes the gap.

- **B** -- Defer to a future "role-type policy" feature. The OLD
  `RoleAppointmentType` table was N x N; a proper port would make
  the policy data-driven (admins configure which roles can request
  which types in a per-tenant settings table). Larger scope.

- **C** -- Accept the gap and document it. Staff manual-reject the rare
  malformed booking. Matches OLD's effective enforcement (the table
  existed but the test coverage on the join didn't, per audit).

**Recommendation:** **A**. The gate is small + closes a security policy
gap without committing to the larger policy-table feature. Code path
in `AppointmentsAppService.CreateAsync`:

```csharp
// Pseudo-code -- file the plan separately if Adrian chooses A.
if (IsExternalCaller())
{
    var appointmentType = await _appointmentTypeRepository.GetAsync(input.AppointmentTypeId);
    var isAmeType = appointmentType.Name.Contains("AME", StringComparison.OrdinalIgnoreCase);
    var roles = CurrentUser.Roles ?? Array.Empty<string>();
    var isAttorneyRole = roles.Any(r =>
        string.Equals(r, "Applicant Attorney", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(r, "Defense Attorney", StringComparison.OrdinalIgnoreCase));
    if (isAmeType && !isAttorneyRole)
    {
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.AppointmentAmeRequiresAttorneyRole);
    }
}
```

Plus the new error code, localization key, and HTTP 400 mapping.

## Audit doc cross-reference

`docs/parity/_remaining-from-old-audit-2026-05-15.md` rows:
- Line 112 (AME / AME-REVAL booking restricted to Attorneys)
- Line 525 (RoleAppointmentType permission table)
- Line 845 (Appointment Request AME -- AME role gate TO VERIFY)
- Line 847 (Appointment Request AME-REVAL -- AME role gate TO VERIFY)

All four should be flipped to **Open observation -- see OBS-23**.

## Test plan (when fix lands)

| # | Test | Acceptance |
|---|------|------------|
| 1 | `CreateAsync_AsPatient_AmeType_Throws` | BusinessException with `AppointmentAmeRequiresAttorneyRole`; HTTP 400. |
| 2 | `CreateAsync_AsClaimExaminer_AmeType_Throws` | Same. |
| 3 | `CreateAsync_AsApplicantAttorney_AmeType_Succeeds` | 200. |
| 4 | `CreateAsync_AsDefenseAttorney_AmeRevalType_Succeeds` | 200. |
| 5 | `CreateAsync_AsPatient_NonAmeType_Succeeds` | 200 -- PQME and OTHER types still allowed. |
| 6 | `CreateAsync_AsInternalStaff_AnyType_Succeeds` | Internal users bypass the gate (matches OLD UserType.InternalUser fast-path). |

## Verification

Manual probe via SPA fetch (token from localStorage):

```javascript
// Logged in as a Patient
fetch('/api/app/appointments', {
  method: 'POST',
  headers: {
    'Authorization': 'Bearer ' + localStorage.access_token,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    appointmentTypeId: '<AME-type-guid>',
    locationId: '<any-location-guid>',
    doctorAvailabilityId: '<any-slot-guid>',
    appointmentDate: '2026-06-01T10:00:00Z',
    patientId: '<own-patient-id>',
    identityUserId: '<own-user-id>',
    // ... required fields
  })
}).then(r => r.status); // Currently 200; expected 400 after fix.
```
