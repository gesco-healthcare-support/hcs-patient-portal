---
id: AP1
title: Clarify Appointments-list actions - Review (view+edit) and Reschedule replace the confusing Review/Edit split
type: ux
components: [angular/src/app/appointments/appointment/components/appointment.component.html, angular/src/app/appointments/appointment/components/appointment-view.component.ts, angular/src/app/appointments/appointment/services/appointment-detail.abstract.service.ts, angular/src/app/proxy/appointment-change-requests/, src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentChangeRequestsAppService.Approval.cs, src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentChangeRequestManager.cs]
related_known_bugs: [BUG-039, OBS-12, OBS-19, BUG-024, BUG-032, BUG-030, BUG-027]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

The appointments-list Actions dropdown shows "Review" and "Edit" as two items that lead to
two architecturally different surfaces with overlapping purpose, which confuses users.
Replace the muddled split with two clear-purpose actions:

- "Review" = the internal-staff view + edit page (everything EXCEPT location/slot/type).
- "Reschedule" = change date/location/slot through the existing change-request workflow.

Remove the leftover ABP-Suite CRUD Edit modal (the Edit-half of BUG-039). Build the
Reschedule + Cancel request modals AND the supervisor "Pending Change Requests" approval
pages now, on ng-bootstrap. Internal-staff reschedule auto-approves; external requests go to
Pending. Type change stays a manual cancel + rebook (no guided flow). Never edit the slot in
place (that skips the capacity gate).

## Current behavior (from investigation)

- Dropdown has three items: Review -> `routerLink ['/appointments/view', id]`; Edit ->
  `(click)="update(row)"`; Delete (`appointment.component.html:243-265`, confirmed by Read).
- "Review" opens the hand-built routed `AppointmentViewComponent` (`appointment-routes.ts:12-20`):
  patient/employer/AA/DA, read-only injury table, authorized users, documents, packet,
  change-log, plus the Approve/Reject office workflow (`appointment-view.component.html:31-103`).
  For internal staff it is fully editable with its own Save; external roles get
  `form.disable()` (`appointment-view.component.ts:420-421`).
- "Edit" opens the ABP-Suite generated CRUD modal via `update(row)` ->
  `AbstractAppointmentDetailViewService.showForm()` (`appointment.abstract.component.ts:62-64`;
  `appointment-detail.abstract.service.ts:50-100`). That modal exposes only 13 flat
  AppAppointments fields (panelNumber, appointmentDate, requestConfirmationNumber [readonly],
  dueDate, appointmentStatus [create-only], 5 lookup IDs) and PUTs via the proxy to
  `AppointmentsAppService.UpdateAsync -> AppointmentManager.UpdateAsync`
  (`AppointmentsAppService.cs:1010`).
- Review is a superset of Edit for data editing; the two surfaces share NO form code. Edit
  uniquely still lets staff re-point type/location/slot/patient/dueDate; Review's only
  editable appointment-level field is panelNumber (`appointment-view.component.ts:205`).
- Review's `save()` builds an `AppointmentUpdateDto` that INCLUDES appointmentDate /
  appointmentTypeId / locationId / doctorAvailabilityId, but it sources those four from the
  loaded `selected` object, NOT from editable controls (deep-dive
  `wpxgq68y4.output:437`, lines 666-672). So today the Review page is already
  "view + edit minus scheduling" -- nothing needs to be locked down on Review.
- Reschedule/Cancel BACKEND IS FULLY BUILT (deep-dive `wpxgq68y4.output:429-435,440`):
  `AppointmentChangeRequest` aggregate (Cancel=1 / Reschedule=2); manager
  `SubmitCancellationAsync` (`AppointmentChangeRequestManager.cs:86`) and
  `SubmitRescheduleAsync` (`:188`); approval AppService
  `AppointmentChangeRequestsAppService.Approval.cs` with `ApproveRescheduleAsync` (`:221`,
  full cascade-clone of a new appointment via `AppointmentRescheduleCloner`),
  `RejectRescheduleAsync` (`:341`), `GetPendingChangeRequestsAsync` (`:405`); submit AppService
  `RequestRescheduleAsync` (`:80`, runs `BookingPolicyValidator` lead-time gate). Controllers
  exist for every endpoint. The "Phase 16/17 will add..." docstrings at
  `AppointmentChangeRequestManager.cs:16-22` are STALE -- all methods exist.
- Status machine permits Reschedule/Cancel from Approved ONLY
  (`AppointmentManager.cs:294`); reschedule-approve CLONES a new appointment at the new
  slot (same confirmation number) and stamps the source Rescheduled* -- it does not mutate the
  old slot row (deep-dive `:434-435`).
- THE GAP IS ANGULAR-ONLY: the proxy exists
  (`angular/src/app/proxy/appointment-change-requests/`) but ZERO non-proxy code consumes it;
  no modal/page invokes any change-request endpoint (deep-dive `:432`). OBS-12 confirms the UI
  gap; the supervisor dashboard has a placeholder "Pending Change Requests" tile labeled
  "(populated when W3 ships)" (deep-dive `:433`).
- Permissions already registered: `AppointmentChangeRequests.Default/.Approve/.Reject`
  (`CaseEvaluationPermissions.cs:245`, provider `:132-134`); submit gated by per-row
  `AppointmentAccessRules.CanEdit` (internal users bypass) (deep-dive `:440`).

## Relevant code locations

- `angular/src/app/appointments/appointment/components/appointment.component.html:242-266`
  (Actions dropdown -- rename items, drop Edit, add Reschedule/Cancel).
- `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts:62-64,70-82`
  (`update()` wiring + Edit/Delete visibility gating).
- `angular/src/app/appointments/appointment/services/appointment-detail.abstract.service.ts:50-100`
  and `appointment-detail.component.ts/.html` (generated CRUD modal -- de-wire, keep abstract
  file per `angular/src/app/CLAUDE.md` "Never delete the abstract file").
- `angular/src/app/appointments/appointment/components/appointment-view.component.ts:130-504,581-730`
  (Review page; add an explicit Edit affordance; never expose type/location/slot inputs).
- `angular/src/app/proxy/appointment-change-requests/` (existing proxy services -- do not edit;
  regenerate if DTOs change).
- New Angular components (ng-bootstrap NgbModal): Reschedule-request modal, Cancellation-request
  modal, two supervisor change-request list pages + approve/reject modal.
- Slot lookup already available: `GET /api/app/doctor-availabilities/lookup`
  (`doctor-availability.service.ts:88`, type=1 dates / type=0 slots; deep-dive `:439`).
- Backend (no changes expected for the happy path):
  `AppointmentChangeRequestsAppService.Approval.cs`, `AppointmentChangeRequestsAppService.cs`,
  `AppointmentChangeRequestManager.cs`, `AppointmentManager.cs:294`,
  `AppointmentRescheduleCloner`.
- Localization: `Domain.Shared/.../en.json:415` (`Appointment:Action:Review`); add keys for
  Reschedule / Cancel actions.

## Phase 3 cross-reference

- BUG-039 (Edit-half) -- same `AbstractAppointmentDetailViewService` CRUD modal; removing the
  Edit dropdown wiring closes its outstanding half. Fix here.
- OBS-12 (reschedule/cancel UI gap) -- this item BUILDS the missing UI; close OBS-12 with it.
- OBS-19 (folded into BUG-039) -- documents the generated modal as scaffolding; resolved by the
  same de-wire.
- Stale docstrings at `AppointmentChangeRequestManager.cs:16-22` ("Phase 16/17 will add...") --
  correct while touching this file; methods already exist.
- Stale draft design docs `docs/design/external-user-appointment-rescheduling-design.md`,
  `...-cancellation-design.md`, `staff-supervisor-change-request-approval-design.md` -- they
  assume MatDialog/mat-table (wrong stack) and external-only framing; supersede/rewrite for
  ng-bootstrap + the internal-staff auto-approve framing.

## Research findings

- Internal patterns / prior art:
  - ng-bootstrap NgbModal is the in-use modal stack (e.g. approve/reject modals on
    appointment-view; `NgbDatepickerModule` for date inputs) -- NOT MatDialog. Reuse the
    existing approve/reject modal components as the structural template
    (`approve-confirmation-modal.component.ts:140-164`,
    `reject-appointment-modal.component.ts:88-107`).
  - Auto-approve precedent: internal-staff booking already auto-approves (BUG-030 fixed) --
    the same "internal staff bypass the Pending gate" mental model applies to reschedule.
  - Server-validated state transitions only reachable from Review (Reject requires a reason:
    BUG-024/BUG-032 fixed) reinforce that Review owns office actions; Reschedule/Cancel are
    distinct change-request actions.
  - Abstract-base retention rule: `angular/src/app/CLAUDE.md` -- never delete the ABP-Suite
    abstract file; de-wire the dropdown/visibility instead so regeneration parity holds.
- External docs: none load-bearing. ng-bootstrap NgbModal and Angular reactive-forms usage
  follow the patterns already established in this feature folder; no new framework pattern is
  introduced.

## Approaches considered (with tradeoffs)

- Option A -- Reschedule = thin UI over the change-request workflow with mandatory supervisor
  approval for everyone (OLD parity). Pro: cleanest audit, reuses everything. Con: two-step
  (request -> approve) is heavier than the desired one-click for internal staff.
- Option B -- Reschedule = direct in-place edit of date/location/slot via
  `AppointmentManager.UpdateAsync`. REJECTED: `UpdateAsync` (`AppointmentManager.cs:155`) does
  NOT re-run the five-step slot/capacity gate that `CreateAsync` runs, so editing
  doctorAvailabilityId in place can overbook or point at an invalid slot; it also abandons the
  audited Rescheduled* workflow and never sets the Rescheduled* statuses. Violates the locked
  "do NOT edit the slot in place" rule.
- Option C (CHOSEN) -- staff Reschedule POSTs the change-request and, when the caller is
  internal staff, immediately calls the existing approve path (auto-approve); external requests
  stop at Pending for the supervisor pages. Pro: one click for staff, full audit trail +
  Rescheduled* statuses + new-row cascade-clone preserved, capacity-integrity intact, reuses
  the fully-built and already-permissioned backend. Con: self-approving change request is a
  slightly unusual pattern -- mitigate with a clear docstring/ADR.

Why C wins: it satisfies both the UX ("Reschedule is one purposeful action") and the locked
integrity rule (capacity gate via the dedicated endpoint, never an in-place slot edit) while
reusing built backend code. A's mandatory two-step is heavier than needed for staff; B is
unsafe. For type change, no change-request alters AppointmentType and the cloner copies type
verbatim (deep-dive `:436`), so type change stays manual cancel + rebook.

## Decision (locked 2026-06-03)

- "Review" = internal-staff view + edit of everything EXCEPT location/slot/type (already the
  effective behavior; add a clear Edit affordance). Remove the generated ABP CRUD Edit modal
  wiring (BUG-039 Edit-half); keep the abstract base file.
- "Reschedule" = change date/location/slot via the EXISTING change-request workflow.
  Internal-staff reschedule AUTO-APPROVES (request + auto-approve in one action); external
  requests go to Pending.
- Build the Reschedule + Cancel request modals AND the supervisor "Pending Change Requests"
  approval pages NOW, on ng-bootstrap (NgbModal), not MatDialog.
- Type change = manual cancel + rebook (no guided flow now).
- Never edit the slot in place (skips the capacity gate).
- Enforcement: capacity/state integrity is server-side (change-request manager + status
  machine, already built); the Review-page omission of type/location/slot inputs is a UI
  affordance (the DTO still carries them but Review echoes loaded values).

## Implementation outline (no code)

1. Dropdown (`appointment.component.html:242-266`): keep "Review" (-> `/appointments/view/:id`);
   REMOVE the "Edit" item; ADD "Reschedule" (Approved + edit-access) and "Cancel"
   (Approved + edit-access). De-wire `update(row)` and its Edit visibility branch in
   `appointment.abstract.component.ts`; leave the abstract detail service file in place.
2. Localization: add `Appointment:Action:Reschedule` / `Appointment:Action:Cancel` keys to
   Domain.Shared `en.json` BEFORE referencing them in templates (CLAUDE.md localization rule).
3. Reschedule modal (new ng-bootstrap NgbModal component): date picker + time select sourced
   from `GET /api/app/doctor-availabilities/lookup` (type=1 dates, type=0 slots) + reason;
   POST `requestReschedule`; when caller is internal staff, chain the approve call
   (auto-approve outcome, e.g. RescheduledNoBill). Decide the auto-approve orchestration site
   (Angular sequential calls vs. a thin backend orchestration method) and document it.
4. Cancellation modal (new NgbModal): reason + late/no-bill outcome per the status machine;
   POST `requestCancellation`; auto-approve for internal staff, Pending for external.
5. Supervisor pages (new): two list pages (pending reschedules, pending cancellations) over
   `GetPendingChangeRequestsAsync`, plus an approve/reject modal calling the four approval
   endpoints. Wire the dashboard "Pending Change Requests" tile to the reschedule list.
6. Review page (`appointment-view`): confirm type/location/slot remain non-editable inputs;
   add an explicit "Edit details" affordance if the inline-edit entry point is unclear. Do not
   add scheduling inputs.
7. Correct stale docstrings (`AppointmentChangeRequestManager.cs:16-22`); supersede the three
   MatDialog draft design docs.
8. Proxy: regenerate via `abp generate-proxy` ONLY if any change-request DTO changes (the
   happy path needs none). No DB migration expected -- the entity/status machine already exist.
9. Server-vs-UI: integrity enforced server-side (existing change-request manager + status
   machine + capacity gate via clone path); the auto-approve role gate is also server-checked
   via the approval permissions / `AppointmentAccessRules`.

## Dependencies

- Resolves: BUG-039 (Edit-half), OBS-12, OBS-19.
- Soft dependency on the ROLES decision (Staff Supervisor as approver of external change
  requests) -- relevant only for which persona sees the supervisor approval pages; the backend
  permissions already exist, so no hard block.
- No dependency on type/panel-number items beyond the shared rule that type change is
  out-of-band (cancel + rebook).

## Residual open questions

- Auto-approve outcome default for staff reschedule (RescheduledNoBill vs. a chosen outcome)
  and where to run the request+approve chain (Angular sequential calls vs. a small backend
  orchestration method) -- minor; pick one and document via docstring/ADR.
- Whether external-initiated Reschedule/Cancel is exposed in this pass or staff-only first; the
  backend supports both, so this is a UI-scope toggle, not a blocker.
