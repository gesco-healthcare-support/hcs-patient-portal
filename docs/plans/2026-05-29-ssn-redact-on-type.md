---
feature: ssn-redact-on-type
date: 2026-05-29
status: in-progress
base-branch: main
related-issues: []
sequence: 5 of 6 (booking-form cluster; supersedes part of F4-01)
branch: feat/ssn-redact-on-type
supersedes: F4-01 wire behavior (standard payloads now last-4 for everyone)
---

## Goal

Adopt the server reveal-endpoint model for SSN (Adrian decision 2026-05-29):
- Standard API payloads carry last-4 only for EVERYONE (internal users + the
  record owner included).
- The full SSN is served only by a dedicated, authorized, audited reveal
  endpoint, invoked when an authorized user clicks "reveal".
- The SSN field is never pre-filled in any form. Mask-on-type while entering a
  new value; block copy of an entered SSN (paste allowed).

## Context

### Why this changes F4-01

F4-01 (`SsnVisibility`, 2026-05-25) sends the FULL SSN over the wire to internal
users and the record owner in every standard payload (patient list/get, booking
pre-fill, appointment view) -- so a masked display is only cosmetic for those
callers and there is no access trail. Adrian chose the stronger posture: the full
SSN never crosses the wire until an authorized reveal is requested, and each
reveal is auditable. This supersedes F4-01's wire behavior (PF-001 updated).

### Verified current state (research + F3/F4 live observation, 2026-05-29)

- `SsnVisibility.RedactForCaller(ssn, isInternalCaller, isRecordOwner)`
  (`src/.../Application/Patients/SsnVisibility.cs:40`) returns full to
  internal/owner, last-4 to others. Applied at 9 `PatientsAppService` read paths
  + 3 `AppointmentsAppService` read paths (full list in the research).
- Write-back gap: `CreateAsync`, `UpdateAsync`,
  `UpdatePatientForAppointmentBookingAsync`, `UpdateMyProfileAsync` return a
  `PatientDto` mapped straight from the entity WITHOUT redaction -- they echo the
  full SSN to all callers today. This change closes that gap.
- Angular SSN inputs (ngx-mask nine-digit dashed pattern, no `[hiddenInput]`):
  booking `appointment-add-patient-demographics.component.html:133`, appointment
  view `appointment-view.component.html:333`, patient profile `:166`, patient
  detail `:148`. SSN is pre-filled from the patient DTO at the load paths
  (`appointment-add.component.ts:1406/1522/1640`, `appointment-view.component.ts:399`,
  the two patient components). Read-only masked display via `SsnMaskPipe` on the
  appointment list (`home.component.html:196`) and patient list
  (`patient.component.html:495`).
- Client knows the caller's roles + id via
  `ConfigStateService.getOne('currentUser')` (`roles: string[]`, `id`); record
  ownership = `currentUser.id === patient.identityUserId` (computable client-side).
- Audit: ABP Commercial's automatic HTTP audit log (`AbpAuditLogs`) records every
  request with user, tenant, URL, timestamp. A `GET /api/app/patients/{id}/ssn`
  reveal call is therefore auto-audited (caller + patient id in the URL). No app
  code writes audit entries today; `IAuditingManager` enrichment is optional.
- No SSN log lines exist today (display only). The reveal endpoint's audit is the
  only new logging.
- The masked form referenced below means asterisks for the first five digits and
  the real last four; no SSN-shaped literal is written in this doc.

## Approach

Backend makes the full SSN reach the client only via the reveal endpoint; the
"never pre-fill" rule means an empty SSN field = "leave the stored value
unchanged", so no masked-sentinel logic is needed.

1. Standard payloads -> last-4 for everyone. Replace the role-aware
   `SsnVisibility.RedactForCaller` usage in the standard read paths with an
   always-mask (`MaskToLast4`) call, and apply it to the 4 write-back paths too
   (closing the gap). `SsnVisibility`'s role/owner parameters move to the reveal
   endpoint's authorization (below).
2. Reveal endpoint. New `IPatientsAppService.GetFullSsnAsync(Guid patientId)`
   returning `SsnRevealDto { string? SocialSecurityNumber }`. Implementation
   authorizes with the existing `IsInternalCallerForSsn()` /
   `IsRecordOwnerForSsn(patient.IdentityUserId)`; throws
   `AbpAuthorizationException` otherwise; returns the raw entity SSN. New
   permission `CaseEvaluationPermissions.Patients.RevealSsn` (granted to internal
   roles + Patient). Manual controller route `GET api/app/patients/{id}/ssn`.
   Regenerate the Angular proxy. The ABP HTTP audit log captures each reveal.
3. Never overwrite on empty. The update paths treat a null/empty incoming SSN as
   "no change" (omit `SocialSecurityNumber` from the entity update) so the
   never-pre-filled empty field cannot wipe a stored SSN. Create with a typed SSN
   still sets it.
4. Angular UX.
   - Remove SSN pre-fill at all load sites (the field starts empty).
   - "SSN on file" read-only display: the masked last-4 form (from the DTO) with
     a reveal (eye) button shown only to internal roles + the record owner;
     clicking calls `GetFullSsnAsync` and shows the full value read-only
     (audited).
   - "Enter / change SSN" input: empty; a mask-on-type directive shows digits as
     typed then redacts to last-4 (~1s), with a client-side reveal of what was
     typed, and blocks copy/cut (paste allowed). Leaving it empty changes nothing.
   - List/grid cells keep the `SsnMaskPipe` masked display (now last-4 for all,
     matching the DTO).
5. Parity: update PF-001 to record the posture change (full SSN now
   endpoint-only + audited).

UX point to confirm at approval: the split between the read-only "SSN on file
(masked + reveal)" display and the empty "enter/change SSN" input. This keeps
"never pre-fill" while still letting authorized staff view + verify the stored
SSN.

Alternatives rejected: client-only toggle (Design A) -- full SSN already on the
wire for internal/owner, no audit; rejected by Adrian. Masked-sentinel round-trip
on edit -- unnecessary once SSN is never pre-filled.

## Tasks

- T1: Standard payloads + write-backs return last-4 for everyone.
  - approach: tdd
  - files-touched: src/.../Application/Patients/SsnVisibility.cs (add/use always-mask), PatientsAppService.cs (read + the 4 write-back paths), AppointmentsAppService.cs (3 read paths); extend test/.../Patients/SsnVisibilityUnitTests.cs
  - acceptance: every patient/appointment read AND write-back returns the masked
    form for all roles; unit tests assert always-mask + null/empty passthrough.

- T2: Audited reveal endpoint + permission.
  - approach: tdd
  - files-touched: Application.Contracts (IPatientsAppService, SsnRevealDto, CaseEvaluationPermissions, CaseEvaluationPermissionDefinitionProvider), PatientsAppService.GetFullSsnAsync, HttpApi PatientController route; regenerate angular/src/app/proxy
  - acceptance: internal caller + record owner get the full SSN; external
    non-owner gets `AbpAuthorizationException`; unauthenticated denied. Unit
    tests cover the role/owner branches. The call appears in `AbpAuditLogs`.

- T3: Update paths never overwrite a stored SSN with empty.
  - approach: tdd
  - files-touched: PatientsAppService.UpdateAsync / UpdatePatientForAppointmentBookingAsync / UpdateMyProfileAsync (+ a shared "apply SSN only if provided" helper); domain/mapper as needed
  - acceptance: updating a patient with a null/empty SSN leaves the stored SSN
    unchanged; updating with a typed SSN sets it; unit tests for both.

- T4: Angular -- never pre-fill, mask-on-type + copy-block, reveal-via-endpoint.
  - approach: test-after
  - files-touched: remove SSN pre-fill at the load sites (appointment-add, appointment-view, patient-profile, patient-detail); new shared SSN entry directive (mask-on-type + copy-block) + a reveal control calling the proxy; role/owner gating via ConfigStateService
  - acceptance: SSN field empty on load; typing shows then masks to last-4; copy
    blocked, paste works; reveal (eye) visible only to internal + owner and shows
    the full stored SSN via the endpoint; `npx ng build --configuration development` passes.

- T5: Update the parity flag.
  - approach: code
  - files-touched: docs/parity/_parity-flags.md (revise PF-001)
  - acceptance: PF-001 reflects full-SSN-only-via-audited-endpoint posture.

## Risk / Rollback

- Blast radius: SSN now last-4 everywhere in standard payloads -- any consumer
  that depended on the full SSN in a list/get must use the reveal endpoint. The
  write-back + never-pre-fill changes mean edit flows must not send a stored SSN
  they no longer have. The "empty = no change" rule is the safety net; T3 tests
  guard it. Missing a write path = a stored SSN could be wiped -> covered by T3.
- At-rest encryption remains out of scope (separate deferred cycle).
- Rollback: revert the PR; standard payloads return to F4-01 role-aware behavior.

## Verification

Rebuild api + angular (`docker compose up -d --build api angular`), then:
1. As internal staff / the patient (owner): patient + appointment views show the
   masked SSN; the reveal (eye) shows the full SSN (a `GET .../ssn` appears in
   the audit log). As an attorney/CE on someone else's record: last-4 only, no
   reveal; calling the endpoint directly -> 403.
2. Edit a patient leaving SSN empty -> stored SSN unchanged. Enter a new SSN ->
   updated. Booking a new patient with a typed SSN -> set.
3. SSN entry field: digits show then mask to last-4; copy blocked; paste works.
4. Network tab: no standard payload carries a full SSN.
5. xUnit green (SsnVisibility + reveal endpoint + update-no-overwrite).
