# 02. Approval / reschedule / cancellation -- OLD vs NEW behavioral parity
> OLD = P:\PatientPortalOld (intent/behavior source of truth). NEW = this repo.
> Exhaustive re-read 2026-05-29. We replicate intent + behavior, not code/features.

## Coverage

### OLD reviewed
- `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsController.cs` (Put/Patch -> approve/reject/cancel)
- `PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentChangeRequestsController.cs`
- `PatientAppointment.Api\Controllers\Api\AppointmentChangeLog\AppointmentChangeLogsController.cs`
- `PatientAppointment.Api\Controllers\Api\AppointmentChangeLog\AppointmentChangeLogsSearchController.cs`
- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs` (UpdateValidation:312-404, Update:406-573, UpdateDoctorAvailbilty:586-611, SendEmail:883-1050)
- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentChangeRequestDomain.cs` (AddValidation:65-195, Add:197-223, Update:231-622, UpdateDocument:896-914)
- `PatientAppointment.Domain\AppointmentChangeLogModule\AppointmentChangeLogDomain.cs` (full)
- `PatientAppointment.DbEntities\Models\AppointmentChangeRequest.cs`, `AppointmentChangeLog.cs`
- `PatientAppointment.Models\Enums\AppointmentStatusType.cs`, `PatientAppointment.DbEntities\Enums\RequestStatus.cs`
- Angular: `appointment-change-requests\{add,edit,view}\*.component.ts`, `appointments\view\appointment-view.component.{ts,html}`, `appointment-change-log\...\list\appointment-change-log-list.component.ts`

### NEW reviewed
- `Domain\Appointments\AppointmentManager.cs` (state machine + transitions), `Appointment.cs`
- `Application\Appointments\AppointmentsAppService.Approval.cs`, `AppointmentApprovalValidator.cs`, `AppointmentRescheduleCloner.cs`
- `Domain\AppointmentChangeRequests\AppointmentChangeRequestManager.cs`, `AppointmentChangeRequest.cs`, `CancellationRequestValidators.cs`, `RescheduleRequestValidators.cs`
- `Application\AppointmentChangeRequests\AppointmentChangeRequestsAppService.cs`, `AppointmentChangeRequestsAppService.Approval.cs`, `ChangeRequestApprovalValidator.cs`
- Contracts: `ApproveAppointmentInput`, `RejectAppointmentInput`, `ApproveCancellationInput`, `ApproveRescheduleInput`, `RejectChangeRequestInput`, `GetChangeRequestsInput`
- HttpApi: `AppointmentApprovalController.cs`, `AppointmentChangeRequestApprovalController.cs`, `AppointmentChangeRequestController.cs`
- Enums: `RequestStatusType.cs`, `AppointmentStatusType.cs` (13-value)
- Angular: `appointment\components\{approve-confirmation-modal,reject-appointment-modal}.component.ts`, `appointment-change-logs\appointment-change-logs.component.ts`, `app.routes.ts` (change-log route)

## Summary
| Class | Count |
|---|---|
| Missing behavior | 3 |
| Partial behavior | 3 |
| Intent deviation | 2 |
| Equivalent (different implementation) | 12 |
| OLD-bug (do not port) | 2 |

## Behavioral gaps (decide)

### G-02-01 -- Global change-log list page with field-level filters is missing
- **Class:** Missing
- **OLD:** `appointment-change-log\appointment-change-logs\list\appointment-change-log-list.component.ts` + `AppointmentChangeLogsController.cs` + `spm.spAppointmentChangeLogs` SP. A standalone, internal-only **searchable list** of every field-level change across ALL appointments, filterable by `fieldName`, `oldValue`, `newValue`, `tableName`, `appointmentStatusName`, `changedDate`, `requestConfirmationNumber`, paged + sortable (default sort `RequestConfirmationNumber`). Row click -> opens the appointment.
- **NEW:** `angular\src\app\appointments\appointment-change-logs\appointment-change-logs.component.ts` -- per-appointment ONLY (route `appointments/view/:id/change-log`, reads route `:id`). Calls ABP `AuditLogsService.getEntityChangesWithUsername` for a single `Appointment` entity id. No global list, no cross-appointment filters, no `tableName`/`fieldName`/`confirmationNumber` search.
- **What it is:** OLD has both a per-appointment view and a global filterable audit grid; NEW has only the per-appointment drill-down.
- **Why it existed:** Staff/supervisors used the global grid to answer "who changed what claim number last Tuesday" without knowing the appointment up front.
- **What it does / user impact:** Internal users lose the ability to search the audit trail by field/value/date across appointments. They must know the specific appointment first.
- **Plain-English:** OLD had a master "history of all edits" page you could search; NEW only shows the history of one appointment at a time.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-02-02 -- Change log does not capture child-entity (injury / body-part / claim) field changes
- **Class:** Partial
- **OLD:** `AppointmentChangeLogDomain.cs` `ChangeLogs` (90-101), `ChangeLogsForBodyParts` (114-124), `ChangeLogForInjuryDetails` (327-333), `AddNewLogForBodyPart` (252-266). OLD reflects over `vAppointmentsForChangeLog` AND over injury-detail / body-part rows, writing a log row per changed field with `TableName` (e.g. "Appointment Injury Body Part Detail"), `FieldName`, `OldValue`, `NewValue`, `ChangedById`, `IsInternalUserUpdate`, `IsMailSent`. Body-part additions log a row with empty `OldValue`.
- **NEW:** `appointment-change-logs.component.ts:39` hardcodes `entityTypeFullName = '...Appointments.Appointment'` -- it queries ABP entity-changes for the **Appointment aggregate root only**. Child entities (`AppointmentInjuryDetail`, `AppointmentBodyPart`, `AppointmentClaimExaminer`, `AppointmentPrimaryInsurance`) carry `[Audited]`? Not confirmed audited, and even if audited they are not surfaced by this viewer (single FQN filter).
- **What it is:** OLD's change log spans the appointment + all injury/claim child tables; NEW's viewer is scoped to the root entity's own scalar properties.
- **Why it existed:** Claim numbers, dates of injury, body parts are the legally-significant fields most often edited post-booking; OLD tracked them explicitly.
- **What it does / user impact:** Edits to claim/injury/body-part data after booking are not visible in the NEW change-log viewer.
- **Plain-English:** If staff change a claim number or injury detail, OLD recorded it in the history; NEW's history screen would not show that change.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-02-03 -- "Intake-form-changed" notification email (per-field diff table) is absent
- **Class:** Missing
- **OLD:** `AppointmentChangeLogDomain.cs` `SendEmailForIntakeFormChanges` (334-378) + `SendEmailForChangeLog` (267-311). After any `Appointment.Update`, OLD collects all change-log rows with `IsMailSent==false`, builds an HTML table of `FieldName / OldValue / NewValue`, emails it to stakeholders via `EmailTemplate.AppointmentChangeLogs`, then flips `IsMailSent=true`. A separate `EmailTemplate.AppointmentRescheduleRequestByAdmin` fires once if a Date/Time field changed (`SendEmailForAppointmentTimeChangedByAdmin`, 312-326).
- **NEW:** No equivalent. `AppointmentManager.UpdateAsync` does not diff fields or send a change-summary email; no `IsMailSent` flag exists on any NEW entity. (Note: emails are area 04's scope, but the gating mechanism -- the change-log diff + `IsMailSent` dedup -- is change-log behavior and has no NEW home.)
- **What it is:** OLD's update path emails affected parties a diff of what changed on the intake form; NEW's update path is silent.
- **Why it existed:** Attorneys / adjusters needed to be notified when staff edited a confirmed appointment's intake data.
- **What it does / user impact:** Stakeholders are no longer auto-notified of post-booking intake-form edits.
- **Plain-English:** OLD emailed everyone a "here's what changed" table when staff edited an appointment; NEW sends nothing.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later  (cross-ref area 04 for the email send; the diff/dedup logic is the gap here)

### G-02-04 -- Reschedule-reject does not release the user-picked (Reserved) slot
- **Class:** Intent deviation
- **OLD:** `AppointmentChangeRequestDomain.cs` Update, `ReScheduleRejectionReason` branch (591-617): on reschedule reject, old slot -> `Booked` (595), **new slot -> `Available` (600)**, change request's `DoctorAvailabilityId` reset to `OldDoctorAvailabilityId`, appointment -> `Approved`. Net: the slot the patient requested is freed for others.
- **NEW:** `AppointmentChangeRequestsAppService.Approval.cs` `RejectRescheduleAsync` (341-403): reverts appointment -> `Approved`, marks request Rejected, publishes status ETO -- but **explicitly removed `ReleaseSlotIfReservedAsync`** (381-385 comment) per the 2026-05-15 slot-rework. The new slot that `SubmitRescheduleAsync` set to `Reserved` (manager line 256) stays `Reserved` after reject.
- **What it is:** OLD freed the requested slot on reject; NEW leaves it `Reserved`.
- **Why it existed:** A rejected reschedule means the patient is NOT moving, so the slot they tentatively grabbed should return to the pool.
- **What it does / user impact:** Under NEW, a rejected reschedule leaves the requested slot stuck in `Reserved`, blocking other bookers (capacity-aware booking treats `Reserved` as manually-closed, NOT held-pending). The submit path still sets `Reserved` (manager 256) but no path clears it -- an orphaned hold. This is internally inconsistent: submit reserves, but neither approve nor reject releases.
- **Plain-English:** When staff reject a reschedule, the time the patient had tentatively grabbed stays blocked instead of going back up for grabs.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later  (NOTE: likely a real bug introduced by the slot-rework; submit still Reserves but nothing un-Reserves on reject/approve)

### G-02-05 -- Internal-user direct cancellation from the appointment view page is missing
- **Class:** Missing
- **OLD:** `appointments\view\appointment-view.component.html` (137-139) + `.ts updateAppointmentRequest` + `AppointmentDomain.Update` (537-550). The single view-page modal lets an internal user directly set an Approved appointment to `CancelledNoBill` with a cancellation reason -- NOT through a change request. `Update` then auto-creates an `AppointmentChangeRequest` with `RequestStatusId=Accepted, IsBeyodLimit=false` as an audit record and frees the slot (`UpdateDoctorAvailbilty` 602-603).
- **NEW:** The view-page modals are `approve-confirmation-modal` and `reject-appointment-modal` only. Direct cancel-from-view is absent. Cancellation in NEW only happens via the external-user change-request submit (`RequestCancellationAsync`) followed by a supervisor approve (`ApproveCancellationAsync`). There is no one-step internal-user cancel of an Approved appointment.
- **What it is:** OLD let staff cancel an approved appointment in one action from the detail page; NEW requires the two-step change-request lifecycle.
- **Why it existed:** Staff who learn an appointment must be cancelled (no patient request) needed to cancel it directly.
- **What it does / user impact:** Staff cannot directly cancel an approved appointment; they would have to fabricate a change request. No code path writes `CancelledNoBill` outside the change-request approval.
- **Plain-English:** In OLD, staff could cancel an approved appointment straight from its page; in NEW there's no staff-initiated cancel button -- it only works if a patient/attorney requests it first.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-02-06 -- Cancel-time window gate not re-applied on supervisor approval; outcome is manual-only
- **Class:** Partial
- **OLD:** `AppointmentChangeRequestDomain.AddValidation` (83-91) checks `AppointmentCancelTime` at **submit** time only; the supervisor's edit modal then lets them pick `CancelledNoBill` OR `CancelledLate` manually (`appointment-change-request-edit.component.ts` 125-157). The two statuses are a pure manual choice -- OLD does NOT auto-derive from the window.
- **NEW:** Manual choice is faithfully ported (`ApproveCancellationInput.CancellationOutcome`, validated to NoBill/Late by `ChangeRequestApprovalValidator.EnsureCancellationOutcome`). The cancel-time window gate is applied at submit (`AppointmentChangeRequestManager.SubmitCancellationAsync` 130-136, `CancellationRequestValidators.IsWithinNoCancelWindow`). **Behaviorally equivalent** for the manual-outcome part. The PARTIAL flag is narrow: OLD's submit gate used `(slotDate - DateTime.Today).TotalDays < cancelTime` with `DateTime.Today` (local server time); NEW mirrors it but uses `DateTime.Today` at the Application/Domain boundary -- consistent, but verify tenant-timezone handling once multi-tenant lands. No functional gap today.
- **What it is:** Outcome selection + window gate are ported; only timezone-anchor robustness is unverified.
- **Why it existed:** Late cancellations (inside the window) are billable; the supervisor decides NoBill vs Late.
- **What it does / user impact:** None observed in single-tenant Phase 1. Flagged for timezone verification only.
- **Plain-English:** The "cancel too late = billable" rule and the manual NoBill/Late choice both carried over; only the date-math timezone needs a sanity check later.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

### G-02-07 -- Approval injury-detail gate has no OLD equivalent (NEW is stricter)
- **Class:** Intent deviation
- **OLD:** `AppointmentDomain.UpdateValidation` approve branch (312-344) only blocks if already Approved/Rejected/etc. There is NO "must have >=1 injury detail" gate on approval.
- **NEW:** `AppointmentManager.ApplyTransitionAsync` (236-241, BUG-043/T8) blocks Pending->Approved when `_appointmentInjuryDetailRepository.GetCountAsync(appointmentId) < 1`, throwing `AppointmentApprovalRequiresInjuryDetail`.
- **What it is:** NEW adds a domain invariant OLD never had.
- **Why it existed:** Documented as BUG-043 -- a defensive guard against approving an appointment with zero claim rows.
- **What it does / user impact:** NEW will refuse to approve an appointment that OLD would have approved (one with no injury details). Likely intentional hardening, but it is a behavior change from OLD ground truth.
- **Plain-English:** NEW won't let staff approve an appointment that has no claim/injury info; OLD allowed it.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later  (confirm with Adrian that the added gate is desired hardening, not a parity regression)

### G-02-08 -- Patient-match override is recorded but not acted on (no patient-row split)
- **Class:** Partial
- **OLD:** `AppointmentDomain.Add` (203-218) + view modal "Patient record has been merged with existing patient" block. On the dedup path, OLD sets `IsPatientAlreadyExist=true` and links the existing `PatientId`; the approve flow shows the merged-patient card.
- **NEW:** `AppointmentApprovalValidator.ShouldOverridePatientMatch` (100-107) + `AppointmentsAppService.Approval.cs` (113-123): when staff send `OverridePatientMatch=true` on an appointment with `IsPatientAlreadyExist=true`, NEW only flips `IsPatientAlreadyExist=false` and logs it. The comment explicitly defers "the actual patient-row split (creating a new Patient row + relinking)" to a future Session-A manager rewrite. The Angular approve modal hardcodes `overridePatientMatch: false` (`approve-confirmation-modal.component.ts:149`) -- so the override is unreachable from the UI today.
- **What it is:** The override decision is recorded on a flag but the downstream effect (split into a new patient) is not implemented, and the UI never sends `true`.
- **Why it existed:** Dedup matching at booking sometimes wrongly merges two distinct patients; staff need to un-merge at approval.
- **What it does / user impact:** Staff cannot actually un-merge a wrongly-matched patient at approval; the flag flips but no new Patient row is created. Plus the UI cannot even trigger it.
- **Plain-English:** If two different patients got merged at booking, OLD's approve screen could split them; NEW records the intent but does not split, and the button to do it isn't wired up.
- **Keep in NEW?** ( ) Yes  ( ) No  ( ) Decide later

## Equivalent -- different implementation (no action)

- **Status state machine vs public setter + `IsStatusUpdate` flag.** OLD enforced transitions via `UpdateValidation` idempotency checks + `IsStatusUpdate/IsInternalUserUpdateStatus` flags on a generic PUT. NEW uses a `Stateless` state machine in `AppointmentManager.BuildMachine` (Pending->Approved/Rejected, Approved->Cancel/Reschedule/NoShow/CheckIn, etc.). Same allowed transitions; NEW is cleaner. Idempotency ("Appointment Already Approved/Rejected") preserved by `AppointmentApprovalValidator.EnsureApprovable/EnsureRejectable`.
- **Approve persistence: responsible user + comments.** OLD batched `PrimaryResponsibleUserId` + `InternalUserComments` into the status PATCH; NEW persists them in `ApproveAppointmentAsync` before firing the manager transition. Both stamp `AppointmentApproveDate` (OLD line 455; NEW manager 248). Responsible-user dropdown sourced from internal roles in both (`internalUserNameLookUps` -> `GetInternalUserLookupAsync`).
- **Reject persistence: rejection notes + RejectedById.** OLD via PATCH; NEW writes both in `RejectAppointmentAsync` AND `AppointmentManager` reject branch (257-258). Required-notes gate preserved (`EnsureRejectable`).
- **Reschedule submit reserves new slot + flips appointment to RescheduleRequested.** OLD `AppointmentChangeRequestDomain.Add` (202-218); NEW `SubmitRescheduleAsync` (256-264). Same intent.
- **Reschedule approval creates a NEW Appointment row, same confirmation #, cascade-copies children.** OLD Update reschedule-accept block (322-549) vs NEW `ApproveRescheduleAsync` + `AppointmentRescheduleCloner` + `CloneChildEntitiesAsync` (466-581). NEW clones injury/body-part/claim-examiner/primary-insurance/employer/applicant-attorney/defense-attorney/accessor/custom-field-value/ad-hoc-document -- a SUPERSET of OLD (OLD missed nothing material; both reuse the same conf#).
- **Reschedule chain linkage.** OLD copied `OriginalAppointmentId` from the source (propagating null on first reschedule); NEW sets `OriginalAppointmentId = source.Id` (points to direct parent). NEW is a slight improvement (auditable chain); user-visible outcome equivalent. Not a gap.
- **Admin-modify-date on reschedule approval.** OLD `appointment-change-request-edit.component.ts` `adminReScheduleReason` + `requestedDoctorAvailabilityId` (132-177) vs NEW `ApproveRescheduleInput.OverrideSlotId` + `AdminReScheduleReason` + `ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason`. Same gate: override slot requires admin reason. Persisted on the change-request row (`AdminOverrideSlotId`, `AdminReScheduleReason`) in both.
- **`IsBeyodLimit` (typo preserved in OLD) -> `IsBeyondLimit`.** OLD's beyond-max-time override flag is carried on the change request and lifts the per-type gate. NEW preserves on `AppointmentChangeRequest.IsBeyondLimit` + threads through the cloner (120). Field-name typo corrected per branch policy (clear typo, not behavior).
- **Lead-time + per-AppointmentType max-time re-check on reschedule submit.** OLD `AddValidation` reschedule branch (116-193, with IsBeyodLimit dual-path); NEW `AppointmentChangeRequestsAppService.RequestRescheduleAsync` (110-117) reuses `BookingPolicyValidator` -- same gates as booking flow.
- **Cancellation request: cutoff-days + reason required.** OLD `AddValidation` (83-95); NEW `SubmitCancellationAsync` (109-136) + entity-ctor `Check.NotNullOrWhiteSpace`. Equivalent.
- **Cancellation approval: manual NoBill/Late + slot release.** OLD Update cancel-accept (263-291, slot->Available 275); NEW `ApproveCancellationAsync` writes terminal status + `CancelledById`, slot freed via capacity-aware model (Rejected/Cancelled* don't count toward capacity per Appointments CLAUDE.md). Outcome-equivalent.
- **Cancellation reject reverts to Approved.** OLD `CancellationRejectionReason` branch (294-306, appointment->Approved); NEW `RejectCancellationAsync` -- parent stayed Approved during Pending (Phase 15 design), so no revert needed; net state identical (Approved).
- **`[Audited]` on Appointment + AppointmentChangeRequest instead of custom AppointmentChangeLog table.** ABP audit-logging records `Old/New/ChangedBy/ChangeTime` per property for the root entity. The per-appointment viewer (`appointment-change-logs.component.ts`) renders this. Outcome-equivalent FOR ROOT-ENTITY SCALAR fields only (the child-entity + global-list + email gaps are tracked separately above as G-02-01/02/03).
- **Change-request pending list + filters.** OLD `spm.spAppointmentChangeRequests` SP (statusTypeId, requestTillDate, searchQuery) vs NEW `GetPendingChangeRequestsAsync` + `ChangeRequestListFilter.Apply` (requestStatus/type/created-range). LINQ vs SP; equivalent.

## OLD bugs (do not port)

- **Hardcoded test emails in reschedule/cancel notification.** `AppointmentChangeRequestDomain.SendEmailData` line 769: commented-but-present `appointmentStackHoldersEmailPhone.EmailList = "surbhi.acharya@radixweb.com,hardikgiri.goswami@radixweb.com";`. Developer test addresses. NEW correctly publishes ETOs to a notification handler instead. Do not port.
- **`AppointmentChangeRequestsController.Patch` blind `ApplyTo` with no existence check.** `AppointmentChangeRequestsController.cs` (64-70): `FindByKey(id)` then `ApplyTo` with no null-guard -> null-ref if the id is bogus. NEW uses explicit `GetAsync` (throws `EntityNotFoundException`). Do not port the fragile pattern.

## Open questions / could-not-verify

- **G-02-04 (slot-on-reject):** Confirm with Adrian whether leaving the requested slot `Reserved` after a reschedule reject is intended under the capacity-aware model. The submit path still flips the slot to `Reserved` (`AppointmentChangeRequestManager.cs:256`) but NEITHER approve nor reject clears it -- this looks like an orphaned-hold bug the slot-rework introduced. If `Reserved` now means "manually closed by doctor's-admin," then submit should NOT be setting it either.
- **G-02-02 (child-entity audit):** Could not confirm whether `AppointmentInjuryDetail` / `AppointmentBodyPart` / `AppointmentClaimExaminer` carry `[Audited]`. Even if they do, the per-appointment viewer filters on the `Appointment` FQN only, so child changes are not surfaced regardless. Verify whether area 03/10 expects child-entity audit.
- **G-02-07 (injury-detail approval gate):** NEW is stricter than OLD. Confirm this is intended hardening (BUG-043) and not a parity regression.
- **Reschedule reject `RejectionNotes` single-field mapping:** OLD persisted `CancellationRejectionReason` and `ReScheduleRejectionReason` as distinct columns on the change-request row; NEW collapses both into one `RejectionNotes` field. Outcome-equivalent for display, but if any OLD report distinguishes the two reason types, that distinction is lost. Flag for area 08 (reporting).
