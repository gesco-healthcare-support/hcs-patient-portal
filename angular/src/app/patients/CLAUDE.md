# Patients -- patient CRUD + self-service profile

## What Lives Here

`patient/` -- admin CRUD modal (patient-detail), read-only list, and patient self-service
profile (patient-profile).

Key files:

- `patient/services/patient-detail.abstract.service.ts` -- form builder + create/update logic
- `patient/components/patient-detail.component.html` -- admin edit modal template
- `patient/components/patient-profile.component.ts` -- self-service profile page

## Conventions

### SSN -- Design B (app-ssn-input only)

`<app-ssn-input>` is the sole SSN entry surface in both the admin modal and the profile
page. See `patient-detail.component.html` for the full binding with `[patientId]`,
`[patientIdentityUserId]`, and `[currentMaskedSsn]` inputs.

`currentMaskedSsn` receives the DTO value (last-4 display string such as `***-**-1234`).
That string is NOT the real SSN; it is a masked sentinel used by `SsnInputComponent` for
display only. Never treat `selected.patient.socialSecurityNumber` as a real SSN -- doing
so and passing it anywhere is a HIPAA violation. For `SsnInputComponent` behavior rules,
see `angular/src/app/shared/CLAUDE.md`.

### Empty SSN submit -- leave stored value unchanged

In `PatientProfileComponent.loadMyProfile` the SSN field is explicitly nulled out after
the spread: `socialSecurityNumber: null`. When the user submits without entering a new SSN
the field is empty (`null`), and the backend Domain rule treats an empty value as "no
change" -- it never clears a stored SSN. Do not add frontend logic to restore or repeat
the masked value on submit; empty is correct.

### PatientProfileComponent -- two fetch paths, one save path

`isExternalUserNonPatient` is true for every role except `Patient` (case-insensitive).

| Role check                  | Load endpoint                    | Save                             |
| --------------------------- | -------------------------------- | -------------------------------- |
| `false` (Patient role)      | GET `/api/app/patients/me`       | PUT `/api/app/patients/me`       |
| `true` (CE, DA, staff, ...) | GET `/api/app/external-users/me` | `save()` returns early -- no PUT |

The external-user path populates only `firstName`, `lastName`, `email`, `identityUserId`; all
other form controls stay `null`. Do not attempt to save from the non-Patient path; the early
return in `save()` is intentional (W-B-2 fix, 2026-04-30).

## Gotchas

- `PatientProfileComponent` uses `ChangeDetectionStrategy.Default`, not OnPush -- no need
  for `markForCheck()` on async loads here, unlike the `abp-lookup-select` OnPush issue.
- The admin modal (`patient-detail`) re-uses `AbstractPatientDetailViewService`; do not add
  a second SSN input or bypass `app-ssn-input` there.

## Related

- docs/security/HIPAA-COMPLIANCE.md
- docs/frontend/ROLE-BASED-UI.md
- docs/frontend/COMPONENT-PATTERNS.md
