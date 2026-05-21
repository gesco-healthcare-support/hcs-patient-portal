---
status: draft
issue: ame-role-gate
owner: AdrianG
created: 2026-05-21
approach: tdd (booking-time validator + role check; pure logic) + code (wiring)
sequence: standalone bug fix; no upstream dependency
parity-audit: docs/parity/_remaining-from-old-audit-2026-05-15.md (lines 112, 525, 845, 847)
related-finding: docs/runbooks/findings/bugs/OBS-23-no-ame-role-gate.md
branch: fix/ame-role-gate (cut from feat/replicate-old-app)
---

# AME role gate (OBS-23)

## Goal

Block non-attorney external users (Patient, Claim Examiner) from
creating an AME or AME-REVAL appointment via the API. The Angular UI
already hides those buttons for non-attorney roles; this plan adds
the equivalent server-side gate so a direct API caller cannot bypass.

## OLD reference (binding)

`P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs`
lines ~640-645 query a `RoleAppointmentType` join table:

```csharp
isAuthorizedUserAppointmentType = AppointmentRequestUow
    .Repository<RoleAppointmentType>()
    .All()
    .Any(x => x.RoleId == currentUserRoleId
          && x.AppointmentTypeId == appointment.AppointmentTypeId);
```

If no join row exists, OLD throws "AppointmentCanNotBook". NEW does
not have a `RoleAppointmentType` table; the strict-parity port is a
hardcoded allow-list (AA + DA can book AME) since this is the only
role-type restriction OLD enforced for external users. Building the
full M:N policy table is deferred (see OBS-23 "Option B").

## Decisions locked

1. **Detect AME types by name substring** -- mirrors the existing
   `AppointmentBookingValidators.cs:75` pattern
   (`name.Contains("AME")`). Current seeded types are PQME, PQME-REVAL,
   AME, AME-REVAL; only AME and AME-REVAL match `Contains("AME")`.
   Panel QME and Qualified Medical Examination (QME) do NOT contain
   the contiguous substring `AME`.
2. **Gate runs for external callers only**. Internal staff
   (admin / Clinic Staff / Staff Supervisor / IT Admin / Doctor)
   continue to bypass via `BookingFlowRoles.IsInternalUserCaller`.
3. **Allowed external roles for AME** = Applicant Attorney +
   Defense Attorney (PARole + DARole in OLD). Patient + Claim
   Examiner blocked.
4. **Error code**: new constant
   `AppointmentAmeRequiresAttorneyRole` mapped to HTTP 400 via
   `CaseEvaluationHttpApiHostModule`.
5. **Localization key** added to en.json:
   `Appointment:AmeRequiresAttorneyRole`.
6. **No new permission constant**. The gate is on a role-type
   combination, not a permission grant; using a permission would
   require seeding a `CaseEvaluation.Appointments.CreateAme`
   permission and granting it to AA + DA roles in the seed
   contributor -- larger surface for the same outcome. Hardcoded
   allow-list is shipped today; a permission-driven version can
   replace it in a follow-up if the policy expands.

## Files touched

### 1. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`

Add the new const:

```csharp
/// <summary>
/// 2026-05-21 (OBS-23) -- raised by
/// <c>AppointmentsAppService.CreateAsync</c> when an external user
/// outside the Applicant Attorney + Defense Attorney roles attempts
/// to create an AME or AME-REVAL appointment.
/// </summary>
public const string AppointmentAmeRequiresAttorneyRole =
    "CaseEvaluation:Appointment.AmeRequiresAttorneyRole";
```

### 2. `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`

Map the new error code to HTTP 400. Mirror the existing block where
`AppointmentInvalidTransition` etc. are mapped.

### 3. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

Add the localization key. Keep the message generic so the same body
covers both AME and AME-REVAL:

```jsonc
"Appointment:AmeRequiresAttorneyRole":
  "Only Applicant Attorneys and Defense Attorneys can request an AME or AME-REVAL appointment. Please contact your attorney to schedule this evaluation."
```

### 4. `src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingFlowRoles.cs`

Add a sibling helper that determines whether a caller is in the
attorney roles. Pure, unit-testable. Sits beside
`IsInternalUserCaller` + `ResolveClaimExaminerEmail`:

```csharp
/// <summary>
/// 2026-05-21 (OBS-23) -- returns true when the caller holds an
/// attorney role (Applicant Attorney or Defense Attorney). AME +
/// AME-REVAL appointment requests are restricted to these roles
/// for external users, mirroring OLD's RoleAppointmentType join.
/// Internal-user callers bypass this gate via
/// <see cref="IsInternalUserCaller"/>.
/// </summary>
internal static bool IsAttorneyCaller(IEnumerable<string?>? callerRoles)
{
    if (callerRoles == null) return false;
    foreach (var role in callerRoles)
    {
        if (string.IsNullOrWhiteSpace(role)) continue;
        var trimmed = role.Trim();
        if (string.Equals(trimmed, "Applicant Attorney", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Defense Attorney", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }
    return false;
}

/// <summary>
/// 2026-05-21 (OBS-23) -- returns true when the appointment-type
/// name carries AME semantics (matches "AME" and "AME-REVAL" without
/// false-positives on Panel QME or Qualified Medical Examination,
/// per the substring match in <see cref="AppointmentBookingValidators"/>).
/// </summary>
internal static bool IsAmeAppointmentType(string? appointmentTypeName)
{
    if (string.IsNullOrWhiteSpace(appointmentTypeName)) return false;
    return appointmentTypeName.Contains("AME", StringComparison.OrdinalIgnoreCase);
}
```

### 5. `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`

Insert the gate inside `CreateAsync`, immediately AFTER the
appointment type is resolved (around line 629) and BEFORE the
existing `_bookingPolicyValidator.ValidateAsync` call. Keeping the
gate in CreateAsync (not UpdateAsync) mirrors OLD's behavior;
admins editing an existing appointment retain whatever type was
already on the row.

```csharp
// OBS-23 (2026-05-21) -- AME / AME-REVAL booking is restricted to
// attorneys (AA + DA) for external callers. Internal staff bypass
// this gate. Mirrors OLD's RoleAppointmentType join; NEW uses a
// hardcoded allow-list since the join table was not ported.
var callerRoles = CurrentUser.Roles ?? Array.Empty<string>();
if (!BookingFlowRoles.IsInternalUserCaller(callerRoles)
    && BookingFlowRoles.IsAmeAppointmentType(appointmentType.Name)
    && !BookingFlowRoles.IsAttorneyCaller(callerRoles))
{
    throw new BusinessException(
        CaseEvaluationDomainErrorCodes.AppointmentAmeRequiresAttorneyRole);
}
```

### 6. `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/BookingFlowRolesUnitTests.cs`

Add facts on the new helpers (pure, no DB needed):

| # | Test | Acceptance |
|---|------|------------|
| 1 | `IsAttorneyCaller_WhenApplicantAttorney_ReturnsTrue` | `["Applicant Attorney"]` -> true. |
| 2 | `IsAttorneyCaller_WhenDefenseAttorney_ReturnsTrue` | `["Defense Attorney"]` -> true. |
| 3 | `IsAttorneyCaller_WhenPatient_ReturnsFalse` | `["Patient"]` -> false. |
| 4 | `IsAttorneyCaller_WhenClaimExaminer_ReturnsFalse` | `["Claim Examiner"]` -> false. |
| 5 | `IsAttorneyCaller_WhenNull_ReturnsFalse` | `null` -> false. |
| 6 | `IsAmeAppointmentType_WhenAme_ReturnsTrue` | `"Agreed Medical Examination (AME)"` -> true. |
| 7 | `IsAmeAppointmentType_WhenAmeReval_ReturnsTrue` | `"AME-REVAL"` -> true. |
| 8 | `IsAmeAppointmentType_WhenPanelQme_ReturnsFalse` | `"Panel QME"` -> false. |
| 9 | `IsAmeAppointmentType_WhenQme_ReturnsFalse` | `"Qualified Medical Examination (QME)"` -> false. |

The AppService-level integration tests already test the booking
flow extensively; one additional integration test confirms the
gate fires correctly:

| # | Test | Acceptance |
|---|------|------------|
| 10 | `CreateAsync_AsPatient_AmeType_ThrowsAmeRequiresAttorney` | Seed AME type; caller is Patient; expect `BusinessException` with code `AppointmentAmeRequiresAttorneyRole`. |
| 11 | `CreateAsync_AsApplicantAttorney_AmeType_Succeeds` | Same setup; caller is AA; passes the gate. |
| 12 | `CreateAsync_AsPatient_NonAmeType_Succeeds` | Caller is Patient; type is QME; gate is skipped. |

Tests 10-12 require an integration test fixture. If
`AppointmentsAppServiceTests` does not already wire booking
end-to-end, ship tests 1-9 (pure unit tests) and verify 10-12 via
live SPA fetch against the running stack (mirrors the BUG-024
verification approach).

## Test plan

TDD on the BookingFlowRoles helpers. 9 pure unit tests.

Live verification against the replicate-old-app stack after the
fix lands:

1. Log in as patient1 (a Patient). POST to
   `/api/app/appointments` with `appointmentTypeId` =
   AME's GUID + a valid slot. Expect HTTP 400 with code
   `CaseEvaluation:Appointment.AmeRequiresAttorneyRole`.
2. POST to `/api/app/appointments` with `appointmentTypeId` =
   QME's GUID. Expect 200.
3. Log in as appatty1 (an AA). POST with AME type. Expect 200.

## Risk and rollback

**Blast radius:**
- One AppService method (`CreateAsync`) gains a 5-line gate.
- One new const + one new localization key + one HTTP mapping +
  two new helper methods.
- No DB schema change.

**Rollback:** revert the commit. The gate disappears; behavior
falls back to OLD's "any role can book any type" semantic.

**Risk: substring match on type name is brittle**. If a future
seeded type carries "AME" anywhere in its name (e.g., a fictitious
"Lame Duck Review" -- unlikely), it would be incorrectly gated.
Mitigation: the AppointmentType seed lives in the DbMigrator
seeder; adding a guard there to refuse "AME"-containing names for
non-attorney-restricted types is trivial if needed.

**Risk: role names hardcoded as strings**. A future rename of the
"Applicant Attorney" or "Defense Attorney" role would silently
break the gate. Mitigation: the seeded role names live in
`ExternalUserRoleDataSeedContributor`; adding a static const reference
to the strings is a small follow-up if the policy needs to be
data-driven.

## Verification

End-to-end procedure on the replicate-old-app stack:

1. Build + restart the API container.
2. Run the 9 unit tests via `dotnet test --filter BookingFlowRoles`.
3. Live probe from the SPA console:
   - As patient1 -> POST AME -> expect 400.
   - As patient1 -> POST QME -> expect 200.
   - As appatty1 -> POST AME -> expect 200.
4. SQL probe: confirm no appointment row was created for the
   blocked Patient AME attempt.

## How to apply

- Create a new branch off `feat/replicate-old-app`:
  `fix/ame-role-gate`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Mark OBS-23 fixed after live verification.
