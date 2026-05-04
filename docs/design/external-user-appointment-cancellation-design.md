---
feature: external-user-appointment-cancellation
status: draft
audited: 2026-05-04
old-source:
  - patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/appointment-change-request-add.component.html
  - patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/appointment-change-request-add.component.ts
  - PatientAppointment.Domain/AppointmentRequestModule/AppointmentChangeRequestDomain.cs
new-source:
  - src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentChangeRequests/IAppointmentChangeRequestsAppService.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentChangeRequests/Dto/RequestCancellationDto.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/CancellationRequestValidators.cs
parity-audit: docs/parity/external-user-appointment-cancellation.md
shell-variant: 2-external-authenticated
strict-parity: true
---

# external-user-appointment-cancellation -- design

A modal launched from the appointment view page (or list row context
menu) that lets an authorized user submit a cancellation request on an
Approved appointment. The modal shares its add-component with the
reschedule modal in OLD; the design.md splits them by status branch
(`statusId == appointmentStatusCancelled` vs `statusId == appointmentStatusReschedule`).
Phase 15 backend already shipped `RequestCancellationAsync`; this design
captures the modal contract.

## 1. Routes

OLD launches the modal via `<rx-popup>` from the view-appointment page;
no dedicated route. NEW preserves the modal pattern:

- OLD: modal launched from `/appointments/view/:id` -> NEW: modal from `/appointments/view/:id`
- NEW backend: `POST api/app/appointment-change-requests/cancel/{appointmentId}`

## 2. Screen layout

### Cancel modal (statusId == Cancelled branch)

```
+--------------------------------------------------------+
|  Cancellation Request                          [X]     |
|  Please provide a reason for the cancellation request. |
+--------------------------------------------------------+
|                                                        |
|   Reason for Cancellation                              |
|   +--------------------------------------------+       |
|   | (textarea, 3 rows)                         |       |
|   |                                             |       |
|   |                                             |       |
|   +--------------------------------------------+       |
|                                                        |
+--------------------------------------------------------+
|                              [Save] [Close]            |
+--------------------------------------------------------+

OLD: ./screenshots/old/external-user-appointment-cancellation/01-cancel-modal.png
NEW: NEW UI not yet built.
```

Cite: `appointment-change-request-add.component.html`:1-25, 91-94.

The modal title bar uses brand-primary background with white text
(`text-white`). Modal-lg width per Bootstrap 4. Footer has
right-aligned Save (primary) + Close (secondary).

### Cancel-time gate error (validation popup)

When user submits within `SystemParameters.AppointmentCancelTime` days
of the slot, NEW throws `BusinessException(ChangeRequestCancelTimeWindow)`
with the verbatim OLD message "You cannot cancel or reschedule an
appointment within {N} days of the appointment date." (cite Phase 15
impl in parity audit).

```
+---------------------------------------------+
|  Validation                              [X] |
+---------------------------------------------+
|  You cannot cancel or reschedule an         |
|  appointment within 7 days of the           |
|  appointment date.                          |
+---------------------------------------------+
|                                       [OK]  |
+---------------------------------------------+
```

NEW: rendered via `<app-validation-popup>` per `_components.md`.

### Empty reason error

Validation popup with "ProvideCancelReason" message (OLD ValidationFailedCode key); NEW localizes to "Please provide a reason for the cancellation request."

## 3. Form fields

| Label | Field name | Type | Validation | Default | Conditional visibility | OLD citation |
|---|---|---|---|---|---|---|
| Reason for Cancellation | `cancellationReason` | textarea (rows=3) | required (not null/whitespace), maxLength TO VERIFY (entity `Check.NotNullOrWhiteSpace` + StringLength on DTO) | -- | `*ngIf="statusId == appointmentStatusCancelled"` | `appointment-change-request-add.component.html`:20-25 |

The textarea has no placeholder in OLD -- empty by default with the
3-line height. NEW: same.

## 4. Tables / grids

None. The user's submitted cancellation requests are visible in the
"Cancel Requests" sidebar item for internal users (per
`_shell-layout.md` side-bar matrix row 4d) -- that surface lives in
the `staff-supervisor-change-request-approval-design.md` doc, not here.

External users do not see a list of their own change requests in OLD;
the parent appointment view shows the change-request status.

## 5. Modals + interactions

The whole feature IS a modal. Trigger:

| Trigger | Modal title | Body | Primary action | Secondary action | OLD citation |
|---|---|---|---|---|---|
| Click "Cancel Appointment" button on view-appointment page (with edit access) | "Cancellation Request" + "Please provide a reason for the cancellation request." subtitle | textarea | Save (`btn-primary`, disabled until form valid) | Close (`btn-secondary`) | `appointment-change-request-add.component.html`:3-15, 91-94 |

After Save:

- Success toast (TO VERIFY exact OLD copy; default: "Your cancellation request has been submitted. A supervisor will review it shortly."). NEW localizes via `AppointmentChangeRequests:CancellationRequestSubmitted`.
- Modal closes; parent view-appointment page refreshes to show the request status (Pending).

After error (validation popup):

- Modal stays open; popup overlays.

## 6. Buttons + actions

| Label | Variant | Permission gate | Pre-action confirm? | Success toast | Error toast | OLD citation |
|---|---|---|---|---|---|---|
| Save | primary | `CaseEvaluation.AppointmentChangeRequests.RequestCancellation` (per parity audit + `IAppointmentAccessRules.CanEdit`) | N | "Your cancellation request has been submitted." | (validation popup) | line 92 |
| Close (modal X icon + Close button) | secondary | n/a | N (no confirm if form is dirty -- OLD does not warn; NEW: TO DECIDE if we add a `canDeactivate` guard) | n/a | n/a | lines 15, 93 |

The Save button binds to `addAppointmentChangeRequest()` and is
`[disabled]="!appointmentChangeRequestFormGroup.valid"`.

## 7. Role visibility matrix

Per parity audit: external users (Patient, Adjuster, Applicant Atty,
Defense Atty) can request cancellation IF they are the appointment
owner OR an accessor with Edit access. View-only accessors are
rejected. Internal users follow the supervisor-side approval flow
documented in `staff-supervisor-change-request-approval-design.md`.

| UI element | Patient | Adjuster | Applicant Atty | Defense Atty | Clinic Staff | Staff Supervisor | IT Admin |
|---|---|---|---|---|---|---|---|
| Cancel button on view-appointment page | Y if owner OR Edit accessor; else hidden | Y if owner OR Edit accessor | Y if owner OR Edit accessor | Y if owner OR Edit accessor | (N/A this view; staff use approval queue) | (N/A) | (N/A) |
| Cancellation Request modal | Y when button clicked | Y | Y | Y | -- | -- | -- |
| Reason textarea | Y | Y | Y | Y | -- | -- | -- |
| Save button | Y when valid | Y | Y | Y | -- | -- | -- |

Permission gate cites: parity audit gap row "Owner OR accessor with
Edit access can cancel" + Phase 15 `AppointmentAccessRules.CanEdit`
predicate composition in NEW `AppointmentChangeRequestsAppService`.

## 8. Branding tokens used

- Modal -- `<app-confirm-dialog>` chrome from `_components.md`: `--scrim-modal`, `--shadow-modal`, `--radius-md`, `--bg-card`, `--motion-modal`
- Modal header bar -- `--brand-primary` bg + `#fff` text
- Form field -- `--radius-sm`, `--font-family-base`, `--text-body`, `--shadow-input-focus`, `--brand-primary-focus-border`, `--color-danger` (invalid border)
- Save button -- `--brand-primary` bg, `#fff` text, `--fs-button-login`, `--fw-bold`
- Close button -- secondary variant per `_components.md`
- Email subject -- `L["AppointmentChangeRequests:CancellationRequestSubmittedSubject", clinicShortName]` (via `_branding.md` localization keys)

## 9. NEW current-state delta

**Backend already shipped Phase 15 (2026-05-04)** -- cite parity audit
gap rows marked `[IMPLEMENTED 2026-05-04 - pending testing]`:

- `IAppointmentChangeRequestsAppService.RequestCancellationAsync(Guid appointmentId, RequestCancellationDto)` exists.
- `RequestCancellationDto.Reason` is `[Required] [StringLength]`.
- `CancellationRequestValidators.IsWithinNoCancelWindow(slotDate, today, cancelTimeDays)` enforces the cancel-time gate (strict less-than match: a slot exactly N days out is still cancellable -- pinned in unit test).
- `CancellationRequestValidators.CanRequestCancellation(status)` returns true only for `Approved`.
- AppService uses `AppointmentAccessRules.CanEdit` predicate against `CurrentUser.Roles + accessor rows` (Phase 13a).
- Manager publishes `AppointmentChangeRequestSubmittedEto` (event surface in place; subscribers are Phase 18).
- Parent appointment is intentionally NOT transitioned to `CancellationRequested = 13` -- stays `Approved` until supervisor decision (strict OLD parity verified 2026-05-04).
- Slot release on approve, status revert on reject -- both `[DESCOPED - Phase 17 Session B]`.

**Frontend NEW UI not yet built.** No
`angular/src/app/appointments/cancel-request/...` component exists.
This design.md drives the modal build.

Implementation hint: the OLD modal is shared with reschedule (one
component, one form group, two `*ngIf` branches). NEW can choose to:

- (a) Mirror OLD's shared component -- one Angular component with
  two states; or
- (b) Split into `<app-cancel-request-modal>` and
  `<app-reschedule-request-modal>` -- cleaner separation, but
  duplicates the modal chrome.

Recommend **(b)** -- the two flows have meaningfully different fields
(cancellation only has a reason; reschedule has slot picker + JDF
upload) and the conditional logic adds noise. Token contract
preserved.

## 10. Strict-parity exceptions

1. **`CancellationRequested = 13` enum value left unused.** OLD's interim status; NEW keeps the parent appointment at `Approved` until supervisor decision (parity verified 2026-05-04 in Phase 15). Strict OLD parity preserved.
2. **`isCancellationRequestApprved` -> `isCancellationRequestApproved`.** OLD typo, fixed for correctness.
3. **`IsBeyodLimit` -> `IsBeyondLimit`.** OLD typo, fixed for correctness.
4. **No info-leak on missing appointment.** Standard ABP authorization throws unauthorized; NEW surface returns 404/403 without echoing the appointment ID.
5. **Modal split (recommended).** Per Section 9 above; visible UX equivalent.
6. **Reason textarea max length** added at DTO level (`StringLength`); OLD lacks an explicit cap but the DB column is bounded -- pin the cap at `2000` chars to match the entity's NVARCHAR length (TO VERIFY DB schema during impl).

## 11. OLD source citations (consolidated)

```
- patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/appointment-change-request-add.component.html
- patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/appointment-change-request-add.component.ts
- patientappointment-portal/src/app/components/appointment-request/appointment-change-requests/add/appointment-change-request-add.service.ts
- PatientAppointment.Api/Controllers/.../AppointmentChangeRequestsController.cs
- PatientAppointment.Domain/AppointmentRequestModule/AppointmentChangeRequestDomain.cs (Add + AddValidation, lines 30-200)
- PatientAppointment.DbEntities.Models.AppointmentChangeRequest
```

## 12. Verification (post-implementation)

- [ ] Owner clicks Cancel on Approved appointment -- modal opens with title "Cancellation Request" + subtitle.
- [ ] Empty reason -- Save disabled.
- [ ] Type reason -- Save enables.
- [ ] Click Save -- success toast; modal closes; parent view shows pending change request banner.
- [ ] Submit on Pending appointment -- popup renders `ChangeRequestAppointmentNotApproved`.
- [ ] Submit within `SystemParameters.AppointmentCancelTime` days -- popup renders `ChangeRequestCancelTimeWindow` with the resolved day count.
- [ ] View-only accessor clicks Cancel -- button is hidden (or popup renders `ChangeRequestEditAccessRequired` if button leaked).
- [ ] Edit-access accessor clicks Cancel -- modal opens; submission succeeds.
- [ ] Click Close -- modal closes without submission; no warning if form is dirty (parity).
- [ ] Per-tenant theme: swap brand-primary -- modal header bar + Save button reflect the new color.
