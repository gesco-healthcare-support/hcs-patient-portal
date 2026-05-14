---
feature: external-user-appointment-cancellation
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-change-requests\
old-docs:
  - socal-project-overview.md (lines 429-433, 515-523)
  - data-dictionary-table.md (AppointmentChangeRequests, AppointmentChangeRequestDocuments)
audited: 2026-05-01
status: audit-only
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request
  - staff-supervisor-approve-cancellation     # supervisor approval flow
---

# External user appointment cancellation

## Purpose

Authorized user (appointment owner OR accessor with edit access) requests cancellation of an Approved appointment. Submits a reason. Staff Supervisor reviews and approves (selecting `CancelledNoBill` or `CancelledLate`) or rejects. On approve: slot is released, appointment status set, all stakeholders emailed + SMS'd. On reject: appointment reverts to `Approved`.

**Strict parity with OLD.** Cancellation and rescheduling share the `AppointmentChangeRequest` table but have distinct workflows.

## OLD behavior (binding)

### Cancel-request submission (`AppointmentChangeRequestDomain.Add` -- cancellation path)

Validation:
- `appointment.AppointmentStatusId == Approved` (else "NoChangeAllowedinAppointment").
- `appointmentChangeRequest.AppointmentStatusId in (CancelledLate, CancelledNoBill)` -- the user/UI specifies which.
- **Cancel-time gate:** `(slot.AvailableDate - DateTime.Today).TotalDays < SystemParameters.AppointmentCancelTime` -> reject ("CannotCancelOrRescheduleAppointment"). User cannot cancel within N days of the appointment.
- `CancellationReason` required (else "ProvideCancelReason").

Action:
1. Insert `AppointmentChangeRequest` row with `AppointmentStatusId = CancelledLate or CancelledNoBill`, `RequestStatusId = Pending`, `CancellationReason`, `CreatedById = UserClaim.UserId`.
2. **Note:** the OLD code path appears NOT to update `appointment.AppointmentStatusId` to a "CancellationRequested" interim state in this `Add` flow. (The "RescheduleRequested" interim is set in the reschedule path; cancellation seems to leave appointment at `Approved` until supervisor decision.)
3. Commit.
4. Send notification to clinic supervisors (TO VERIFY).

### Cancel-request approve (`Update` -- cancellation accept path, supervisor only)

Per `AppointmentChangeRequestDomain.Update` lines 263-292:

1. If `RequestStatusId == Accepted` AND `AppointmentStatusId in (CancelledLate, CancelledNoBill)`:
   - Set `oldAppointment.AppointmentStatusId = appointmentChangeRequest.AppointmentStatusId` (final cancelled state).
   - Set `oldAppointment.ModifiedById/Date`.
   - **Release slot:** `oldDoctorsAvailability.BookingStatusId = BookingStatus.Available`.
   - Save change request as Modified.
   - Commit.
   - Trigger SMS + Email to all stakeholders.
2. Set `isCancellationRequestApprved = true` (typo in OLD, preserved).

### Cancel-request reject

Per `Update` lines 294-306:

1. If `appointmentChangeRequest.CancellationRejectionReason != null`:
   - Revert `oldAppointment.AppointmentStatusId = Approved`.
   - Save change request.
   - Commit.
   - Send rejection email to requester.
2. Set `isCancellationRequestRejected = true`.

### Critical OLD behaviors

- **Two cancellation outcomes:** `CancelledNoBill` (within prescribed time) vs `CancelledLate` (will be billed). Supervisor chooses on approval.
- **Cancel-time gate** prevents cancellation requests within `AppointmentCancelTime` days of the appointment.
- **Slot is RELEASED on approval** -- becomes Available for new bookings.
- **No interim "CancellationRequested" status on the Appointment.** Appointment stays Approved until supervisor decides. (NEW has `CancellationRequested = 13` enum value; we should check if NEW uses it as interim or removes it for parity.)
- **Reject reverts appointment to Approved.**
- **Cancellation reason required**, rejection reason populated by supervisor.
- **Owner OR accessor with Edit access** can cancel (per spec line 431; OLD code has `// if (appointment.CreatedById != UserClaim.UserId)` commented out -- the owner-only check was removed at some point).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentChangeRequestDomain.cs` (1035 lines) | `Add` (request submit), `Update` (supervisor accept/reject), validation |
| `PatientAppointment.Domain/AppointmentChangeRequestModule/AppointmentChangeRequestDomain.cs` (106 lines, basic CRUD) | Plain CRUD shell |
| `PatientAppointment.Api/Controllers/.../AppointmentChangeRequestsController.cs` | API endpoints |
| `patientappointment-portal/.../appointment-change-requests/{add,detail,delete,edit,view}/...` | Cancel/reschedule UI components |
| `DbEntities.Models.{AppointmentChangeRequest, AppointmentChangeRequestDocument}` | EF entities |
| `Models.Enums.{AppointmentStatusType, RequestStatus, BookingStatus}` | Enums |

## NEW current state

- `AppointmentChangeRequest` entity not visible in earlier globs (TO VERIFY presence in `Domain/AppointmentChangeRequests/` or similar)
- NEW's `AppointmentStatusType` enum has `CancellationRequested = 13` -- suggests an interim state that OLD's code didn't actually use. **Strict parity: do not transition Appointment to CancellationRequested**; leave at Approved until supervisor decides. The enum value can stay (unused) without breaking parity, OR remove it.
- NEW's reminder jobs include `CancellationRescheduleReminderJob.cs` (in `Appointments/Notifications/Jobs/`) -- so some of the notification pattern is in place.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `AppointmentChangeRequest` entity | OLD table | NEW: present (Phase 1.5) | [IMPLEMENTED PRIOR] -- exists at `Domain/AppointmentChangeRequests/AppointmentChangeRequest.cs` (Phase 1.5, 2026-05-01) with all OLD fields, including `IsBeyondLimit` (typo fixed) and `AdminReScheduleReason`. | B |
| `AppointmentChangeRequestDocuments` entity | OLD table for supporting docs on cancel/reschedule | NEW: present | [IMPLEMENTED PRIOR] -- exists at `Domain/AppointmentChangeRequests/AppointmentChangeRequestDocument.cs` (Phase 1.5, 2026-05-01). | B |
| Submit-cancel endpoint | `POST /api/appointments/{id}/AppointmentChangeRequests` | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- new `IAppointmentChangeRequestsAppService.RequestCancellationAsync(appointmentId, RequestCancellationDto)`. Controller route `POST api/app/appointment-change-requests/cancel/{appointmentId}`. | B |
| Cancel-time gate | `AppointmentCancelTime` from SystemParameters | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- pure helper `CancellationRequestValidators.IsWithinNoCancelWindow(slotDate, today, cancelTimeDays)` (Domain). Manager loads `SystemParameter.AppointmentCancelTime` via `ISystemParameterRepository.GetCurrentTenantAsync` and throws `BusinessException(ChangeRequestCancelTimeWindow)` with the verbatim OLD-parity message. Strict less-than match (a slot exactly `cancelTimeDays` out is still cancellable -- pinned in unit test). | B |
| Approved-only gate | OLD code | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- pure helper `CancellationRequestValidators.CanRequestCancellation(status)` (returns true only for `Approved`). Manager throws `BusinessException(ChangeRequestAppointmentNotApproved)` on failure. | B |
| `CancellationReason` required | OLD validation | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- `RequestCancellationDto.Reason` is `[Required] [StringLength]`. Defense-in-depth: `AppointmentChangeRequest` constructor calls `Check.NotNullOrWhiteSpace(cancellationReason)` when `ChangeRequestType = Cancel`. AppService also pre-checks via `UserFriendlyException` so callers get a friendlier error before the entity is built. | B |
| Approve-cancel endpoint (supervisor) | `PUT /api/appointments/{id}/AppointmentChangeRequests/{id}` with `RequestStatusId = Accepted` | -- | [DESCOPED - Phase 17 Session B] -- supervisor approve/reject is Session B's territory per `memory/project_two-session-split.md`. | B |
| Reject-cancel endpoint (supervisor) | `PUT /api/...` with `CancellationRejectionReason` | -- | [DESCOPED - Phase 17 Session B] -- same. | B |
| Slot release on approve | OLD: `BookingStatus.Available` | NEW: known gap (slot stays Booked) | [DESCOPED - Phase 17 Session B] -- supervisor-side concern; Phase 17 owns the slot transition. | B |
| Status reverts to Approved on reject | OLD code | -- | [DESCOPED - Phase 17 Session B] -- supervisor reject is the trigger; Session B owns. | B |
| Notifications (Email + SMS) on submit | OLD: notify supervisors on submit | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- Manager publishes the existing `AppointmentChangeRequestSubmittedEto` (Phase 18 Eto stub). Email/SMS handler subscribers are Phase 18 (Session B) territory; the event surface is in place. | I |
| Owner OR accessor with Edit access can cancel | OLD: per UI; explicit owner-only check is commented out in OLD code | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 15) -- AppService composes the Phase 13a `AppointmentAccessRules.CanEdit` predicate with live state (CurrentUser.Roles, accessor rows for the target appointment). Throws `BusinessException(ChangeRequestEditAccessRequired)` on failure. View accessors are rejected; Edit accessors are allowed. | B |
| `CancellationRequested = 13` enum value | Not used as interim in OLD | NEW: enum value exists | [VERIFIED PARITY 2026-05-04] (Phase 15) -- Manager.SubmitCancellationAsync intentionally does NOT transition the parent appointment to `CancellationRequested`; the parent stays `Approved` until the supervisor's terminal decision (Phase 17). Strict OLD parity preserved. The enum value remains for any future use; not used in this flow. | I |
| `IsBeyodLimit` field (typo preserved -- "Beyod") | OLD field on AppointmentChangeRequest | NEW: present, typo fixed | [IMPLEMENTED PRIOR] -- entity property is `IsBeyondLimit` (typo corrected; OLD's "Beyod" is a real misspelling fixed per the strict-parity directive's "fix OLD typos" rule). | I |
| `AdminReScheduleReason` field | OLD field for IT Admin override on reschedule | NEW: present | [IMPLEMENTED PRIOR] -- entity property exists; used only by Phase 17's reschedule-approve override path. | I |

## Internal dependencies surfaced

- `SystemParameters.AppointmentCancelTime` (IT Admin slice)
- Supervisor approve/reject UX (Staff Supervisor audit slice)
- Email + SMS templates for cancellation events

## Branding/theming touchpoints

- Email templates: `Appointment-Cancellation-Request-Accepted.html` (referenced in OLD comment line 289), `Appointment-Cancellation-Request-Rejected.html`, `Appointment-Cancellation-Request-Submitted.html`
- SMS templates with `TemplateCode` per event

## Replication notes

### ABP wiring

- **Entity:** `AppointmentChangeRequest : FullAuditedEntity<Guid>` with `Appointment` FK + all OLD fields. `IsBeyondLimit` (corrected typo).
- **AppService:** `IAppointmentChangeRequestsAppService` with `RequestCancellationAsync`, `ApproveCancellationAsync`, `RejectCancellationAsync`. `[RemoteService(IsEnabled = false)]`. Manual controller.
- **Permissions:** `CaseEvaluation.Appointments.RequestCancellation` (any external user with edit access), `CaseEvaluation.Appointments.ApproveCancellation` (Staff Supervisor), `CaseEvaluation.Appointments.RejectCancellation` (Staff Supervisor).
- **Validation:** custom `IAppointmentChangeRequestValidator` with the cancel-time gate + Approved-only check.
- **Slot release:** in approve flow, set `slot.BookingStatusId = Available`. Use `IDoctorAvailabilityRepository`.
- **Events:** publish `AppointmentCancellationRequestedEto` (on submit), `AppointmentCancellationApprovedEto` (on approve), `AppointmentCancellationRejectedEto` (on reject). Subscribers send Email + SMS.
- **Edit-access policy:** reuse `IAppointmentAccessPolicy.CanEditAsync` from view-appointment audit.

### Things NOT to port

- Stored proc `spm.spAppointmentChangeRequests` -- LINQ-to-EF.
- `vSystemParameter` view -- direct EF query against `SystemParameters` entity.
- `ITwilioSmsService` direct -- use `ISmsSender`.
- The typo `isCancellationRequestApprved` -- fix in NEW.
- The typo `IsBeyodLimit` -> `IsBeyondLimit` in NEW.

### Verification (manual test plan)

1. Owner submits cancel on Approved appointment with reason -> success, change request row created
2. Owner submits cancel within `AppointmentCancelTime` days of appointment -> rejected
3. Owner submits cancel without reason -> rejected
4. Owner submits cancel on Pending appointment -> rejected
5. Accessor with View access tries to cancel -> rejected (no edit rights)
6. Accessor with Edit access submits cancel -> success
7. Supervisor approves with `Outcome = CancelledNoBill` -> appointment status = CancelledNoBill, slot = Available, all stakeholders emailed + SMS
8. Supervisor approves with `Outcome = CancelledLate` -> same but status = CancelledLate
9. Supervisor rejects -> appointment reverts to Approved, requester emailed
10. Slot becomes Available for re-booking after approve
