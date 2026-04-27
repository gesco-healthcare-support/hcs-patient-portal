# Probe log: new-sec-04-external-signup-real-defaults

**Timestamp (local):** 2026-04-24T23:15:00
**Purpose:** Confirm NEW-SEC-04 via static code reading. The defect is a
hardcoded-literal bug in `ExternalSignupAppService.RegisterAsync`. A live probe
of `POST /api/public/external-signup/register` would create a persistent
`IdentityUser`, `IdentityRole` (if missing), and `Patient` row in LocalDB. Per
`../README.md:262-272` ("Never probe SaaS tenant creation, IdentityUser
creation, OpenIddict client creation, ApplicantAttorney creation, or Patient
creation"), no live probe is run for this capability. Static proof suffices.

## Static evidence: hardcoded values in RegisterAsync

**File:** `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs`
**Lines:** 211-224

The literal call to `_patientManager.CreateAsync` for `UserType == Patient`:

```
if (input.UserType == ExternalUserType.Patient)
{
    await _patientManager.CreateAsync(
        stateId: null,
        appointmentLanguageId: null,
        identityUserId: user.Id,
        tenantId: CurrentTenant.Id,
        firstName: input.FirstName,
        lastName: input.LastName,
        email: input.Email,
        genderId: Gender.Male,
        dateOfBirth: DateTime.UtcNow.Date,
        phoneNumberTypeId: PhoneNumberType.Home
    );
}
```

Three literals are written directly into the call:

- `genderId: Gender.Male` -- every self-registered Patient is persisted as
  `GenderId = 1` (per `Domain.Shared/Enums/Gender.cs:5`).
- `dateOfBirth: DateTime.UtcNow.Date` -- every self-registered Patient's DOB
  column is set to the server UTC midnight of the signup day.
- `phoneNumberTypeId: PhoneNumberType.Home` -- every self-registered Patient
  is persisted as `PhoneNumberTypeId = 29` (per
  `Domain.Shared/Enums/PhoneNumberType.cs:6`).

No conditional branch, no input mapping, no configuration look-up. The three
values are the deterministic outputs of any successful invocation.

## Static evidence: DTO does not carry these fields

**File:** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/ExternalUserSignUpDto.cs`
**Lines:** 6-29

The DTO exposes only `UserType`, `FirstName`, `LastName`, `Email`, `Password`,
`TenantId`. There is no property named `GenderId`, `DateOfBirth`, or
`PhoneNumberTypeId`. Therefore any value in those Patient columns must come
from inside the AppService, not from the HTTP caller. This confirms the values
in the defect are hardcoded, not overwritten by client-side input.

## Static evidence: Patient columns are currently NOT NULL

**File:** `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260210185726_Added_Patient.cs`
**Lines:** 23, 24, 32

```
GenderId = table.Column<int>(type: "int", nullable: false),
DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: false),
...
PhoneNumberTypeId = table.Column<int>(type: "int", nullable: false),
```

All three columns are `nullable: false`. Making them nullable in the
recommended solution requires an EF migration (single
`AlterColumn<int?>`/`AlterColumn<DateTime?>` per column).

## Static evidence: the entity ctor currently requires these values

**File:** `src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs`
**Lines:** 32, 34, 57, 83-126

Properties are declared as non-nullable value types
(`Gender GenderId`, `DateTime DateOfBirth`, `PhoneNumberType PhoneNumberTypeId`).
The ctor takes them as required positional parameters. No schema escape hatch
exists today for "no value supplied".

## Static evidence: PatientManager.CreateAsync forwards the three values

**File:** `src/HealthcareSupport.CaseEvaluation.Domain/Patients/PatientManager.cs`
**Lines:** 23-48

The manager's `CreateAsync` signature requires non-nullable
`Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId`
and passes them straight into the Patient ctor. `Check.NotNull` on value
types is a no-op; the real constraint is the type system.

## Static evidence: empty Patients table at probe time

Confirmed in `../probes/service-status.md`: the authenticated
`GET /api/app/appointments` probe returns `{"totalCount": 0, "items": []}`.
The related `GET /api/app/patients` probe was not executed (PHI isolation
policy) but the same "zero seeded rows" state applies because there is no
seed contributor for Patient (confirmed by absence of `PatientsDataSeedContributor`
under `Domain/Patients/`). So the EF migration in the recommended solution
encounters zero rows: no data migration is required.

## Interpretation

The defect is conclusively present at the cited lines. The three hardcoded
values are not configurable, not overridable by the caller, and not the
result of a missing form field -- they are intentional literals. The
recommended fix (nullable columns + drop the literals + Angular
profile-completion guard) is safe against the current zero-row database
state and requires no data backfill.

## Cleanup

Not applicable. No mutating probes were executed; no cleanup is required.
