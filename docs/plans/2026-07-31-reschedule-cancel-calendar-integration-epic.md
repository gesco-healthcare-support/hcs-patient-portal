---
feature: Reschedule / Cancel / Calendar / Case-Tracker epic -- ROADMAP
date: 2026-07-31
status: in-progress
base-branch: main
related-issues: []
---

# Epic roadmap: reschedule redesign, cancel outcomes, staff calendar, CT admin access

LIVING DOCUMENT. This is the phase-by-phase tracker for the 9-item request of
2026-07-31. Each phase gets its OWN plan file and its OWN PR. Update the Phase table
and "Learnings carried forward" after every phase lands, BEFORE designing the next one
-- later phases are expected to change based on what implementation teaches us.

## Working rules for this epic

- Branch off `main` ONLY. Fast-forward local `main` to `origin/main` first, every time.
- NEVER create a new git worktree for this epic, even if another session is writing code
  in the same worktree. Work in `C:/src/patient-portal/main`.
- Because a worktree may be shared with another session, `.git/index` is shared:
  commit BY PATHSPEC (`git commit -F - -- <explicit paths>`), never a bare `git commit`.
- Branch names are DESCRIPTIVE, never phase letters. The phase labels below exist only
  in this doc.
- One plan file per phase in `docs/plans/`, squash-merged to `main` via PR, then cascaded
  to `development` by the CI-gated auto-PR.

## Source request (verbatim intent, condensed)

Reschedule: (R1) calendar picker instead of a slot dropdown, same availabilities and
rules as booking; (R2) staff pick the new date, not the requestor, and BOTH sides then
consent; (R3) old appointment goes to a Rescheduled status and a NEW appointment is
created in the old one's status, slot freed, history linked; (R4) notify Case Tracker
only once the new appointment is approved, carry documents over, regenerate packets with
new dates; (R5) staff select a billing outcome on the rescheduled appointment so Case
Tracker can close or bill it.
Cancel: (C1) on approval, status Cancelled with reason + billing status, pushed to Case
Tracker with identifiers.
Calendar: (CAL1) Google-Calendar-style staff schedule showing booked / requested / empty
slots with patient name and/or appointment number; (CAL2) chips positioned at the real
date+time, clickable through to the appointment view.
Case Tracker: (CT1) the manual-send / dead-letter admin page reachable in the UI for IT
Admin and Staff Supervisor.

## Locked decisions (Adrian, 2026-07-31)

- Decision: reschedule produces TWO Case Tracker cases (old closed/billed, new opened),
  reversing the 2026-07-01 in-place design, because staff need to bill or close the old
  date while tracking the new one separately.
- Decision: the new appointment is created by REUSING the existing create pipeline
  (`CreateRevalAsync`-style `AppointmentCreateDto` path) rather than resurrecting the
  deleted cascade cloner -- with an explicit per-child-group audit + tests, because the
  old cloner caused bug F18 (silently dropped 2 of 8 child groups). Adrian's condition:
  "implement the concrete fixes ... not a patchwork."
- Decision: the requestor gets NO date picker for now (reason only); keep
  `NewDoctorAvailabilityId` and the suggestion wiring intact so a future "suggested date"
  needs no migration.
- Decision: a declined/expired consent creates a NEW consent ROUND per staff-proposed
  date (needs a round/supersedes link + migration), because Adrian wants the full audit
  trail of every date proposed and who declined it.
- Decision: uploaded documents carry over by COPYING rows that SHARE the same MinIO
  object key, because the rescheduled appointment must keep its own history; consequence
  is delete becomes soft-delete-only for shared blobs, which already matches the
  retention guarantee given to Case Tracker.
- Decision: packets are REGENERATED for the new appointment, never carried over,
  because the packet unique index is `(TenantId, AppointmentId, Kind)` and packet content
  embeds the appointment date.
- Decision: build the calendar on FullCalendar MIT plugins (`@fullcalendar/angular`
  7.0.2, `@angular/core` peer `16 - 22`, timeGrid + dayGrid), NOT the paid resource /
  timeline views. Adrian's caveat: if it does not do what he wants, we replace it and
  hand-build the whole thing -- so the backend endpoint stays VIEW-AGNOSTIC and a swap
  must remain frontend-only.
- Decision: Case Tracker coordination is NOT a build gate. We build and test our side;
  Levon already knows the design change; after implementation + testing we send him a
  detailed written change summary so he can adjust his receiver.
- Decision: Staff Supervisor gets the CT permissions HOST-side only, because the
  dead-letter screen is host-scoped and aggregates all offices; both IT Admin and Staff
  Supervisor legitimately have all-office access.
- Decision: separate plan + PR per phase, not one epic PR.

## Phases

| # | Phase | Branch | Plan | Status |
|---|---|---|---|---|
| 1 | Staff Supervisor CT permissions | `fix/supervisor-case-tracker-permissions` | `2026-07-31-staff-supervisor-case-tracker-permissions.md` | PLAN APPROVED - not built |
| 2 | Cancellation reason + billing status to CT | `feat/cancel-reason-to-case-tracker` | not written | TODO |
| 3 | Staff schedule calendar (FullCalendar) | `feat/staff-schedule-calendar` | not written | TODO |
| 4a | Extract reusable availability calendar | `refactor/extract-availability-calendar` | not written | TODO (prereq for 4b) |
| 4b | Staff pick the reschedule date | `feat/staff-picks-reschedule-date` | not written | TODO (after 4a) |
| 4c | Consent rounds, both sides, after date pick | `feat/reschedule-consent-rounds` | not written | TODO (after 4b) |
| 4d | Reschedule creates a new appointment | `feat/reschedule-creates-new-appointment` | not written | TODO (after 4c) |
| 4e | CT two-case semantics + contract amendment | `feat/case-tracker-two-case-reschedule` | not written | TODO (after 4d) |

Phases 1, 2 and 3 are mutually independent. 4a-4e are strictly sequential.

## Cross-phase research findings (verified 2026-07-31)

Facts later phases depend on. Re-verify before asserting -- code moves.

- The Case Tracker contract at `docs/integration/case-tracker-api-contract.md` is marked
  FINAL and BUILT and documents the OPPOSITE of decision 1: section E2 (`:395`) says the
  portal "moves the SAME appointment in place rather than cloning a row ... it never
  creates a second one", and `:403` warns the receiver that a reschedule is signalled by
  a changed date, NOT a status change. Phase 4e must rewrite E2, the section A status
  table, section H timing, and Coordination decisions 4 and 6.
- `Appointment.OriginalAppointmentId` (`Appointment.cs:146`) already exists and is
  already used to link a re-eval to its source (`AppointmentsAppService.cs:879-891`).
  Reschedule chaining can reuse it, but `EvaluationKind` is persisted SEPARATELY precisely
  so a dual-purpose link cannot mislabel a case -- keep the two meanings disambiguated.
- `(TenantId, RequestConfirmationNumber)` is a hard unique index
  (`CaseEvaluationDbContext.cs:574-576`, tenant context `:523-524`): a new appointment
  MUST get its own confirmation number.
- Consent is already two-sided on the change-request row (`SideA*` / `SideB*`, 5 columns
  each, migration `20260701195010_TwoSidedChangeRequestConsent`). The requestor's side is
  currently AUTO-GRANTED (`AutoGrantSide`, `AppointmentChangeRequestsAppService.cs:244`) --
  that is the line Phase 4c changes. The staff-initiated branch (`:257-287`) already asks
  BOTH sides and is the path to reuse.
- Phase 4c blocker: `IssueSideConsent` (`AppointmentChangeRequest.cs:192-199`) is only
  valid from `NotRequired`, so a side already Approved/Rejected cannot be re-tokened
  without a reset or a new round row.
- Phase 4c ordering inversion: consent is checked BEFORE the slot is resolved
  (`AppointmentChangeRequestsAppService.Approval.cs:204-205` vs `:207`). The new flow needs
  slot-first, then consent.
- `RegeneratePacketAsync` (`AppointmentDocumentsAppService.cs:836-851`) already exists and
  enqueues all three kinds -- reuse it in 4d, do not write a new generator.
- Packet-set completion (the gate a CT notify must wait on) is
  `PacketSetPolicy.IsComplete` via `PacketsCompleteHandler` -- reuse, do not reinvent.
- `AppointmentDocument.AppointmentId` is indexed but NOT unique
  (`CaseEvaluationDbContext.cs:609-612`), so copying document rows is cheap; the packet
  unique index `(TenantId, AppointmentId, Kind)` filtered on `IsDeleted=0`
  (`:833`) is why packets must be regenerated instead.
- The booking calendar is NOT reusable as-is: `AppointmentAddScheduleComponent` is a
  template shell, while the real logic (`availableDateKeys`,
  `markAppointmentDateDisabled`, `loadAvailableDatesBySelection`, the 60/90-day horizon)
  lives on the 3764-line `AppointmentAddComponent`. That extraction is Phase 4a.
- The reschedule modal today lacks the 60/90-day horizon rules, so "same rules as
  booking" requires porting them, not just swapping the widget.
- `ChangeRequestApproveModalComponent` has NO slot picker at all -- Phase 4b adds one.
- No calendar library exists in `angular/package.json`; Phase 3 adds the first one.
- No endpoint joins slots to appointments. `getSlotPatientNames` returns names only (no
  id, no status), so it cannot power a clickable chip -- Phase 3 needs a new joined
  endpoint returning per-slot occupancy plus appointment id / confirmation number /
  status.
- Permission grants are applied by `GrantAllAsync`
  (`InternalUserRoleDataSeedContributor.cs:134-140`) UNCONDITIONALLY on every seed pass;
  only `EnsureRoleAsync` is create-once. So a seeder grant reaches ALREADY-DEPLOYED roles
  on the next `db-migrator` run.

## Pre-existing bugs found during research

Not part of the original request. Folded into the phase that already touches the code
rather than expanding scope; Adrian was told and did not exclude them.

- `BookingStatus.Booked` is never set by ANY code path -- only `Available` / `Reserved`
  are ever assigned, so the existing week-grid's "Booked" colour is only reachable by a
  manual admin edit and is not ground truth. -> Phase 3 (which needs real occupancy
  counts anyway).
- Packets go STALE after an in-place reschedule: packet content embeds the appointment
  date (`PacketTokenResolver.cs:222-233`) but nothing calls regeneration from the
  reschedule approval path. -> Fixed by Phase 4d. Affects production until then.
- Consent can hang forever: there is NO expiry job, so a side stays `Pending`
  indefinitely and blocks finalize, and the consent email's "referred to our clinic
  staff" promise has no implementation. -> Phase 4c.

## Learnings carried forward

Append after each phase. Empty until Phase 1 ships.

- (2026-07-31, pre-build) The worktree was stale on a merged branch while `origin/main`
  had advanced through PR #406. Always fast-forward `main` before designing OR building a
  phase; a plan written against a stale tree can cite superseded anchors.
- (2026-07-31, pre-build) Phase 1 shrank mid-design: PR #406 (`058fd57f`) had already
  granted IT Admin the same two permissions hours earlier, leaving only the Staff
  Supervisor half. Re-check `origin/main` at the START of each phase's design -- other
  sessions are shipping into this repo.
