---
feature: Staff pick the reschedule date (epic phase 4b)
date: 2026-08-04
status: done
base-branch: main
related-issues: []
---

# Phase 4b -- staff pick the reschedule date

Epic tracker: `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md`.
Consumes phase 4a's `AvailabilityCalendarComponent`
(`docs/plans/2026-08-03-extract-availability-calendar.md`).

## Goal

The requestor asks for a reschedule with a REASON only, and internal staff choose the new
date and time using the same calendar and the same availability rules as booking -- on both
the reschedule request modal (when staff are the filer) and the live change-request inbox
approve modal.

## Context & decisions

Covers source items R1 ("calendar picker instead of a slot dropdown, same availabilities and
rules as booking") and the staff-picks half of R2 ("staff pick the new date, not the
requestor"). The consent half of R2 is phase 4c.

Why now: 4a extracted the calendar specifically so this phase could drop it in. The current
dropdown (`reschedule-request-modal.component.html:26-37`) lists every Available slot in a
hardcoded 90-day fetch with NO client-side lead-time or horizon gating, while the server
rejects out-of-policy picks at `AppointmentChangeRequestsAppService.cs:151`. R1 is therefore a
UI/server MISMATCH, not a missing rule: the user can pick something that then fails.

### Resolved decisions

- Decision: the calendar goes on BOTH surfaces -- the inbox approve modal and the reschedule
  request modal gated on `requesterIsStaff` -- because staff filing a reschedule already know
  the date they want, while an externally-filed reason-only request can only ever get a date
  at approval. Inbox-only would work (no auto-approve exists, so every request passes through
  it) but would force staff to file blind then re-open the inbox.
- Decision: 4b makes the requestor's slot optional server-side NOW rather than deferring to
  4c, because R1 and the staff-picks half of R2 are one coherent change and splitting them
  would ship a calendar the requestor still has to use.
- Decision: NO migration. `AppointmentChangeRequest.NewDoctorAvailabilityId` is ALREADY
  `Guid?` (`AppointmentChangeRequest.cs:54`); only code enforces the requirement.
- Decision: suppress consent issuance for reschedule submits in 4b, because consent is issued
  at submit today (`AppointmentChangeRequestsAppService.cs:164`) and a dateless request would
  email the opposing side asking them to consent to a reschedule with no date in it. 4c
  reintroduces consent as a round issued AFTER the staff date pick.
- Decision: run `BookingPolicyValidator` on the resolved slot inside `ApproveRescheduleAsync`,
  because the approve path never ran it -- a staff override already bypasses lead-time and the
  60/90-day horizon today, and 4b makes staff the ONLY picker, so the bypass becomes the
  primary path. Deny-by-default belongs server-side, not in the calendar's disabled days.
- Decision: enrich `AppointmentChangeRequestDto` with the appointment's location/type and the
  requested slot's date/time, populated set-based in `GetPendingChangeRequestsAsync`, because
  the inbox today has only a slot GUID -- it cannot feed the calendar OR display the requested
  date, which is why its detail panel tells staff to "Open the appointment to review the slot"
  (`internal-change-request-inbox.component.html:138-140`).
- Decision: delete the orphaned `change-request-{list,approve-modal,reject-modal}` trio in this
  phase as its own commit, because commit `35d10b7a` ("...replacing the two legacy per-type
  tables") superseded them on 2026-06-15, nothing references them, and they already misdirected
  this phase's research -- the epic doc line 160 named `ChangeRequestApproveModalComponent` as
  4b's insertion point, which no user can reach.
- Decision (Adrian, 2026-08-04, at plan approval): 4b and 4c DEPLOY TOGETHER as one release,
  because 4b deliberately leaves reschedule consent unissued and only 4c reissues it after the
  staff date pick. Neither phase is deployable alone. This is a release constraint, not a merge
  constraint -- each still merges to `main` on its own PR.

### Carried to phase 4c (NOT in 4b scope)

- Adrian, 2026-08-04: "consent emails only go once the staff selects a date" -- confirms 4c's
  design; the trigger is the staff date pick, not the submit.
- Adrian, 2026-08-04: "maybe we can add a button to send the emails once the staff has picked a
  date too" -- a manual staff-initiated "send consent request" action. Deferred to 4c because 4b
  issues no consent at all, so the button would have nothing to send. Open question for 4c's
  research: is the button the ONLY trigger, or a re-send alongside an automatic issue-on-pick?

## All needed context

### The live surfaces (verified 2026-08-04)

`CHANGE_REQUEST_ROUTES` (`change-request-routes.ts:14-23`) routes ONLY
`InternalChangeRequestInboxComponent`; `reschedules` and `cancellations` are
`redirectTo: ''`. `ChangeRequestListComponent` has zero references in `angular/src`, and it is
the only importer of the two `abp-modal` dialogs -- a closed island of 3 components.

Reschedule request modal hosts (all four already bind `locationId` + `appointmentTypeId`):

| Host                                         | Line | `requesterIsStaff`           |
| -------------------------------------------- | ---- | ---------------------------- |
| `internal-appointments.component.html`       | 372  | `true`                       |
| `internal-appointment-detail.component.html` | 1269 | `true`                       |
| `appointment-view.component.html`            | 150  | `isInternalUser`             |
| `external-appointment-detail.component.html` | 996  | omitted -> `false` (correct) |

There is NO auto-approve anywhere: B3 (2026-07-01) removed it
(`internal-appointments.component.ts:455-460`), so every reschedule -- staff-filed included --
is finalized through the inbox.

### The slot requirement, layer by layer

| Layer       | Anchor                                           | Current guard                                                      |
| ----------- | ------------------------------------------------ | ------------------------------------------------------------------ |
| DTO         | `RequestRescheduleDto.cs:18-19`                  | `[Required]` non-nullable `Guid`                                   |
| Manager     | `AppointmentChangeRequestManager.cs:215,231-234` | non-nullable param; `Guid.Empty` -> `ChangeRequestNewSlotRequired` |
| Entity ctor | `AppointmentChangeRequest.cs:154-158`            | `Check.NotNull` when type is Reschedule                            |
| Approve     | `ChangeRequestApprovalValidator.cs:91-96`        | raw `ArgumentException` -> HTTP 500                                |

Submit also holds the picked slot `Available -> Reserved`
(`AppointmentChangeRequestManager.cs:282-283`). Both release sites are already
`HasValue`-guarded (`...Approval.cs:252`, `:360`) so they tolerate null unchanged.

### The staff-override backend already exists

- `ApproveRescheduleInput.OverrideSlotId` + `AdminReScheduleReason`
  (`ApproveRescheduleInput.cs:36,45`).
- `ChangeRequestApprovalValidator.ResolveNewSlotAndEnsureAdminReason` (`:86-110`).
- `ApproveRescheduleAsync` consumes it (`...Approval.cs:213-219`), persists both fields
  (`:267-271`), and releases the requestor's held slot while landing on the override (`:252-261`).

### Two latent defects a null user-pick exposes

1. `isAdminOverride` (`...Approval.cs:218-219`) is `OverrideSlotId.HasValue && != NewDoctorAvailabilityId`.
   With a null user pick that is `true` for EVERY staff pick, so every requestor would get the
   override email -- "Reschedule request has been changed by our team"
   (`ChangeRequestApprovedEmailHandler.cs:155-156`) -- when they never proposed a date.
2. `ChangeRequestApprovedEmailHandler.cs:144-146` picks `slotId` from `AdminOverrideSlotId` only
   when `IsAdminOverride`, else `NewDoctorAvailabilityId`. With a null user pick and
   `isAdminOverride` corrected to false, `ResolveNewSlotAsync(null)` returns `("", "")`
   silently (`:181-184`) -- a BLANK-DATE approval email.

Both must be fixed here; neither is optional once the requestor stops picking.

### Server-side policy already in place

- `GetDoctorAvailabilityLookupAsync` (`DoctorAvailabilitiesAppService.cs:613-650`) applies the
  tenant lead time, filters `BookingStatus.Available`, and excludes full slots. The picker's
  list is already trimmed on the lower bound.
- `BookingPolicyValidator` is `ITransientDependency` in the Application layer
  (`BookingPolicyValidator.cs:30`) -- inject directly.
- `RequestRescheduleAsync` calls it role-aware today (`...AppService.cs:150-151`): external 60,
  internal 90.

### Calendar contract (phase 4a)

`AvailabilityCalendarComponent` (`availability-calendar.component.ts:67-102`). Inputs:
`locationId`, `appointmentTypeId`, `typeChosen`, `leadDays` (3), `ceilingDays` (90),
`selectedDate` (`YYYY-MM-DD` string), `selectedTime`, `dateInvalid`, `timeInvalid`,
`minimumBookingRuleMessage`, `dateLabel`, `timeLabel`, `noSlotsMessage`. Outputs:
`slotSelected: {date, time, doctorAvailabilityId}` and `dateCleared`.

It pins its own `NgbDateAdapter` (`:65`), which is what makes a non-booking host safe -- that
was 4a defect #6. `UsDateParserFormatter` is root-provided (`app.config.ts:182`), so display
formatting works inside a modal with no extra provider.

Pattern to mirror for embedding: `appointment-add-schedule.component.html:59-76` plus its
parent's `onSlotSelected`.

### Gotchas

- The datepicker is a POPUP with `[displayMonths]="2"` (~600px). `abp-modal` forwards its
  `options` input to `NgbModal` with no default size, so the dialog is Bootstrap's 500px
  default -- expect overflow. Set `[options]="{ size: 'lg' }"`. VERIFY LIVE.
- The calendar hardcodes `id="availability-calendar-date"` / `-time`
  (`availability-calendar.component.html:14,19,63,66`) and its own comment states the
  one-instance-per-page assumption. Keep it to ONE instance -- never render one per inbox row.
- `AppointmentChangeRequestDto` is also returned by `GetActiveForAppointmentAsync`
  (`...AppService.cs:175`), which will NOT populate the new fields. They are nullable and
  documented as populated only by the queue query.
- Mapperly needs `[MapperIgnoreTarget]` for every DTO field with no entity source, mirroring
  `AppointmentConfirmationNumber` (`CaseEvaluationApplicationMappers.AppointmentChangeRequests.cs:23,26`).
- `ChangeRequestNewSlotRequired` already exists (`CaseEvaluationDomainErrorCodes.cs:545`) --
  reuse it, do not add a code.
- Only ONE caller of `SubmitRescheduleAsync` exists (`...AppService.cs:156`); no test calls it.
- No spec exists for `reschedule-request-modal` or the inbox component. The established pattern
  is a pure `*.util.ts` + `*.util.spec.ts` (see `cr-inbox.util.spec.ts`).

## Tasks

### T1 -- make the requestor's slot optional on the wire

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentChangeRequests/RequestRescheduleDto.cs`
  -- `NewDoctorAvailabilityId` becomes `Guid?`, drop `[Required]`, update the doc comment to say
  the requestor no longer picks and the field is retained for a future "suggested date".
- pattern: `RequestCancellationDto.cs` (reason-only input shape).
- approach: code
- acceptance (EARS): WHEN a reschedule request is submitted with no `newDoctorAvailabilityId`,
  THE SYSTEM SHALL accept the payload and SHALL NOT return a model-validation error.

### T2 -- entity ctor stops requiring a slot for Reschedule

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/AppointmentChangeRequest.cs`
  -- in the `ChangeRequestType.Reschedule` branch (`:154-158`) remove
  `Check.NotNull(newDoctorAvailabilityId, ...)`; KEEP `Check.NotNullOrWhiteSpace(reScheduleReason, ...)`.
  Update the class doc lifecycle note (`:12-35`) to state staff choose the slot at approval.
- pattern: the Cancel branch (`:150-153`), which requires a reason and no slot.
- approach: tdd
- acceptance (EARS): WHEN an `AppointmentChangeRequest` of type Reschedule is constructed with a
  null `newDoctorAvailabilityId` and a non-empty reason, THE SYSTEM SHALL construct the entity
  with `RequestStatus` Pending. If the reason is null or whitespace, then THE SYSTEM SHALL throw.

### T3 -- manager accepts a null slot and skips the hold

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentChangeRequests/AppointmentChangeRequestManager.cs`
  -- `SubmitRescheduleAsync` param `Guid newDoctorAvailabilityId` becomes `Guid? newDoctorAvailabilityId`;
  delete the `Guid.Empty` -> `ChangeRequestNewSlotRequired` throw (`:231-234`); wrap the slot
  lookup, the `IsSlotAvailable` guard and the `Available -> Reserved` write (`:253-264`, `:282-283`)
  in `if (newDoctorAvailabilityId.HasValue)`. The `Approved -> RescheduleRequested` transition
  (`:297-300`) is UNCHANGED and still fires. Update the XML doc.
- pattern: `SubmitCancellationAsync` (`:101-178`), which passes `newDoctorAvailabilityId: null`.
- approach: tdd
- acceptance (EARS): WHEN `SubmitRescheduleAsync` is called with a null slot on an Approved
  appointment, THE SYSTEM SHALL insert a Pending Reschedule request, SHALL NOT reserve any slot,
  and SHALL still transition the appointment to `RescheduleRequested`. WHEN called with a slot
  that is not `Available`, THE SYSTEM SHALL throw `ChangeRequestNewSlotNotAvailable`.

### T4 -- approval validator resolves a staff-only pick

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/ChangeRequestApprovalValidator.cs`
  -- replace `ResolveNewSlotAndEnsureAdminReason`'s `ArgumentException` (`:91-96`) with: if
  `userPickedSlotId` has no value, require `overrideSlotId` and return it, else throw
  `BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired)`. The admin-reason
  gate applies ONLY when BOTH ids have values and differ.
- pattern: the existing `EnsureRescheduleOutcome` (`:68-76`) `BusinessException` style.
- approach: tdd
- acceptance (EARS): WHEN both ids are null, THE SYSTEM SHALL throw
  `ChangeRequestNewSlotRequired`. WHEN the user pick is null and an override is supplied, THE
  SYSTEM SHALL return the override and SHALL NOT require an admin reason. WHEN both are supplied
  and differ with no admin reason, THE SYSTEM SHALL throw `ChangeRequestAdminReasonRequired`.

### T5 -- submit path: conditional policy gate, no reschedule consent

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.cs`
  -- in `RequestRescheduleAsync`, wrap the slot lookup + `_bookingPolicyValidator.ValidateAsync`
  (`:138-151`) in `if (input.NewDoctorAvailabilityId.HasValue)`; pass the nullable through to
  `SubmitRescheduleAsync` (`:158`). Skip `IssueConsentAndNotifyAsync` (`:164`) when
  `changeRequest.ChangeRequestType == ChangeRequestType.Reschedule`, with a comment naming 4c as
  the owner of the post-date-pick consent round. Cancellation consent (`:102`) is UNCHANGED.
- pattern: the existing `HasValue` guards at `...Approval.cs:252-261`.
- approach: code
- acceptance (EARS): WHEN a reschedule request is submitted, THE SYSTEM SHALL NOT issue a
  consent token and SHALL NOT publish `ChangeRequestConsentRequestedEto`. WHEN a cancellation
  request is submitted, THE SYSTEM SHALL issue consent exactly as before. WHERE a slot IS
  supplied on a reschedule submit, THE SYSTEM SHALL still enforce the role-aware booking policy.

### T6 -- enforce booking policy on the slot staff actually chose

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Application/AppointmentChangeRequests/AppointmentChangeRequestsAppService.Approval.cs`
  -- inject `BookingPolicyValidator` (6th ctor dep); after `newSlot` is loaded (`:222`) call
  `ValidateAsync(newSlot.AvailableDate, sourceAppointment.AppointmentTypeId, isInternalRescheduler: true)`
  (approval is always an internal actor, so the 90-day internal horizon applies). Also fix
  `isAdminOverride` (`:218-219`) to require `changeRequest.NewDoctorAvailabilityId.HasValue`, so a
  staff pick on a reason-only request is NOT reported as an override.
- pattern: `...AppService.cs:150-151` (the submit-side call).
- approach: tdd
- acceptance (EARS): WHEN a reschedule is approved onto a slot inside the lead time or beyond
  the 90-day ceiling, THE SYSTEM SHALL throw the booking-policy exception and SHALL NOT move the
  appointment. WHEN the requestor supplied no slot, THE SYSTEM SHALL set `IsAdminOverride` false
  on `AppointmentChangeRequestApprovedEto`.

### T7 -- approval email renders the resolved slot

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/ChangeRequestApprovedEmailHandler.cs`
  -- change the `slotId` selection (`:144-146`) to `AdminOverrideSlotId ?? NewDoctorAvailabilityId`,
  independent of `IsAdminOverride`, so a staff-chosen date always renders.
- pattern: the existing null-tolerant `ResolveNewSlotAsync` (`:179-194`).
- approach: tdd
- acceptance (EARS): WHEN a reschedule with no requestor slot is approved onto a staff-chosen
  slot, THE SYSTEM SHALL render that slot's date and time into `NewAppointmentDate` /
  `NewAppointmentFromTime` and SHALL NOT emit an empty date.

### T8 -- enrich the change-request DTO for the inbox

- what: MODIFY `.../Application.Contracts/AppointmentChangeRequests/AppointmentChangeRequestDto.cs`
  -- add `Guid? AppointmentLocationId`, `Guid? AppointmentTypeId`, `DateTime? RequestedSlotDate`,
  `string? RequestedSlotFromTime`, each documented as populated only by the queue query.
  MODIFY `CaseEvaluationApplicationMappers.AppointmentChangeRequests.cs` -- add a
  `[MapperIgnoreTarget]` per new field on both `Map` overloads.
  MODIFY `...AppService.Approval.cs` -- add `PopulateAppointmentContextAsync` beside
  `PopulateAppointmentConfirmationNumbersAsync` (`:424-449`): one set-based appointment query for
  location + type, one set-based `DoctorAvailability` query for the non-null picked slots' date +
  `FromTime`; call it from `GetPendingChangeRequestsAsync` (`:413`).
- pattern: `PopulateAppointmentConfirmationNumbersAsync` (`:424-449`) -- distinct ids, single
  projection query, dictionary join, no N+1.
- approach: tdd
- acceptance (EARS): WHEN the pending queue is fetched, THE SYSTEM SHALL populate location and
  type for every row and SHALL populate requested slot date/time only for rows whose
  `NewDoctorAvailabilityId` is non-null, using at most one query per referenced table.

### T9 -- regenerate the Angular proxies

- what: RUN `abp generate-proxy -t ng -u http://localhost:44327 --module app`; commit ONLY the
  touched `appointment-change-requests` model files plus `generate-proxy.json`.
- pattern: prior phases' proxy commits.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL expose `newDoctorAvailabilityId` as optional on
  `RequestRescheduleDto` and the four new read fields on `AppointmentChangeRequestDto`, and
  `npx tsc` SHALL report no error in the proxy folder.

### T10 -- reschedule request modal: calendar for staff, reason-only for external

- what: MODIFY `angular/src/app/appointments/appointment/components/reschedule-request-modal.component.{ts,html}`
  -- delete `slots`, `isLoadingSlots`, `slotLabel()` and `loadSlots()` (`ts:60,64,104-137`);
  replace the `<select>` block (`html:15-39`) with `<app-availability-calendar>` rendered only
  `@if (requesterIsStaff)`; add `selectedDate` / `selectedTime` fields and an `onSlotSelected`
  handler that sets `newDoctorAvailabilityId`; `canSubmit` (`ts:74-81`) requires a slot ONLY when
  `requesterIsStaff`; add `[options]="{ size: 'lg' }"` to `<abp-modal>` (`html:1`); pass
  `newDoctorAvailabilityId` as `null` when external. Reset the new fields in `ngOnChanges` and
  `setVisible` alongside the existing resets.
- pattern: `appointment-add-schedule.component.html:59-76` + its parent's `onSlotSelected`.
- approach: test-after
- acceptance (EARS): WHILE `requesterIsStaff` is true, THE SYSTEM SHALL render the availability
  calendar and SHALL keep Submit disabled until both a date and a time are chosen. WHILE
  `requesterIsStaff` is false, THE SYSTEM SHALL render no date control and SHALL enable Submit on
  a non-empty reason alone.

### T11 -- inbox approve modal: show the requested date, let staff choose one

- what: MODIFY `angular/src/app/appointments/change-requests/internal-change-request-inbox.component.{ts,html}`
  -- add `overrideSlotId` / `overrideDate` / `overrideTime` / `adminReason` signals reset in
  `openApprove` (`ts:250-257`) and `closeModal` (`ts:262`); in the approve branch
  (`html:159-204`) for reschedule rows render the requested date from the new DTO fields (or
  "No date requested"), one `<app-availability-calendar>` fed by `appointmentLocationId` /
  `appointmentTypeId`, and an admin-reason textarea shown only when the row HAS a requested slot
  and staff pick a different one; send `overrideSlotId` + `adminReScheduleReason` in the
  `approveReschedule` call (`ts:313-317`); extend the approve-button disabled guard (`html:198`)
  to require a slot when the row has none. Replace the "Open the appointment to review and
  confirm the slot" copy (`html:138-140`).
- pattern: the existing outcome `<select>` + `ra-field` markup (`html:178-185`); the
  `consentNote` conditional (`html:186-191`) for the admin-reason block.
- approach: test-after
- acceptance (EARS): WHEN a reschedule row with no requested slot is opened for approval, THE
  SYSTEM SHALL disable Approve until a date and time are chosen and SHALL NOT require an admin
  reason. WHEN a reschedule row HAS a requested slot and staff choose a different one, THE SYSTEM
  SHALL require a non-empty admin reason before enabling Approve. THE SYSTEM SHALL render at most
  one availability calendar at a time.

### T12 -- pure util + specs for the new gating

- what: CREATE `angular/src/app/appointments/change-requests/cr-approve.util.ts` with
  `canApproveReschedule({requestedSlotId, chosenSlotId, chosenTime, adminReason})` and
  `requiresAdminReason(requestedSlotId, chosenSlotId)`; CREATE `cr-approve.util.spec.ts`.
  CREATE `angular/src/app/appointments/appointment/components/reschedule-submit.util.ts` with
  `canSubmitReschedule({requesterIsStaff, slotId, time, reason, maxReasonLength})` and its spec.
  Both components delegate to these instead of inline boolean logic.
- pattern: `cr-inbox.util.ts` + `cr-inbox.util.spec.ts`.
- approach: tdd
- acceptance (EARS): THE SYSTEM SHALL cover, with a passing spec each, both role branches of
  submit eligibility and all three approve branches (no requested slot, accepted requested slot,
  overridden requested slot).

### T13 -- localization keys

- what: MODIFY `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- retire `Appointment:Modal:RescheduleSlotPlaceholder` and `Appointment:Modal:RescheduleNoSlots`
  (`:512,515`) if unreferenced after T10; add keys for the external reason-only note and the
  staff date labels. Keep `Appointment:Modal:RescheduleSlotLabel` (`:511`) if reused as the
  calendar's `dateLabel`.
- pattern: the surrounding `Appointment:Modal:*` block.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL resolve every `abpLocalization` key referenced by the two
  modified templates, and `grep` SHALL find no reference to a removed key.

### T14 -- delete the superseded change-request trio (SEPARATE COMMIT)

- what: DELETE `angular/src/app/appointments/change-requests/change-request-list.component.{ts,html}`,
  `change-request-approve-modal.component.{ts,html}`, `change-request-reject-modal.component.{ts,html}`.
  Remove any `::ChangeRequest:Modal:*` localization keys left unreferenced.
- pattern: 4a's removal of `legacy-add-redirect.ts` -- delete, then grep to prove nothing
  references the removed symbols.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL build with `npx ng build` after the deletion, and `grep`
  for `ChangeRequestListComponent`, `ChangeRequestApproveModalComponent` and
  `ChangeRequestRejectModalComponent` SHALL return no hits under `angular/src`.

### T15 -- update the epic tracker

- what: MODIFY `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` -- 4b row to
  DONE with the PR/sha; CORRECT the stale line 160 anchor; record the two latent email defects and
  the approve-time policy bypass under pre-existing bugs; add 4b learnings. Set this file's
  `status: done`.
- approach: code
- acceptance (EARS): THE SYSTEM SHALL contain no reference to
  `ChangeRequestApproveModalComponent` as a live surface in the tracker.

## Build log -- deviations and defects found (2026-08-04)

### Deviation: three pure extractions so the `tdd` tasks were actually testable

T6, T7 and T8 were flagged `tdd`, but their logic sat inline in
`AppointmentChangeRequestsApprovalAppService`, which research had already established is neither
unit- nor integration-testable (10 ctor deps + ABP ambient services, and the change-request
harness does not exist until 4c). Rather than downgrade them to `code`, the decisions were
extracted into pure, already-`InternalsVisibleTo` homes -- matching this folder's existing
precedent (`ChangeRequestListFilter`, `ChangeRequestApprovalValidator`, `RescheduleInPlacePolicy`):

- `ChangeRequestApprovalValidator.IsAdminOverride(proposedSlotId, staffSlotId)` (T6)
- `ChangeRequestApprovalValidator.ResolveScheduledSlotId(adminOverrideSlotId, newDoctorAvailabilityId)` (T7)
- `ChangeRequestQueueContext` (new file) for the queue projection (T8)

All three are covered by unit tests. The T8 tests were written after the code, so they were
mutation-checked (swapping `LocationId` for `AppointmentTypeId` produced 2 failures) to prove they
can fail.

### Defect found: `AdminOverrideSlotId` was only persisted on a genuine override

Fixing `isAdminOverride` correctly (so a staff pick on a reason-only request is not reported as an
override) exposed a second-order bug: `...Approval.cs` only wrote `AdminOverrideSlotId` when
`isAdminOverride` was true. On the 4b external path that left BOTH slot columns null on the
accepted row -- losing the audit trail and blanking the date in the approval email, which resolves
its slot from that row. Now the staff choice is persisted whenever staff chose one; only the admin
REASON stays gated on a real override.

### Defect INTRODUCED and fixed: a fresh object literal bound to a signal input hangs the browser

`[options]="modalOptions"` on `<abp-modal>` was first written as a getter returning
`{ size: 'lg' }` / `{}`. `ModalComponent.options` is a SIGNAL input, so a new object identity on
every change-detection pass re-dirties the view and re-runs change detection forever. It hung the
browser tab hard and SILENTLY, because the container serves a PRODUCTION build where Angular's
dev-mode infinite-CD guard is compiled out -- three consecutive Playwright calls timed out at
1800s before the cause was found, and the wedged renderer then starved the MCP server itself.

Fixed by returning FROZEN MODULE CONSTANTS from `rescheduleModalOptions(requesterIsStaff)`, with a
regression test asserting reference identity. The test was mutation-verified: restoring the object
literal fails it.

Generalisable: any binding feeding an ABP v10 signal input must be referentially stable. A getter
is fine; a getter that CONSTRUCTS is not.

### Confirmed while investigating

`ModalComponent` really does default to `size: 'md'` (Bootstrap 500px) -- see the compiled
`toggle()` in `@abp/ng.theme.shared`. The two-month datepicker popup does need the `lg` dialog.

### Proxy regeneration note

`abp generate-proxy` rewrites the whole proxy tree, producing large unrelated drift (it deletes
`proxy/books/*`, adds `proxy/integration/`, and reorders methods in ~35 files). Only
`appointment-change-requests/models.ts` + `generate-proxy.json` were kept; everything else was
reverted. The generated `models.ts` was byte-identical to the hand-written version. `abp` is a
DOTNET GLOBAL TOOL at `~/.dotnet/tools/abp` -- `npx abp` fails with "could not determine
executable to run".

## Validation loop

Backend:

```
dotnet format --verify-no-changes
dotnet build -warnaserror
dotnet test
```

Frontend (both required -- a build alone misses specs that pin existing values):

```
export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe"
npx prettier --check <changed frontend files>
npx eslint <changed frontend files>
npx ng build
npx ng test --watch=false --browsers=ChromeHeadless --include='**/appointments/**/*.spec.ts'
```

Live gate (Playwright MCP; `docker restart main-angular-1` first, wait for "Accepting connections"):

1. External at `falkinstein.localhost:4200` as `patient@falkinstein.test` -- file a reschedule,
   confirm NO date control and that a reason alone submits.
2. Internal at `admin.localhost:4200` as `clistaff1@gesco.com` -> "Enter practice" -- file a
   reschedule, confirm the calendar renders unclipped inside the modal, greys out sub-lead-time
   days, and that the picked date DISPLAYS in the input (4a defect #6 regression check).
3. Same session -> Change requests inbox -- approve the external request by choosing a date;
   confirm Approve stays disabled until date+time and that no admin reason is demanded.
4. SQL-verify each: `AppAppointmentChangeRequests` (`NewDoctorAvailabilityId`, `AdminOverrideSlotId`,
   `RequestStatus`) and `AppAppointments` (`AppointmentDate`, `AppointmentStatus`).

No migration is expected -- confirm `dotnet ef migrations has-pending-model-changes` reports none
for BOTH the host and Tenant contexts.

## Live gate: PASSED (2026-08-05, Playwright MCP, both doors)

Run after `docker compose up -d` (the stack had OOM'd -- `main-sql-server-1` exited 137 during the
15-minute EF Core run) and a `main-angular-1` restart.

| Step | Door                                                                       | Result                                                                                                                                                                                                                                                      |
| ---- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | External `patient@falkinstein.test` at `falkinstein.localhost:4200`        | Modal has NO calendar, NO date input, NO time select, NO old slot dropdown. Dialog stays `modal-md` (500px). Submit disabled on empty reason, enabled on reason alone. Submitted.                                                                           |
| 2    | Internal `clistaff1@gesco.com` at `admin.localhost:4200` -> Enter practice | Calendar renders, dialog widens to `modal-lg` (800px), picker NOT clipped (spans 584-1058 inside a body of 560-1360). Only 13/20/27 clickable -- exactly Demo Clinic South's published availability. Picked date DISPLAYS as `08/27/2026`. Submitted.       |
| 3    | Same internal session -> Change requests inbox                             | Detail panel reads "No date was requested -- choose the new date and time when you approve" (replacing the old "open the appointment" copy). Exactly ONE calendar instance. Approve disabled until date AND time; no admin-reason field demanded. Approved. |

SQL proof (`CaseEvaluation_falkinstein`), with enum values verified from source rather than assumed
(`RequestStatusType.Pending = 25 / Accepted = 26`, `AppointmentStatusType.Approved = 2 /
RescheduledNoBill = 7 / RescheduleRequested = 12`, `BookingStatus.Available = 8 / Reserved = 10`,
`ChangeRequestConsentStatus.NotRequired = 0`):

- External reason-only submit: `NewDoctorAvailabilityId = NULL`, `RequestStatus = 25`, reason
  persisted, `SideA = SideB = 0` (consent NOT issued -- T5), appointment -> `12`.
- Staff approval: appointment moved `2026-08-13 09:00` -> `2026-08-20 13:30`, status -> `2`,
  request -> `26`, outcome `7`, `AdminOverrideSlotId` SET even though it overrode nothing (the
  defect-2 fix), `AdminReScheduleReason = NULL` (no reason demanded -- the defect-1 fix), the
  landed slot `8`.
- Staff-filed submit: `NewDoctorAvailabilityId` SET to the Aug 27 10:30 slot, that slot ->
  `10` (Reserved hold applies when a slot IS proposed -- T3), consent still `0`.

The two branches were exercised on the SAME appointment, so the conditional slot handling and the
conditional Reserved hold are proven side by side rather than inferred.

Screenshots: `.github/pr-media/4b-external-reason-only.png`,
`4b-staff-calendar-in-request-modal.png`, `4b-staff-picks-date-in-approve.png`.

Test data left in the dev DB on purpose: A00036 sits at Aug 20 13:30 (Approved) with a PENDING
staff-filed reschedule to Aug 27 10:30 holding that slot Reserved. A00037 and A00034 are unchanged
from earlier phases.

## Risk / rollback

Blast radius: the reschedule request path (4 hosts) and the change-request approval path. Cancel
is untouched. `RequestRescheduleDto` relaxes a constraint, so old clients sending a slot keep
working -- the change is backward compatible on the wire. `AppointmentChangeRequestDto` gains
optional fields only.

Highest risks:

1. Suppressing reschedule consent means a reschedule can be approved with no consent recorded
   until 4c lands. RESOLVED as a release constraint: Adrian confirmed 4b and 4c ship to the
   server together (see Resolved decisions). 4b MUST NOT be deployed alone.
2. The datepicker popup may clip inside `abp-modal`; caught by live gate step 2.
3. T6 adds a gate that can now REJECT approvals that previously succeeded (dates inside the lead
   time). Intended, but it changes existing behaviour -- call it out in the PR.

Rollback: `git revert` the squash-merge commit. T14 is a separate commit and reverts
independently. No schema change, so no down-migration.
