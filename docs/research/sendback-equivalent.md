# OLD-App Equivalent of NEW's "SendBack" Feature

**Date:** 2026-05-04
**Branch:** `feat/replicate-old-app`
**Scope:** Settle Open Question O5 (clinic-staff send-back-for-info flow)
**Verdict:** OLD has **no send-back lifecycle**. The closest analog is a free-text
`InternalUserComments` field that staff fills in **only when approving** an
appointment, plus `RejectionNotes` populated on rejection. There is **no
"return to booker for correction" workflow** at all.

---

## 1. Executive Answer

OLD does **not** have a send-back / awaiting-more-info / RFI flow under any
name. Its appointment status enum has 13 values (verbatim what NEW now
mirrors), none of which represent "needs more info from booker." The only
adjacent mechanism is two free-text columns on the `Appointment` row:

- `InternalUserComments` (NVARCHAR(250)) -- typed by staff in the **Approval**
  modal; emitted into the booker's "Appointment Approved" email but never
  prompts a re-edit.
- `RejectionNotes` (NVARCHAR(500)) -- typed by staff in the **Rejection**
  modal; emitted into the booker's "Appointment Rejected" email. Rejection
  is a terminal state in OLD; the booker would have to file a **new**
  appointment to "fix" anything.

OLD's correction paths for an already-submitted appointment are:
1. **Approve with a comment** (passive correction: staff types the issue
   into `InternalUserComments`, approves anyway, and the booker sees the
   note in their approval email -- but cannot re-edit the appointment).
2. **Reject with a reason** (terminal: booker files new request).
3. **AppointmentChangeRequest** (post-approval: separate entity for
   reschedule/cancel; not a send-back -- triggered by booker, not staff,
   and only after Approved status).

There is no path where staff says "this is wrong, fix it and resubmit"
and the same row cycles through `Pending -> AwaitingMoreInfo -> Pending`.
NEW's SendBack feature is genuinely **NEW-only**, with no OLD ancestor.

---

## 2. OLD's Flow (best-fit "correction" path, not a true send-back)

OLD has no send-back. The closest workflow is the staff-comment-on-approve
path, walked here for completeness:

1. Booker submits appointment via `appointment-add.component.ts`. Status
   defaults to `Pending=1` (`AppointmentStatusType.cs:11`).
   - File: `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts:562`
   - `internalUserComments: null` is set in the form group (booker cannot
     write to it; backend ignores booker-sent values).

2. Staff opens **Pending** appointment list -> clicks an appointment ->
   **AppointmentView modal** opens (`appointment-view.component.html:1-156`).

3. Modal lets staff pick a target status from a dropdown. The form
   conditionally renders different fields based on the **target** status:
   - If target = `Approved=2`: shows "Responsible User" select +
     "Any comments?" textarea bound to `internalUserComments`
     (`appointment-view.component.html:124-131`, `141-144`).
   - If target = `Rejected=3`: shows "Write Rejection Reason" textarea
     bound to `rejectionNotes` (`appointment-view.component.html:132-135`).
   - If target = `CancelledNoBill=5`: shows cancellation reason textarea
     (`appointment-view.component.html:136-139`).

4. Staff submits via `updateAppointmentRequest()`
   (`appointment-view.component.html:148-149`). This issues a JSON-Patch
   `replace` operation against the `Appointment` row, e.g.
   `[{ "op": "replace", "path": "/internalUserComments", "value": "..." }]`
   (sample in `Documents_and_Diagrams\Postman Collection\Patient Appointment API.postman_collection.json:5728`).

5. **Backend processing** (`AppointmentDomain.cs:923-983`): the handler
   switches on `statusId`:
   - For `Approved` and `internalUserUpdateStatus == true` (staff-driven
     update), it pulls `vemailSender.InternalUserComments` and renders the
     `Patient-Appointment-ApprovedInternal.html` template, prefixed with
     `<b> Staff comments for an appointment: </b>` and emailed to the
     booker (`AppointmentDomain.cs:960-966`).
   - For `Approved` and `internalUserUpdateStatus == false`, it uses
     `Patient-Appointment-ApprovedExternal.html` with a `Please note:`
     prefix (`AppointmentDomain.cs:974-980`).
   - For `Rejected`, it renders `Patient-Appointment-Rejected.html` with
     `RejectionNotes` (`AppointmentDomain.cs:984-991`).

6. **End of flow.** Status is now Approved or Rejected. The booker reads
   the email but **cannot re-edit the appointment**. No status returns to
   Pending. No table tracks "field corrections needed."

**Critical:** there is no step where staff says "data is wrong, please
fix it" and the booker re-edits the same appointment. Staff either rolls
forward (Approve with comment) or terminates (Reject).

---

## 3. OLD's Data Model

### Appointment table columns relevant to "comments / notes"

Source: `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Appointment.cs`

| Column | Line | Type | Purpose |
|---|---|---|---|
| `InternalUserComments` | 56-58 | NVARCHAR(250) | Staff free text shown in approval email |
| `RejectionNotes` | 90-92 | NVARCHAR(500) | Staff rejection reason shown in rejection email |
| `CancellationReason` | 29-31 | NVARCHAR(500) | Cancellation reason |
| `ReScheduleReason` | 105-107 | NVARCHAR(500) | Reschedule reason |
| `AppointmentStatusId` | 110-116 | FK | Status enum (no AwaitingMoreInfo value) |

Schema confirms: `_local\generated-schema.sql:413` -- `[InternalUserComments] nvarchar(250) NULL`.

### What's **not** present in OLD

- No `SendBackInfo` / `MoreInfoRequest` / `RFI` table.
- No `MissingFields` / `FlaggedFields` / `RequestedFields` JSON column.
- No `IsResolved` / `ResolvedAt` / `SentBackBy` columns anywhere.
- No status transition that returns to Pending after staff review.

**Search exhaustion:** zero non-vendor hits across all of:
`SendBack`, `sendback`, `AwaitingMoreInfo`, `RequestInfo`, `MoreInfo`,
`InfoRequired`, `AdditionalInfo`, `ReturnToCreator`,
`RequestForInformation`, `RFI`, `Returned`, `PendingInfo`, `MissingInfo`,
`RequestMoreInfo`, `AskForInfo`, `InformationRequested`, `MissingFields`,
`RequestedFields`, `FlaggedFields`, `MissingFieldNotes`, `StaffComments`,
`MoreInfoNotes`. (Twilio's vendored `MoreInfo` exception field and
MimeKit's `AttachAdditionalInfo` constants are unrelated noise.)

---

## 4. OLD's Status State Machine

Source: `P:\PatientPortalOld\PatientAppointment.Models\Enums\AppointmentStatusType.cs`

```csharp
public enum AppointmentStatusType
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    NoShow = 4,
    CancelledNoBill = 5,
    CancelledLate = 6,
    RescheduledNoBill = 7,
    RescheduledLate = 8,
    CheckedIn = 9,
    CheckedOut = 10,
    Billed = 11,
    RescheduleRequested = 12,
    CancellationRequested = 13,
}
```

### Transitions (verbatim from OLD)

| From | To | Trigger | Notes |
|---|---|---|---|
| (none) | Pending | Booker submits new request | `appointment-add` |
| Pending | Approved | Staff approves (`InternalUserComments` may be set) | `AppointmentDomain.cs:953` |
| Pending | Rejected | Staff rejects (`RejectionNotes` required) | `AppointmentDomain.cs:984` |
| Approved | CheckedIn | Staff checks patient in | `AppointmentDomain.cs:992` |
| CheckedIn | CheckedOut | Staff checks patient out | `AppointmentDomain.cs:1004` |
| Approved | NoShow | Patient no-show | `AppointmentDomain.cs:1016` |
| CheckedOut | Billed | Billing job | not relevant here |
| Approved | RescheduleRequested | Booker submits change request | separate entity |
| Approved | CancellationRequested | Booker submits cancel request | separate entity |
| RescheduleRequested | RescheduledNoBill / RescheduledLate | Staff approves reschedule | clones row |
| CancellationRequested | CancelledNoBill / CancelledLate | Staff approves cancel | terminal |

**No transition exists where a non-terminal status returns to Pending.**
The only way "back to Pending" is for the booker to file a brand-new
appointment request after rejection.

---

## 5. OLD's Notifications

Source: `P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs`

```csharp
public enum TemplateCode
{
    AppointmentBooked = 1,
    AppointmentApproved = 2,
    AppointmentRejected = 3,
    AppointmentCancelledRequest = 4,
    AppointmentCancelledRequestApproved = 5,
    AppointmentCancelledRequestRejected = 6,
    AppointmentRescheduleRequest = 7,
    AppointmentRescheduleRequestApproved = 8,
    AppointmentRescheduleRequestRejected = 9,
    RejectedPackageDocument = 12,
    RejectedJoinDeclarationDocument = 13,
    AppointmentDueDate = 14,
    AppointmentDueDateUploadDocumentLeft = 15,
    SubmitQuery = 16,
    AppointmentApprovedStackholderEmails = 17,
    AppointmentCancelledByAdmin = 18
}
```

**No template** for "send back," "needs more info," "info requested," or
"please correct."

The two templates that surface staff comments:

- `Patient-Appointment-ApprovedInternal.html` -- contains
  `##InternalUserComments##` placeholder (line 52). Sent when staff
  approves with a comment.
- `Patient-Appointment-ApprovedExternal.html` -- same placeholder,
  different recipient context.
- `Patient-Appointment-Rejected.html` -- contains `##RejectionNotes##`
  (line 51). Sent on rejection.

The `AppointmentChangeRequestDomain.cs` flow uses templates 4-9 for
post-approval reschedule/cancel correspondence, **not** for
send-back-style staff requests.

---

## 6. OLD's UX (booker-facing and staff-facing)

### Staff sees (Pending appointment review)

`appointment-view.component.html:124-144`:
```html
<div class="form-group" *ngIf="appointmentDetail.appointmentStatusId == appointmentStatus.Approved">
  <label class="form-label">Responsible User</label>
  <select class="custom-select" formControlName="primaryResponsibleUserId">...</select>
</div>
<div class="form-group" *ngIf="appointmentDetail.appointmentStatusId == appointmentStatus.Rejected">
  <label class="form-label">Write Rejection Reason</label>
  <textarea class="form-control" rows="5" formControlName="rejectionNotes" placeholder="Rejection reason"></textarea>
</div>
<div class="form-group" *ngIf="appointmentDetail.appointmentStatusId == appointmentStatus.Approved">
    <label class="form-label">Any comments?</label>
    <textarea class="form-control" rows="3" formControlName="internalUserComments" placeholder="Please enter comments regarding this appointment"></textarea>
</div>
```

The status dropdown drives which fields appear. There is no "Send Back"
button anywhere -- staff's only choices are Approve, Reject, or
Cancel/Reschedule (the latter two are pre-existing-status-only).

### Booker sees

- **In the appointment edit form** (`appointment-edit.component.ts:189`):
  `internalUserComments` is loaded into a local `internalUserComment`
  variable when the page boots. It is **read-only** for the booker --
  shown for context but never editable. There is no "highlighted field"
  rendering, no banner ("your appointment needs more info"), no
  resubmit button conditional on a "needs-info" status.
- **In email** (`Patient-Appointment-ApprovedExternal.html:52`): the
  `##InternalUserComments##` placeholder renders staff's note inline
  with the approval body.

### What's missing vs NEW's SendBack

| Capability | OLD | NEW (deleted) |
|---|---|---|
| Staff flags specific fields | NO | YES (92 fields, 9 sections) |
| Staff writes a note | YES (in Approved email only) | YES (modal textarea) |
| Booker sees highlighted fields | NO | YES (banner pills) |
| Booker can edit + resubmit same appointment | NO | YES |
| Status reverts to Pending after fix | NO | YES (auto on resubmit) |
| Multiple send-back rounds | NO | YES (multiple rows in `AppAppointmentSendBackInfos`) |
| Persisted record of the request | NO (just a 250-char text column) | YES (full audited entity) |

---

## 7. NEW's Current SendBack Feature (reconstructed from git history)

### Status: DELETED

Removed by commit **`d1bbdab`** (`chore(domain): remove Doctor user role
and AppointmentSendBackInfo`, 2026-05-03). Commit message reads:

> Drops AppointmentSendBackInfo and the SendBack / SaveAndResubmit flows
> because OLD has no send-back lifecycle -- Pending appointments only
> transition to Approved or Rejected; corrections route through cancel /
> reschedule per OLD spec.

The deletion commit was made by Adrian on the parity branch with the
reasoning that OLD has no equivalent. **This research confirms that
reasoning is correct on the OLD-parity axis** -- but Adrian has since
re-asked the question, hinting the UX value alone may justify keeping
it as parity-plus.

### What the deleted feature did

**Domain entity** (`src/.../Domain/Appointments/AppointmentSendBackInfo.cs`,
84 lines, deleted in d1bbdab):

```csharp
[Audited]
public class AppointmentSendBackInfo : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }
    public virtual Guid AppointmentId { get; set; }
    public virtual string FlaggedFieldsJson { get; set; } = "[]";
    public virtual string? Note { get; set; }
    public virtual DateTime SentBackAt { get; set; }
    public virtual Guid? SentBackByUserId { get; set; }
    public virtual bool IsResolved { get; set; }
    public virtual DateTime? ResolvedAt { get; set; }
    // ... ctor + GetFlaggedFields() + MarkResolved()
}
```

**Input DTO** (`SendBackAppointmentInput.cs`, deleted):
```csharp
public class SendBackAppointmentInput
{
    public List<string> FlaggedFields { get; set; } = new();
    [StringLength(2000)]
    public string? Note { get; set; }
}
```

**EF migration** (`20260428003045_Added_AppointmentSendBackInfo.cs`,
applied 2026-04-28, dropped 2026-05-02 by
`20260502001639_Drop_AppointmentSendBackInfo.cs`): created table
`AppAppointmentSendBackInfos` with:
- `Id`, `TenantId`, `AppointmentId`, `FlaggedFieldsJson` (NVARCHAR(MAX)),
  `Note` (NVARCHAR(2000)), `SentBackAt`, `SentBackByUserId`,
  `IsResolved`, `ResolvedAt`
- FK to `AppAppointments(Id)`
- Index on `AppointmentId`
- Full ABP audit columns (CreationTime/CreatorId/etc.)

**Status enum** had a 14th value: `AwaitingMoreInfo = 14`. Removed in
the same cleanup; the current `AppointmentStatusType.cs` comment at
lines 4-7 documents the removal.

**Frontend** (3 files, all deleted in d1bbdab):

1. `angular/src/app/appointments/appointment/send-back-fields.ts` --
   single source of truth for flaggable fields. **92 fields across 9
   sections**:
   - Section 1: **Patient Demographics** (20 fields, W1)
   - Section 2: **Employer Detail** (7 fields, W1)
   - Section 3: **Applicant Attorney** (11 fields, W1)
   - Section 4: **Authorized Users** (5 fields, W1)
   - Section 5: **Appointment Details** (7 fields, W1)
   - Section 6: **Defense Attorney** (11 fields, W2)
   - Section 7: **Patient Injury / Claim Details** (7 fields, W2)
   - Section 8: **Insurance Carrier** (10 fields, W2)
   - Section 9: **Claim Adjuster** (10 fields, W2)
   - Helper `buildFlaggedFieldLookup()` -- builds key->section map for
     booker pill rendering.

2. `send-back-appointment-modal.component.ts` -- standalone Angular 20
   modal. Renders 9 collapsible accordion panels of checkboxes + a
   2000-char note textarea. Submit enabled when `>=1 field flagged
   OR note non-empty`. Calls `appointmentService.sendBack(id, input)`,
   emits `succeeded` event, closes modal.

3. `send-back-appointment-modal.component.html` -- markup for the modal.

**API endpoints** (from current proxy
`angular/src/app/proxy/appointments/appointment.service.ts`,
**still present** -- proxy is stale per F19):

- `POST /api/app/appointments/{id}/send-back` -- body: `SendBackAppointmentInput`,
  returns `AppointmentDto` (transitions `Pending -> AwaitingMoreInfo`).
- `GET /api/app/appointments/{id}/send-back-info/latest` -- returns
  `AppointmentSendBackInfoDto | null` (latest unresolved row, drives
  booker banner pills).

**Booker-side UI** (still partially wired in
`angular/src/app/appointments/appointment/components/appointment-view.component.ts`,
per F13 build-blocker):

- Lines 14-15, 31, 34: imports `AppointmentSendBackInfoDto`,
  `SendBackAppointmentModalComponent`, `buildFlaggedFieldLookup`.
- Line 36: `type TransitionAction = 'approve' | 'reject' | 'sendBack';`
- Lines 124-128: `sendBackModalVisible`, `latestSendBackInfo`,
  `flaggedFieldsCache`.
- Line 329: `this.maybeLoadLatestSendBackInfo(...)` on detail load.
- Lines 396-402: `flaggedFieldsCache` Set built from
  `latestSendBackInfo.flaggedFields` for O(1) field-key lookup
  during input rendering (field shows red border when flagged).
- Lines 465-472: action-state machine -- Pending allows
  `[approve, reject, sendBack]`; AwaitingMoreInfo allows `[approve, reject]`
  (sendBack would be redundant).
- Lines 498-502: builds the booker-facing list of flagged-field pills
  from the same `latestSendBackInfo.flaggedFields`.

This is the "shrapnel" referenced by F13 in
`docs/research/stage-0b-build-blockers.md`. Stage 0a is supposed to
strip it; Stage A1 is supposed to reintroduce send-back if O5 = keep.

---

## 8. Mapping Table OLD <-> NEW

| Concern | OLD | NEW SendBack (deleted) | Recommendation |
|---|---|---|---|
| Status `Pending` | int 1 | Guid 1 | KEEP (parity) |
| Status `AwaitingMoreInfo` | (none) | int 14 (deleted) | **NEW-ONLY** -- if kept, parity-flag |
| Staff free-text note (post-action) | `Appointment.InternalUserComments` (250 char, NVARCHAR) | `AppointmentSendBackInfo.Note` (2000 char) | RENAME if kept; consider keeping OLD's column for approval-comment use case (different lifecycle) |
| Staff free-text note (pre-correction) | (none) | `AppointmentSendBackInfo.Note` | NEW-ONLY |
| Flagged-field list | (none) | `AppointmentSendBackInfo.FlaggedFieldsJson` | NEW-ONLY |
| Persisted send-back history | (none) | `AppAppointmentSendBackInfos` table | NEW-ONLY |
| Email template "send back" | (none) | (was being designed; never shipped a template) | NEW-ONLY -- need to design |
| Booker banner / highlighted fields | (none -- read-only `internalUserComments` echoed in email) | banner pills + red-border inputs in `appointment-view` | NEW-ONLY |
| Booker resubmit | (none -- file new request) | edit + Save -> auto-transition to Pending | NEW-ONLY |

**Adjacent OLD mechanisms that NEW does NOT replace by deleting SendBack:**

- `Appointment.InternalUserComments` (250-char, on Appointment) -- shown
  in approval email. NEW currently has no equivalent. If SendBack stays
  deleted, NEW must add this column for parity (audit ref:
  `docs/parity/clinic-staff-appointment-approval.md` should already
  cover it -- needs re-check).
- `Appointment.RejectionNotes` (500-char, on Appointment) -- shown in
  rejection email. Same parity status as above.

---

## 9. Recommendation for O5

**Option chosen: KEEP NEW's SendBack as parity-plus, with a `_parity-flags.md` row.**

Reasoning:

1. **No OLD ancestor exists.** Exhaustive search across status enum,
   schema, domain code, Angular components, email templates, postman
   collection, and architecture docs confirms zero send-back equivalent.
   Strict-parity rule alone would say "drop it."

2. **But strict parity is not the only criterion.** The branch
   `CLAUDE.md` explicitly allows "parity-plus" features when they fix
   genuine UX gaps and are flagged. OLD's correction story is broken:
   staff who notice missing data can either (a) approve-with-comment
   (booker can't edit, has to remember the note) or (b) reject (booker
   files an entirely new appointment, losing all docs and history).
   Send-back fills a real workflow gap.

3. **Ambiguous-bug-or-design rule favors keeping it.** Per CLAUDE.md
   "Bug and deviation policy," when behavior is genuinely unclear we
   replicate AND flag. The inverse applies here: when a NEW feature
   fills an OLD workflow gap and was already partially built, deleting
   it again risks repeating the back-and-forth (Apr 28 add -> May 2 drop
   -> May 4 reconsider). Lock in the decision now.

4. **Implementation cost is small.** The entity, migration, DTOs,
   endpoint, modal, and field registry all exist in commit `d1bbdab^`
   and can be cherry-picked. The only outstanding work is:
   - Re-add `AwaitingMoreInfo = 14` to `AppointmentStatusType.cs` (and
     update the comment).
   - Re-create the EF migration (or revert the drop migration -- but a
     fresh additive migration is cleaner).
   - Re-add the entity, DTOs, AppService method, controller route.
   - Re-add the modal + field registry to Angular.
   - Add a `_parity-flags.md` row.
   - Add an email template for the booker notification (NEW-only).
   - Document in a new `docs/parity/clinic-staff-send-back.md` audit doc
     marking it parity-plus with rationale.

5. **Alternative considered: merge into ChangeRequest.** OLD's
   `AppointmentChangeRequest` is post-approval-only (covers reschedule
   + cancel). Forcing send-back into it would either (a) require
   `AppointmentChangeRequestType` to gain a new "InfoRequest" value the
   OLD enum doesn't have (still parity-plus, just buried in a different
   entity) or (b) break the post-approval invariant (send-back fires
   pre-approval). Verdict: separate entity is cleaner. Reject.

6. **Alternative considered: fold note into `InternalUserComments`
   only, drop the field flagging.** This loses 80% of the UX value
   (which is precisely the highlighted-fields UX). The whole point of
   send-back over a free-text comment is targeted feedback. Reject.

---

## 10. Implementation Impact

### Plan tasks affected (`docs/plans/2026-05-04-appointment-lifecycle-parity-fixes.md`)

| Task | Current state | Change |
|---|---|---|
| **G0a** Strip dangling SendBack refs | Pending | **Modify scope.** Don't strip -- restore. The 33 refs in `appointment-view.component.ts` and 52 in the HTML are real consumers of the feature, not dead code. Replace this task with **G0a': Restore SendBack feature components.** |
| **A1** Clinic staff approval UI + send-back modal | Pending | **Confirm scope.** A1 already lists send-back per O5; once O5 = keep, A1 reintroduces the modal + handler. Verify the deleted modal's 92-field registry is still appropriate after W1/W2 cap landings (Section 6-9 wave gating logic in `send-back-fields.ts:25-39` may now be partially obsolete). |
| **F13** Build-blocker: dangling SendBack refs | Open | **Resolution path changes.** Instead of stripping refs to unblock the build, restore the deleted modal + send-back-fields module. F13 closes when the restoration lands. |
| **F19** AppointmentStatusType drift | Open | **Resolves to: stale enum is correct -- fix the enum and proxy together.** Re-add `AwaitingMoreInfo = 14` to `AppointmentStatusType.cs`, update the comment to say "kept as parity-plus per O5 / sendback-equivalent.md," regenerate the Angular proxy, F19 closes. |
| **C6** Phase 17 cascade-clone gap (CustomFieldsValues + AppointmentDocuments) | Pending | **No change.** Cascade-clone is a separate concern from send-back; reschedule clones the row, send-back does not. |

### Research docs needing addendum

1. `docs/research/stage-0b-build-blockers.md` -- F13 row needs to flip
   from "strip refs" to "restore deleted feature." F19 row needs the
   AwaitingMoreInfo decision recorded.
2. `docs/research/stage-5-approval.md` -- Add a "Send-back: kept as
   parity-plus" subsection citing this doc as the rationale.
3. `docs/parity/_parity-flags.md` -- Add a row:
   - Feature: Clinic-staff send-back-for-info
   - OLD source: (none -- NEW-only feature)
   - Description: 14th status value `AwaitingMoreInfo`, 92-field flag
     registry, 2000-char note, persisted history table.
   - Status: `kept-parity-plus`
   - Rationale: Fills OLD UX gap; see `docs/research/sendback-equivalent.md`.
4. New file: `docs/parity/clinic-staff-send-back.md` -- audit doc per
   the unaudited-features protocol in branch CLAUDE.md.

### Decision-Log entry (recommended)

Add to `06-Work-Context/Decision-Log.md`:
- Date: 2026-05-04
- Decision: Restore NEW's SendBack feature as parity-plus (reverse the
  2026-05-03 deletion in commit d1bbdab).
- Rejected alternative 1: Drop entirely (matches OLD strict parity but
  loses real UX value -- OLD's correction story is broken).
- Rejected alternative 2: Fold into AppointmentChangeRequest (forces
  pre-approval lifecycle on a post-approval entity -- worse than
  keeping a separate entity).
- Trigger to revisit: if Adrian decides during multi-tenant Phase 2
  that the field-flag registry maintenance burden outweighs the UX
  benefit, drop send-back and revert to the OLD pattern.

---

## Source Citations Index

OLD app:
- `P:\PatientPortalOld\PatientAppointment.Models\Enums\AppointmentStatusType.cs` (full file)
- `P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs` (full file)
- `P:\PatientPortalOld\PatientAppointment.Models\ViewModels\vEmailSenderViewModel.cs:33-34`
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\Appointment.cs:56-58, 90-92`
- `P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs:923-991`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\view\appointment-view.component.html:124-150`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\edit\appointment-edit.component.ts:189`
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\add\appointment-add.component.ts:562`
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\Patient-Appointment-ApprovedExternal.html:52`
- `P:\PatientPortalOld\PatientAppointment.Api\wwwroot\EmailTemplates\Patient-Appointment-ApprovedInternal.html:52`
- `P:\PatientPortalOld\_local\generated-schema.sql:413, 425, 531, 594, 617, 696`

NEW app (current state):
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.Domain.Shared\Enums\AppointmentStatusType.cs` (full file, 25 lines)
- `W:\patient-portal\replicate-old-app\angular\src\app\proxy\appointments\appointment.service.ts:227-251` (stale proxy still has sendBack methods)
- `W:\patient-portal\replicate-old-app\angular\src\app\proxy\appointments\models.ts:91-105` (stale proxy DTOs)
- `W:\patient-portal\replicate-old-app\angular\src\app\appointments\appointment\components\appointment-view.component.ts:14-15, 31, 34, 36, 124-128, 329, 396-402, 465-472, 498-502`
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.EntityFrameworkCore\Migrations\20260428003045_Added_AppointmentSendBackInfo.cs` (full file)
- `W:\patient-portal\replicate-old-app\src\HealthcareSupport.CaseEvaluation.EntityFrameworkCore\Migrations\20260502001639_Drop_AppointmentSendBackInfo.cs` (full file)

NEW app (deleted -- recoverable from git):
- Commit `d1bbdab` -- `chore(domain): remove Doctor user role and AppointmentSendBackInfo`
- Pre-deletion files: `git show d1bbdab^:<path>` for all 6 listed files
  in section 7 above.
