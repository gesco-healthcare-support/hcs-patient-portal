---
feature: staff-supervisor-change-request-approval
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs (Update + supervisor flows)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-change-requests\
old-docs:
  - socal-project-overview.md (lines 503-523)
  - data-dictionary-table.md (AppointmentChangeRequests, AppointmentChangeRequestDocuments, RequestStatus)
audited: 2026-05-01
re-verified: 2026-05-04
status: in-progress
priority: 2
strict-parity: true
internal-user-role: StaffSupervisor (also ITAdmin per permission matrix)
depends-on:
  - external-user-appointment-cancellation     # external user side of cancel
  - external-user-appointment-rescheduling     # external user side of reschedule
required-by: []
---

# Staff Supervisor -- Change request approval (cancel + reschedule)

## Purpose

Staff Supervisor (and IT Admin) reviews each Pending `AppointmentChangeRequest` (cancellation OR reschedule) submitted by external users. Decides accept (with outcome bucket) or reject (with notes). On accept: appointment status transitions, slots updated, notifications fired. On reject: appointment reverts to Approved, requester notified.

**Strict parity with OLD.** This audit consolidates the supervisor side of both cancel + reschedule flows; the external-user side audits cover the request-submission side.

## OLD behavior (binding)

### Pending change requests UI

Staff Supervisor opens the "Change Requests" list page (per OLD `appointment-change-requests/list/`). Filters: AppointmentId, StatusTypeId (Cancel or Reschedule), date range, search.

Each row: appointment confirmation #, requester, type, reason, submitted date, current status (`RequestStatus.Pending` / `Accepted` / `Rejected`).

### Accept-cancellation flow

(Detailed in `external-user-appointment-cancellation.md`. Recap from supervisor side:)

1. Supervisor selects outcome: `CancelledNoBill` or `CancelledLate` (based on whether request is within `AppointmentCancelTime` of the appointment date).
2. Click Accept.
3. Backend (`AppointmentChangeRequestDomain.Update`):
   - `oldAppointment.AppointmentStatusId = CancelledNoBill or CancelledLate`
   - Slot released: `BookingStatusId = Available`
   - `AppointmentChangeRequest.RequestStatusId = Accepted`
   - All stakeholders emailed + SMS'd.

### Reject-cancellation flow

1. Supervisor enters `CancellationRejectionReason` (required).
2. Backend:
   - Appointment status reverts to `Approved`.
   - `AppointmentChangeRequest.RequestStatusId = Rejected`, `CancellationRejectionReason` saved.
   - Requester emailed with notes.

### Accept-reschedule flow

(Detailed in `external-user-appointment-rescheduling.md`. Recap:)

1. Supervisor selects outcome: `RescheduledNoBill` or `RescheduledLate`.
2. **Optional:** modify the reschedule date before accepting (uses `AdminReScheduleReason` field; required if changing).
3. Click Accept.
4. Backend creates **NEW Appointment row** with same `RequestConfirmationNumber` + new slot + status `Approved`. Old appointment status -> `RescheduledNoBill / Late`. Old slot released, new slot Booked. All child entities (attorneys, injuries, etc.) deep-copied to new row.
5. Stakeholders emailed + SMS'd with new date.

### Reject-reschedule flow

1. Supervisor enters `ReScheduleRejectionReason` (required).
2. Backend:
   - Appointment status reverts to `Approved`.
   - New slot released (Reserved -> Available).
   - `AppointmentChangeRequest.RequestStatusId = Rejected`.
   - Requester emailed with notes.

### Critical OLD behaviors

- **Single supervisor permission gates both flows.** No separate "approve cancel" vs "approve reschedule" permissions; Staff Supervisor (or IT Admin) handles both.
- **Outcome bucket is supervisor's call** -- there's no automatic "within X days = NoBill" rule; supervisor selects manually based on policy.
- **Supervisor can override the user's reschedule date** with `AdminReScheduleReason`.
- **Per OLD Update method**, both flows go through the same `AppointmentChangeRequestDomain.Update` -- branched by status type. Rejected requests do not delete the change request row -- it stays as historical audit.
- **Supporting documents** uploaded with the request (`AppointmentChangeRequestDocuments`) are visible to supervisor for context.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentChangeRequestDomain.cs` (1035 lines) | All accept/reject logic |
| `patientappointment-portal/.../appointment-change-requests/edit/...` | Accept/reject UI |
| `patientappointment-portal/.../appointment-change-requests/list/...` | Pending list UI |
| `Models.Enums.RequestStatus.{Pending, Accepted, Rejected}` | Request status enum |

## NEW current state

- `AppointmentChangeRequest` entity status: TO VERIFY presence in NEW `Domain/`.
- `Notifications/Jobs/CancellationRescheduleReminderJob.cs` exists -- handles reminder side, not the approval side.
- No accept/reject AppService visible from earlier globs.

## Gap analysis (strict parity)

(All gap items below are also captured in the cancellation + reschedule audits. This doc consolidates for the supervisor's view.)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Accept-cancellation endpoint | Implicit Update with RequestStatus=Accepted + outcome | -- | **Add `ApproveCancellationAsync(Guid changeRequestId, ApproveCancelDto { Outcome })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- Phase 17 ships `IAppointmentChangeRequestsApprovalAppService.ApproveCancellationAsync(id, ApproveCancellationInput)` on a NEW sibling AppService (`AppointmentChangeRequestsAppService.Approval.cs`). Sibling pattern (not partial) honors the user's "DO NOT edit the main file" directive; existing `AppointmentChangeRequestsAppService.cs` (Phase 15+16 submit endpoints) stays untouched. |
| Reject-cancellation endpoint | Implicit Update with CancellationRejectionReason | -- | **Add `RejectCancellationAsync(Guid changeRequestId, RejectDto { Reason })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- shipped on the same sibling AppService. |
| Accept-reschedule endpoint | Implicit Update | -- | **Add `ApproveRescheduleAsync(Guid changeRequestId, ApproveRescheduleDto { Outcome, OverrideSlotId?, AdminReason? })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- consumes Session A's Phase 11j `AppointmentRescheduleCloner` for the cascade-copy (scalar + every child entity). Handles admin-override-slot release + new-slot Booked promotion explicitly because the cascade handler does not know about an abandoned user-picked slot. |
| Reject-reschedule endpoint | Implicit Update | -- | **Add `RejectRescheduleAsync(Guid changeRequestId, RejectDto { Reason })`** | B | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- transitions parent appointment back to Approved, releases the Reserved new slot via direct repo write (cascade handler covers the parent's slot revert via the Approved status cascade). |
| Pending list endpoint with filters | OLD stored proc | -- | **Add `GetPendingChangeRequestsAsync(GetChangeRequestsInput)` with filters** | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- `GetPendingChangeRequestsAsync(GetChangeRequestsInput)` filters by `RequestStatus`, `ChangeRequestType?`, AppointmentTypeId?, FromDate?, ToDate?. Pure filter predicate `ChangeRequestListFilter.Apply` extracted as `internal static` for unit testability. |
| Outcome buckets are supervisor's call | OLD: free choice | -- | **Don't auto-derive from `AppointmentCancelTime`**; expose as DTO field | -- | `[VERIFIED 2026-05-04]` -- DTO accepts the outcome from the supervisor; no auto-derivation. |
| `AdminReScheduleReason` required when supervisor overrides | OLD: bool check | -- | **Validate**: if `OverrideSlotId != original`, `AdminReason` is required | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- `ChangeRequestApprovalValidator.EnsureAdminReasonWhenOverridingSlot(originalSlotId, overrideSlotId, adminReason)` throws `BusinessException(ChangeRequestAdminReasonRequired)` when the override differs from the user's pick AND the reason is null/whitespace. |
| New Appointment row on reschedule-approve with cascade-copy | OLD complex flow | -- | **Implement `AppointmentManager.CloneForRescheduleAsync`** -- detailed in reschedule audit | B | `[IMPLEMENTED 2026-05-04 - pending integration testing]` -- Session A's Phase 11j `AppointmentRescheduleCloner` (8 child-entity clone helpers) + the new approval-side orchestration in this AppService. Reuses Session A's helper rather than re-cloning -- per the session-split file-ownership rule, the cloner lives in `Application/Appointments/`. |
| Slot transitions: Reserved -> Booked (new) + Booked -> Available (old) on reschedule-approve; Reserved -> Available on reschedule-reject; Booked -> Available on cancel-approve | OLD | NEW: Reserved missing | **Add Reserved + transitions** | B | `[VERIFIED 2026-05-04]` `[IMPLEMENTED 2026-05-04]` -- `BookingStatus.Reserved` exists since Phase 1.7. Phase 17's flows publish `AppointmentStatusChangedEto` and Session A's `SlotCascadeHandler` flips slots per the T11 sync table (verified for cancel-approve via Approved -> CancelledNoBill mapping). The reschedule-override case + reschedule-reject's user-picked-slot release land via direct repo writes (cascade handler does not see "abandoned reserved slot"). |
| Notifications (Email + SMS) per event | OLD | NEW: missing | **Subscribe handlers** to ChangeRequestApproved / ChangeRequestRejected events | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- two new handlers in `Application/Notifications/Handlers/`: `ChangeRequestApprovedEmailHandler` + `ChangeRequestRejectedEmailHandler`. Each subscribes to the Phase-18-declared `AppointmentChangeRequestApprovedEto` / `AppointmentChangeRequestRejectedEto`, branches on `ChangeRequestType` (Cancel vs Reschedule) to pick the right `NotificationTemplateConsts.Codes.*` template (CancellationRequestAccepted / RescheduleApproved / CancellationRequestRejected / RescheduleRejected), resolves all stakeholders via Session A's `IAppointmentRecipientResolver`, dispatches via Phase 18's `INotificationDispatcher`. SMS leg stays deferred until `Volo.Abp.Sms` is wired (Phase 18.3). |
| Permissions | -- | -- | **`CaseEvaluation.AppointmentChangeRequests.{Default, Approve, Reject}`** -- Staff Supervisor + IT Admin | B | `[VERIFIED 2026-05-04]` -- keys exist (`CaseEvaluationPermissions.cs`:240-242), registered (`CaseEvaluationPermissionDefinitionProvider.cs`:131-133), granted to Staff Supervisor + IT Admin (Approve+Reject) and Clinic Staff (Default read-only) per `InternalUserRoleDataSeedContributor.cs`:223-225, 289-291, 341. |
| Concurrency: two supervisors approving same request | OLD: no guard visible | -- | **Add concurrency stamp** on `AppointmentChangeRequest`; reject second update | I | `[IMPLEMENTED 2026-05-04 - tested unit-level]` -- entity inherits ABP's `IHasConcurrencyStamp` via `FullAuditedAggregateRoot<Guid>`. AppService catches `AbpDbConcurrencyException` and re-throws as `BusinessException(ChangeRequestAlreadyHandled)`. Pre-flight check on `RequestStatus != Pending` raises the same code with the OLD-verbatim "This change request has already been processed" wording before the optimistic-concurrency gate fires (cleaner UX). |

## Internal dependencies surfaced

- Notification templates (covered by `it-admin-notification-templates.md`).
- Supporting docs upload (`AppointmentChangeRequestDocuments`) -- minor entity; CRUD pattern.

## Branding/theming touchpoints

- Approve/reject UI (logo, primary color, status-color indicators).
- Email templates per event.

## Replication notes

### ABP wiring

- **`IAppointmentChangeRequestsAppService`** with 4 methods + `GetPendingAsync`. `[RemoteService(IsEnabled = false)]`. Manual controller.
- **State machine on `AppointmentChangeRequest`**: Pending -> Accepted | Rejected.
- **State machine on `Appointment`** (separate entity): handles the secondary transitions (Approved -> RescheduleRequested, Approved -> CancelledLate, RescheduleRequested -> Approved on reject, etc.).
- **Cascade-copy** for reschedule-approve: in `AppointmentManager.CloneForRescheduleAsync(originalId, newSlotId)`. Copies all child entities. Tested separately.
- **Events:** `CancellationApprovedEto`, `CancellationRejectedEto`, `RescheduleApprovedEto`, `RescheduleRejectedEto`. All carry the relevant IDs + stakeholder emails.

### Things NOT to port

- `vSystemParameter` view -- direct entity read.
- Stored proc list endpoint.

### Verification (manual test plan)

1. External user submits cancel -> supervisor sees in pending list
2. Supervisor approves with `Outcome = CancelledNoBill` -> appointment status = CancelledNoBill, slot = Available, stakeholders notified
3. Supervisor rejects with notes -> appointment back to Approved, requester emailed
4. External user submits reschedule -> supervisor sees in pending list
5. Supervisor approves with no override -> NEW appointment row, same conf #, status Approved, new slot Booked, old slot Available, all stakeholders emailed with new date
6. Supervisor approves with `OverrideSlotId` + `AdminReason` -> picks supervisor's slot, AdminReason saved
7. Supervisor approves with `OverrideSlotId` but no `AdminReason` -> rejected (validation)
8. Supervisor rejects reschedule -> appointment back to Approved, new slot Available, requester emailed
9. Two supervisors approving same change request concurrently -> 1st succeeds, 2nd gets concurrency error
10. Cascade-copy verified: new appointment has all attorneys, injuries, employer, etc. matching old
