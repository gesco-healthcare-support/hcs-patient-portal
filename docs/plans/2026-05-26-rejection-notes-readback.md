---
feature: rejection-notes-readback
date: 2026-05-26
status: in-progress
base-branch: main
related-issues: []
related-finding: F4-02-RejectionNotes (surfaced 2026-05-26 02:16 PT during demo hardening pass)
---

## Goal

Surface `Appointment.RejectionNotes` (and `RejectedById`) on the read
DTO so the patient + staff can see WHY a rejected appointment was
rejected. Today the column is written on reject but never returned.

## Context

Live-verified 2026-05-26 02:16 PT:

- `Appointment.cs:85` defines `public virtual string? RejectionNotes`.
- `AppointmentsAppService.Approval.cs:156` sets
  `appointment.RejectionNotes = input.Reason` during the reject flow.
- DB row for A00002 (Rejected) has
  `RejectionNotes = "Doctor schedule conflict - demo rejection test reason."`.
- `AppointmentDto.cs` does NOT declare `RejectionNotes`.
- API response for `/api/app/appointments/with-navigation-properties/<id>`
  has `appointment.internalUserComments: null` AND no `rejectionNotes`
  key at all.
- Riok.Mapperly silently drops the source-only field because the
  target DTO has nothing to bind to.

**Effect:** Patient cannot see why their appointment was rejected.
Staff cannot see the rejection reason after the fact either. Demo
risk: if audience clicks A00002 or rejects A00003 then re-opens it,
the rejection-reason field reads empty.

OLD parity reference: OLD app preserved rejection reasons in the
rejected-appointment view per `P:\PatientPortalOld` (specific
location not inspected -- parity intent inferred from the email
template `PatientAppointmentRejected.html` which embeds
`##RejectionNotes##`).

## Approach

**Single targeted change: extend `AppointmentDto` with the missing
field.** Mapperly's source-by-property-name convention will auto-map
the column. Then regenerate the Angular proxy so the typed client
sees the field.

**No redaction needed.** The rejection reason is intended for the
patient + their attorneys to read (the OLD `PatientAppointmentRejected`
email template literally renders it as
`Please note rejection reason: ##RejectionNotes##`). Internal
staff also need to see it. There is no "internal-only" semantics to
filter -- it is a user-facing field by design.

**Alternatives considered:**

- Add to `AppointmentDto` PLUS pipe through
  `ExternalUserDtoFilter.MaskInternalFields` to null it for
  externals. Rejected -- defeats the purpose; patient must see the
  reason.
- Add ONLY `RejectionNotes`, skip `RejectedById`. Rejected --
  surface both for audit clarity; the entity has them paired.
- Add a brand-new `RejectedAppointmentDto` projection. Rejected --
  overkill for two columns; one DTO with optional fields is fine.

## Tasks

- T1: Add `RejectionNotes` (string?) and `RejectedById` (Guid?) to
  AppointmentDto.cs.
  - approach: code
  - files-touched:
    - src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentDto.cs
  - acceptance:
    - `dotnet build src/HealthcareSupport.CaseEvaluation.Application` passes.
    - Mapperly source-generated code includes assignments
      `destination.RejectionNotes = source.RejectionNotes` and
      `destination.RejectedById = source.RejectedById` (verify via
      `find . -name "AppointmentToAppointmentDtoMappers.g.cs"` after
      build).

- T2: Regenerate Angular proxy.
  - approach: code
  - files-touched:
    - angular/src/app/proxy/appointments/models.ts (auto-generated)
  - acceptance:
    - `grep -r "rejectionNotes" angular/src/app/proxy/appointments/`
      returns at least one match in `models.ts`.
    - `npx ng build --configuration development` passes.
  - Note: ABP CLI may not be available in this dev environment; if
    `abp generate-proxy` fails, manually patch `models.ts` to add
    `rejectionNotes?: string | null;` and `rejectedById?: string | null;`
    to the `AppointmentDto` interface.

- T3: Verify live in browser.
  - approach: code (verification only, no code change)
  - acceptance:
    - Restart `main-api-1` to load new DTO.
    - Login as patient1, navigate to A00002 (Rejected QME).
    - Capture the `/api/app/appointments/with-navigation-properties/<id>`
      response via browser_network_request -- response body contains
      `rejectionNotes: "Doctor schedule conflict - demo rejection
      test reason."`.

## Risk / Rollback

- Blast radius: tiny. Two additive DTO fields. Riok.Mapperly handles
  the rest via convention. Proxy regen is mechanical.
- Rollback: revert the AppointmentDto.cs edit. Restart the api
  container. The form-control reads empty again -- same as today.

## Verification

After T1 + T2 + T3 land:

1. Restart api container so the new DTO assembly loads.
2. Login as patient1@gesco.com.
3. Navigate to /appointments/view/c2b6e5d6-e6c8-0f03-ab54-3a2170433b02
   (A00002 Rejected).
4. Open DevTools Network tab -> XHR -> click the
   `with-navigation-properties` request.
5. Response body has
   `"rejectionNotes": "Doctor schedule conflict - demo rejection test reason."`.
6. Same check as stafsuper1 (should also see the full reason).

Form-control visual rendering: out of scope for this plan. The
appointment-view component currently has no `formControlName="rejectionNotes"`
binding (verified live 02:18 PT). Wiring the form-control to display
the value is a separate Angular-side task -- not blocking the demo
since the data is on the wire and a future UI iteration can render
it without further server changes.
