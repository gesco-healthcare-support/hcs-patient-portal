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
| Submit-reschedule endpoint | `POST /api/appointments/{id}/AppointmentChangeRequests` with `AppointmentStatusId = RescheduleRequested` | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- new `IAppointmentChangeRequestsAppService.RequestRescheduleAsync(appointmentId, RequestRescheduleDto)`. Controller route `POST api/app/appointment-change-requests/reschedule/{appointmentId}`. | B |
| `RequestRescheduleDto` | `{ NewDoctorAvailabilityId, ReScheduleReason, AppointmentStatusId, IsBeyondLimit, SupportingDocuments[] }` | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- DTO with `[Required] NewDoctorAvailabilityId`, `[Required][StringLength] ReScheduleReason`, optional `IsBeyondLimit`. SupportingDocumentIds is deferred to a Phase 14 follow-up (the document-upload integration is Session B territory). | B |
| Lead time + max time gates (mirror booking) | OLD validation | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- AppService calls the existing Phase 11b `BookingPolicyValidator.ValidateAsync` against the new slot's `AvailableDate` + the appointment's `AppointmentTypeId`. Same throws as the booking flow (`AppointmentBookingDateInsideLeadTime` / `AppointmentBookingDatePastMaxHorizon`). | B |
| `IsBeyondLimit` admin override | OLD: when set, validation requires beyond-limit date | -- | [PARTIALLY IMPLEMENTED 2026-05-04] (Phase 16) -- entity field `IsBeyondLimit` already exists on `AppointmentChangeRequest` (Phase 1.5). DTO carries the field; Manager threads it through to the entity constructor. The admin-side INVERSION of the lead/max-time gate (require date BEYOND max horizon when admin sets IsBeyondLimit=true) is intentionally NOT implemented in the external-user submit path -- it's a Phase 17 supervisor-side concern. External-user submits with IsBeyondLimit=true today still validate forward-direction; the field stores the user's intent for the supervisor's downstream consumption. | I |
| Interim status `RescheduleRequested = 12` | Used by OLD on Appointment | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- new `AppointmentManager.RequestRescheduleAsync(id, reason, actingUserId)` transition method composes the existing state-machine trigger `RequestReschedule` (already in `BuildMachine`'s `Approved -> RescheduleRequested` permit). The change-request manager calls this after persisting the request row. | B |
| New slot transitions: Available -> Reserved (submit) -> Booked (approve) | OLD has 3-step | NEW: 3-step now | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- `BookingStatus.Reserved` enum value already exists (NEW Phase 1). `AppointmentChangeRequestManager.SubmitRescheduleAsync` directly transitions the new slot Available -> Reserved via `IRepository<DoctorAvailability,>.UpdateAsync`. The OLD slot stays Booked while the request is Pending; release happens in Phase 17 supervisor approve. The Available -> Booked promotion (on supervisor approve) is also Phase 17. | B |
| New Appointment row on approve | OLD: insert with same confirmation # + new slot + status=Approved | -- | [DESCOPED - Phase 17 Session B] -- supervisor-side flow; the cascade-clone helpers Phase 11c + 11j shipped feed this. | B |
| Cascade-copy child entities | OLD: PatientAttorneys, DefenseAttorneys, InjuryDetails (with sub), EmployerDetails, Accessors, CustomFieldsValues, Documents | NEW: helpers shipped Phase 11j | [IMPLEMENTED Phase 11c + 11j] -- per-entity static clone helpers in `AppointmentRescheduleCloner` (Application/Appointments/) ready for Phase 17's orchestrator to compose. | B |
| `OriginalAppointmentId` field | OLD field on Appointment | NEW: present (Phase 1.6) | [IMPLEMENTED PRIOR] -- `Appointment.OriginalAppointmentId Guid?` exists since Phase 1.6 (verified at the entity definition). | B |
| `ReScheduleReason`, `ReScheduledById`, `RescheduleRejectionReason` | OLD fields | NEW: present | [IMPLEMENTED PRIOR] -- `Appointment.ReScheduleReason`, `Appointment.ReScheduledById`, `Appointment.RejectionNotes` (the change-request's rejection-side equivalent). `AppointmentChangeRequest` carries its own `ReScheduleReason` + `RejectionNotes` for the per-request fields. | B |
| `AdminReScheduleReason` field | OLD field for supervisor override on reschedule date | NEW: present | [IMPLEMENTED PRIOR] -- `AppointmentChangeRequest.AdminReScheduleReason` exists (Phase 1.5); used only by Phase 17's supervisor approve override. | I |
| Old slot release on approve | OLD: sets Available | NEW: known gap (slot stays Booked today) | [DESCOPED - Phase 17 Session B] -- the OLD slot release happens on supervisor approve, not on submit. The submit-side flow leaves the OLD slot at Booked (parity with OLD line 207-209). | B |
| New slot Reserved on submit | OLD has this | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- see "New slot transitions" row above. | B |
| Supervisor can modify reschedule date before accepting | OLD: spec line 511 + `AdminReScheduleReason` field | -- | [DESCOPED - Phase 17 Session B] -- supervisor approve concern; Phase 17 owns the override + AdminReScheduleReason capture. | I |
| Calendar window in UI | OLD: shows only dates per max-time window | -- | [DESCOPED - Phase 11d UI follow-up] -- UI-side concern; backend gate already enforces the same window via `BookingPolicyValidator`. | I |
| Notifications (Email + SMS) on submit | OLD: yes | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- Manager publishes the existing `AppointmentChangeRequestSubmittedEto` (Phase 18 stub) with `ChangeRequestType.Reschedule`. Email/SMS handler subscribers are Session B's Phase 18 territory; the event surface is in place. The `AppointmentManager.RequestRescheduleAsync` transition also publishes the existing `AppointmentStatusChangedEto`. | I |
| Owner OR accessor with Edit access | OLD: per UI | -- | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 16) -- AppService composes Phase 13a's `AppointmentAccessRules.CanEdit` predicate (shared with Phase 15's cancel submit). View accessors rejected; Edit accessors allowed; internal users bypass. | B |

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
| Child-entity cascade (InjuryDetails / BodyParts / ClaimExaminers / PrimaryInsurances, EmployerDetails, ApplicantAttorney, DefenseAttorney, Accessors) | OLD `AppointmentChangeRequestDomain.cs` lines 800+ | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 11j) -- `AppointmentRescheduleCloner` extended with 8 per-entity static clone helpers (`CloneInjuryDetailFor`, `CloneBodyPartFor`, `CloneClaimExaminerFor`, `ClonePrimaryInsuranceFor`, `CloneEmployerDetailFor`, `CloneApplicantAttorneyFor`, `CloneDefenseAttorneyFor`, `CloneAccessorFor`). Each helper preserves every scalar field (back-fills the post-construction ones), generates a fresh Id, re-points the parent FK, and assigns the supplied tenant. 10 unit tests cover scalar-preservation + parent-FK repointing + null-source rejection. The fetch + persist orchestration around the helpers lives in Phase 17 (change-request approval AppService). AppointmentTypeFieldConfigValue + AppointmentDocument cascade is deferred to Phase 14, where the document-specific Status preservation rule (per OLD) drives a different code path. |
| AppointmentManager.CreateRescheduleCloneAsync orchestration (load source, build clone, persist, status transitions on source) | -- | [DESCOPED 2026-05-04 - Phase 17] -- this is the supervisor-side orchestration; Phase 17 owns the state-machine transitions on the source row + slot status updates. |
