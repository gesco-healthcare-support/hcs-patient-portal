---
feature: external-user-appointment-rescheduling
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-change-requests\
old-docs:
  - socal-project-overview.md (lines 421-427, 503-513)
  - data-dictionary-table.md (AppointmentChangeRequests, Appointments.OriginalAppointmentId)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request
  - external-user-appointment-cancellation     # shares AppointmentChangeRequest table + workflow shape
  - staff-supervisor-approve-reschedule         # supervisor approval flow
---

# External user appointment rescheduling

## Purpose

Authorized user (appointment owner OR accessor with edit access) requests reschedule of an Approved appointment. Picks a new slot from the availability calendar (within window). Submits with reason. Staff Supervisor reviews; on approve, **a NEW appointment row is created with the SAME `RequestConfirmationNumber`**, `OriginalAppointmentId` pointing to old, status `Approved`, new slot booked. The old appointment is set to `RescheduledNoBill` or `RescheduledLate`. Old slot released, new slot booked. All stakeholders notified.

**Strict parity with OLD.** Sibling slice to cancellation; share the `AppointmentChangeRequest` table.

## OLD behavior (binding)

### Reschedule-request submission (`AppointmentChangeRequestDomain.Add` -- reschedule path)

Validation (see `AddValidation` lines 97-193):

- `appointment.AppointmentStatusId == Approved` (else "NoChangeAllowedinAppointment").
- `appointmentChangeRequest.AppointmentStatusId in (RescheduledLate, RescheduledNoBill, RescheduleRequested)` -- the user/UI specifies (typically `RescheduleRequested` for the interim state).
- `ReScheduleReason` required (else "ProvideRescheduleReason").
- `DoctorAvailabilityId != 0` (else "ProvideNewAppointmentDateTime").
- New slot's `BookingStatusId == Available` (else "AppointmentBookingDateNotAvailable").
- **Lead time gate** (only for non-`IsBeyodLimit` requests): new slot's date >= `DateTime.Now + AppointmentLeadTime`.
- **Per-type max time gate** (per appointment type, mirroring booking validation):
  - PQME / PQME-REVAL: <= `DateTime.Now + AppointmentMaxTimePQME`
  - AME / AME-REVAL: <= `DateTime.Now + AppointmentMaxTimeAME`
  - OTHER: <= `DateTime.Now + AppointmentMaxTimeOTHER`
- **`IsBeyodLimit` (typo: "IsBeyondLimit") flag:** when set (e.g., admin override), validation INVERTS -- requires the new date to be BEYOND the max-time window. Used for IT Admin to schedule in the future beyond normal limits.

Action (per `Add` lines 197-223):

1. If `AppointmentStatusId == RescheduleRequested`:
   - Set `slot.BookingStatusId = Reserved` (interim hold on the new slot pending supervisor approval).
   - Save uploaded supporting documents (`AppointmentChangeRequestDocuments`) to `wwwroot/Documents/...` -- via `UploadRescheduleDocument`. Set their status to `Uploaded`.
   - Send notification email (interim).
2. Set `appointment.AppointmentStatusId = appointmentChangeRequest.AppointmentStatusId` (transitions appointment to `RescheduleRequested`).
3. Insert `AppointmentChangeRequest` row.
4. Commit.

### Reschedule-request approve (`Update` -- reschedule accept path, supervisor only)

Per `AppointmentChangeRequestDomain.Update` lines 309+:

1. Set `oldAppointment.AppointmentStatusId = appointmentChangeRequest.AppointmentStatusId` (RescheduledLate or RescheduledNoBill).
2. **CREATE a NEW Appointment row** (insert, not update) with:
   - All FKs copied from `oldAppointment` (PatientId, AppointmentTypeId, LocationId, etc.)
   - `RequestConfirmationNumber = oldAppointment.RequestConfirmationNumber` (**SAME** confirmation #)
   - `DoctorAvailabilityId = appointmentChangeRequest.DoctorAvailabilityId` (NEW slot)
   - `AppointmentStatusId = Approved`
   - `AppointmentApproveDate = DateTime.Now`
   - `OriginalAppointmentId = oldAppointment.OriginalAppointmentId` (chains through)
   - All ReSchedule/Cancellation/Rejection reason fields preserved from old
   - `IsPatientAlreadyExist = oldAppointment.IsPatientAlreadyExist`
3. Cascade-copy all child entities:
   - `AppointmentPatientAttorneys` (if any) -> new attorney rows linked to new appointment
   - (Same for DefenseAttorneys, InjuryDetails + sub-entities, EmployerDetails, Accessors, CustomFieldsValues, Documents, JointDeclarations -- TO VERIFY full list in lines 350+)
4. Update slot statuses:
   - Old slot: TO VERIFY -- likely RELEASED (BookingStatus.Available) since the appointment moved
   - New slot: BOOKED (BookingStatus.Booked) -- promoted from Reserved
5. Save change request as Modified.
6. Commit.
7. Send Email + SMS to all stakeholders with NEW appointment date.

### Reschedule-request reject

Per `Update` (similar to cancel reject, TO VERIFY exact lines):

1. Revert `oldAppointment.AppointmentStatusId = Approved`.
2. Release the new slot (Reserved -> Available).
3. Set `appointmentChangeRequest.ReScheduleRejectionReason`.
4. Send rejection email to requester.

### Critical OLD behaviors

- **Two reschedule outcomes:** `RescheduledNoBill` (within prescribed time) vs `RescheduledLate` (will be billed). Supervisor chooses on approval.
- **NEW APPOINTMENT ROW on approve** -- same `RequestConfirmationNumber`, new `DoctorAvailabilityId`, status `Approved`. The old appointment becomes a historical record at `RescheduledNoBill/Late`.
- **Slot transitions:** new slot -> Reserved on submit, Booked on approve, Available on reject. Old slot -> stays Booked while change request is Pending; released on approve.
- **Interim status:** `RescheduleRequested` on the appointment during pending review (this IS used in OLD's reschedule flow, unlike cancellation where the appointment stays Approved during pending).
- **Supervisor can MODIFY the reschedule date** before accepting (per spec line 511): supervisor picks a different slot than user requested; must enter a reason via `AdminReScheduleReason` field. Strict parity: implement this.
- **Owner OR accessor with Edit access** can request reschedule.
- **Reschedule reason required**, rejection reason populated by supervisor.
- **Calendar window** restricts which dates user can pick (per spec line 425): "Calendar will show only the dates as per the window specified by the IT Admin in system parameters." This is the `AppointmentMaxTime*` settings.
- **`IsBeyondLimit` admin override** -- IT Admin can reschedule beyond normal max-time windows.

## OLD code map

Same as cancellation (shared file).

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentChangeRequestDomain.cs` | `Add` (request submit, both cancel + reschedule), `Update` (supervisor accept/reject, both flows), validation, `UploadRescheduleDocument`, `SendEmailData` |
| `patientappointment-portal/.../appointment-change-requests/{add,detail,delete,edit,view}/...` | UI |

## NEW current state

Same as cancellation (shared entity).

- `AppointmentStatusType.RescheduleRequested = 12` -- exists as enum value. Strict parity says USE this for interim state during reschedule pending review.
- `OriginalAppointmentId` field on Appointment -- TO VERIFY presence; not in CLAUDE.md's listed Appointment fields, so likely missing. **Needs to be added.**
- Reschedule reason / rejection reason fields -- TO VERIFY presence on Appointment + AppointmentChangeRequest.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Submit-reschedule endpoint | `POST /api/appointments/{id}/AppointmentChangeRequests` with `AppointmentStatusId = RescheduleRequested` | NEW: TO VERIFY | **Add `RequestRescheduleAsync(Guid appointmentId, RequestRescheduleDto)` AppService method** | B |
| `RequestRescheduleDto` | `{ NewDoctorAvailabilityId, ReScheduleReason, AppointmentStatusId, IsBeyondLimit, SupportingDocuments[] }` | -- | **Add DTO** | B |
| Lead time + max time gates (mirror booking) | OLD validation | NEW: TO VERIFY in reschedule path | **Add gates** | B |
| `IsBeyondLimit` admin override | OLD: when set, validation requires beyond-limit date | NEW: missing | **Add `IsBeyondLimit` bool flag** + admin-only validation path | I |
| Interim status `RescheduleRequested = 12` | Used by OLD on Appointment | NEW: enum exists; verify Appointment can transition to it | **Use it** in reschedule flow on submit | B |
| New slot transitions: Available -> Reserved (submit) -> Booked (approve) | OLD has 3-step | NEW: 2-step (Available -> Booked); Reserved missing | **Add `Reserved` to BookingStatus** + transitions | B |
| New Appointment row on approve | OLD: insert with same confirmation # + new slot + status=Approved | NEW: TO VERIFY this flow | **Add `ApproveRescheduleAsync` method** that creates new Appointment row, copies all child entities | B |
| Cascade-copy child entities | OLD: PatientAttorneys, DefenseAttorneys, InjuryDetails (with sub), EmployerDetails, Accessors, CustomFieldsValues, Documents (TO VERIFY) | NEW: TO BUILD | **Implement cascade-copy** -- factor into `IAppointmentManager.CloneForRescheduleAsync(Appointment, newSlotId)` | B |
| `OriginalAppointmentId` field | OLD field on Appointment | NEW: missing | **Add `OriginalAppointmentId Guid?` FK** on Appointment entity | B |
| `ReScheduleReason`, `ReScheduledById`, `RescheduleRejectionReason` | OLD fields | NEW: missing | **Add fields** | B |
| `AdminReScheduleReason` field | OLD field for supervisor override on reschedule date | NEW: missing | **Add field** | I |
| Old slot release on approve | OLD: TO VERIFY (likely sets Available) | NEW: known gap (slot stays Booked) | **Release old slot** | B |
| New slot Reserved on submit, Booked on approve | OLD has this | NEW: TO BUILD | **Implement** | B |
| Supervisor can modify reschedule date before accepting | OLD: spec line 511 + `AdminReScheduleReason` field | NEW: TO VERIFY | **Allow supervisor to override `DoctorAvailabilityId` in approve method**; require `AdminReScheduleReason` if changed | I |
| Calendar window in UI | OLD: shows only dates per max-time window | NEW: TO VERIFY (Add page has `minimumBookingDays = 3` only) | **Apply max-time window in slot picker** | I |
| Notifications (Email + SMS) on submit / approve / reject | OLD: yes | NEW: `CancellationRescheduleReminderJob` exists; verify covers reschedule events | **Add `RescheduleRequestedEto`, `RescheduleApprovedEto`, `RescheduleRejectedEto` events** | I |
| Owner OR accessor with Edit access | OLD: per UI | NEW: TO VERIFY | **Apply `IAppointmentAccessPolicy.CanEditAsync`** | B |

## Internal dependencies surfaced

- `SystemParameters.AppointmentMaxTimePQME/AME/OTHER`, `AppointmentLeadTime`
- Doctor availability slots (Staff Supervisor sets these up)
- Email + SMS templates per event
- Supervisor approve/reject UX (separate slice)

## Branding/theming touchpoints

- Email templates per event (submit / approve / reject)
- SMS templates per event
- Reschedule UI (calendar picker, reason input, supporting docs upload)

## Replication notes

### ABP wiring

- **Cascade-copy implementation:** new method on `AppointmentManager` -- `CloneForRescheduleAsync(Guid oldAppointmentId, Guid newSlotId, AppointmentStatusType outcome)`. Uses repository to deep-copy. Returns new Appointment.
- **Slot management:** `IDoctorAvailabilityRepository.SetStatusAsync(Guid slotId, BookingStatus newStatus)`. Watch for race conditions on concurrent reschedules of overlapping slots.
- **State machine guards** prevent invalid transitions (e.g., reschedule-approve can only happen from Pending change request).
- **Supervisor override** of reschedule date: require `AdminReScheduleReason` not null when `appointmentChangeRequest.DoctorAvailabilityId != originalRequest.DoctorAvailabilityId`.
- **Events:** `AppointmentRescheduleRequestedEto`, `AppointmentRescheduleApprovedEto { OldAppointmentId, NewAppointmentId, OldSlotId, NewSlotId }`, `AppointmentRescheduleRejectedEto`.

### Things NOT to port

- `IsBeyodLimit` typo -> `IsBeyondLimit`.
- Stored proc reads.
- `vSystemParameter` view -> direct entity query.
- The OLD pattern of mutating `oldAppointment` in-place rather than treating it as a state machine (NEW should use the state machine method on Appointment).

### Verification (manual test plan)

1. Owner submits reschedule on Approved appointment with reason + new slot -> success, change request created, appointment -> RescheduleRequested, new slot -> Reserved
2. Owner submits without reason -> rejected
3. Owner submits with old slot (no change) -> rejected (or accepted, TO DEFINE -- but OLD likely accepts and supervisor handles)
4. Owner submits with unavailable slot -> rejected
5. Owner submits beyond max-time window -> rejected (unless `IsBeyondLimit` + admin)
6. Supervisor approves with `Outcome = RescheduledNoBill` -> NEW appointment row created, same conf #, status Approved; old -> RescheduledNoBill; old slot -> Available; new slot -> Booked; all stakeholders emailed + SMS with new date
7. Supervisor approves with date override + `AdminReScheduleReason` -> new slot is supervisor's pick, reason recorded
8. Supervisor rejects -> appointment back to Approved, new slot -> Available, requester emailed
9. Reschedule child entities: verify InjuryDetails, attorneys, employer, accessors, custom fields all replicated to new appointment row
10. Cascade-copy preserves OriginalAppointmentId chain across multiple reschedules

Tests:
- `AppointmentManagerTests.CloneForRescheduleAsync_CopiesAllChildEntities`
- `RescheduleApprovedEvent_PublishesAllStakeholderEmails`
- `RescheduleSubmit_NoReason_Throws`
- `RescheduleSubmit_UnavailableSlot_Throws`
- `RescheduleApprove_OldSlotReleased_NewSlotBooked`
- `Reschedule_StateMachine_RejectsInvalidTransition`
- Synthetic data only.

## Phase 11c annotations [2026-05-04]

> **Phase 11c slice -- scalar clone helper extracted; child-entity
> cascade is Phase 11c-extended.** Phase 17 (change-request approval)
> consumes this helper; the helper is in place so 17 can focus on the
> orchestration (status transitions on source row, slot transitions,
> AppointmentChangeRequest row updates) instead of inlining the entity
> copy.

| Aspect | OLD source | NEW Phase 11c status |
|--------|-----------|----------------------|
| Scalar field copy on reschedule approve | `AppointmentChangeRequestDomain.cs` Update reschedule path | [IMPLEMENTED 2026-05-04 - pending testing] -- `AppointmentRescheduleCloner.BuildScalarClone` (Application/Appointments/) builds a fresh Appointment with: copied scalars (Patient/IdentityUser/AppointmentType/Location FKs, PanelNumber, DueDate, IsPatientAlreadyExist, InternalUserComments, party emails, PrimaryResponsibleUserId), new DoctorAvailabilityId + AppointmentDate, status forced to Approved, AppointmentApproveDate recomputed, OriginalAppointmentId pointing at source. Excludes ReScheduleReason / ReScheduledById -- those describe the change request, not the result. 12 boundary-case unit tests. |
| Same-confirmation-# reuse | OLD reuses confirmation # so end user sees one identifier across the lifecycle | [IMPLEMENTED 2026-05-04 - pending testing] -- `sameConfirmationNumber` flag on `BuildScalarClone`; defaults to reuse, override path requires explicit value. |
| Child-entity cascade (InjuryDetails / BodyParts / ClaimExaminers / PrimaryInsurances, EmployerDetails, ApplicantAttorney, DefenseAttorney, Accessors, CustomFieldValues, Documents) | OLD `AppointmentChangeRequestDomain.cs` lines 800+ | [DESCOPED 2026-05-04 - Phase 11c-extended] -- the per-entity copy logic is mechanical but touches 7+ child entities; Phase 17 will land this when it consumes the cloner. |
| AppointmentManager.CreateRescheduleCloneAsync orchestration (load source, build clone, persist, status transitions on source) | -- | [DESCOPED 2026-05-04 - Phase 17] -- this is the supervisor-side orchestration; Phase 17 owns the state-machine transitions on the source row + slot status updates. |
