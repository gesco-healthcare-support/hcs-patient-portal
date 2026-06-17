---
feature: send-back-request-info
date: 2026-06-17
status: in-progress
base-branch: feat/internal-user-pages
related-issues: []
note: Design handoff Prompt 17 (Send Back Feature - Redesign). No GitHub issue.
---

## Goal

Bring the existing appointment "send back / request info" loop up to the Prompt 17
redesign across all four surfaces and fill the backend gaps (send-back email, staff
diff review, request history) the redesigned loop depends on.

## Context

The feature already has a working end-to-end skeleton: `AppointmentStatusType.InfoRequested=14`,
a `Pending<->InfoRequested` state machine, `SendBackAsync`/`ResubmitAsync`/`GetOpenAsync`,
the `AppointmentInfoRequest` entity (live migration `20260614015543`), a grouped-checkbox
staff modal, a basic external fix-it block, and Info-Requested status chips on the internal
list, internal detail, external home, and external detail. The work is reconciling that
skeleton to the richer redesign plus three backend gaps.

Decisions made this session (AskUserQuestion):
- Two sub-branches, both delivered this pass.
- Send-back email now; no auto-reminder job.
- Full parity: every send-back field becomes inline-editable on the external fix-it page.
  Mapping each field to its data source (2026-06-17) showed ~half have no external edit
  path today, so B1 builds the missing backend: appointment-scoped upsert endpoints for
  primary insurance + claim examiner, a document replace path (external delete/replace),
  applicant/defense attorney child-entity edits with appointment denorm-column sync, and
  language via the existing patient for-appointment-booking endpoint.
- panelNumber + appointmentDate/time are REMOVED from the send-back modal (staff resolve
  the panel directly; date changes go through reschedule) but KEPT in the shared
  FLAGGABLE_FIELDS list for Field Config (Prompt 15) via a new `sendBackFlaggable` flag --
  the list is consumed by cf-config.util.ts too, so entries must be flagged, not deleted.
- Request-info gating is unchanged: all three internal roles already hold
  `Appointments.Approve` (send-back's gate). Full parity DOES add new EXTERNAL edit
  permissions (insurance/examiner upsert + document replace) seeded to external roles.

Out of scope (tracked separately):
- External pre-approval reschedule/cancel (the change-request system exists but is
  Approved-only; extending it pre-approval needs its own state-machine + billing research).
  Recorded as a follow-up feature, to be done right after this pass.
- Whole-app QA sweep (Phase 4) and multi-tenant hardening.

Constraints:
- HIPAA: synthetic data only; SSN stays masked in lists, the diff card, and any email.
- ABP Commercial 10 / Angular 20 standalone / .NET 10; API envelope convention.
- Never hand-edit generated Angular proxies (regenerate via abp CLI).
- Never commit `design_handoff_*` or `docker-compose.override.yml`.
- ASCII only; no Claude attribution in commits/PRs.

## Approach

Split along the natural backend/UI boundary so the first branch delivers a working,
demoable loop and the second adds the staff review surface:

- Branch 1 (`feat/redesign-send-back-loop`): the core staff -> external -> resubmit loop.
  Reconcile the staff modal, reconcile the flaggable-field list, add the send-back email,
  complete the external fix-it page to full parity, and enforce the server-side field lock.
- Branch 2 (`feat/redesign-send-back-review`): the staff review side. Snapshot flagged-field
  values so a per-round before/after exists, add a history/diff read endpoint + proxy, and
  build the "What changed since your request" + "Request history" cards on the internal detail.

Each branch is squash-merged to `feat/internal-user-pages` only after live sign-off, matching
the established per-page rhythm. B2 starts after B1 is merged.

Alternatives rejected:
- One big branch: largest single review surface; delays the first demoable loop. Rejected
  for reviewability.
- Reconcile-only (defer email + diff/history): diverges from the prototype's promised email
  + the staff review cards the brief lists as in-scope. Rejected.
- ABP audit-log diff for the staff diff card: `AuditFieldDiff` exists but is not scoped to a
  send-back round and would conflate unrelated edits. A per-round value snapshot is simpler
  and exact. Rejected in favor of the snapshot.

## Tasks

### Branch 1 -- core send-back loop + full external-edit parity (feat/redesign-send-back-loop)

- B1-T1: Reconcile the flaggable-field registry (keep keys real; flag, do not delete).
  - approach: code
  - files-touched: [angular/src/app/appointments/appointment/send-back-fields.ts]
  - acceptance: keys remain the real booking-control names; add `sendBackFlaggable`
    (false for panelNumber + appointmentDate, true for the rest) and per-field edit
    metadata (`editSource`: patient | appointment-aa | appointment-da |
    appointment-insurance | appointment-examiner | document; `control`: text | date |
    select | file). Field Config (cf-config.util.ts) still iterates the full list; the
    send-back modal filters to `sendBackFlaggable`. Add insurance phone + confirm language
    only if backed by a real field.

- B1-T2: Redesign the staff request-info modal to the prototype.
  - approach: test-after
  - files-touched: [request-info-modal.component.ts, request-info-modal.util.ts (new),
    internal-appointment-detail.component.ts, internal-appointment-detail.component.html]
  - acceptance: modal lists only `sendBackFlaggable` fields; per-field hint input
    (maxlen 150) revealed on check; "Selected (N)" removable purple chips; note maxlen 500
    + "{n}/500" counter; Send disabled when 0 fields OR blank note (pure predicate in the
    util, unit-tested); copy matches the prototype; submit via `AppointmentInfoRequestService`
    proxy carrying flaggedFields[] (key + hint) + note; success toast "Sent back to the
    requester - email queued with your note."

- B1-T3: Add the send-back email.
  - approach: tdd
  - files-touched: [NotificationTemplateConsts, PatientAppointmentInfoRequested template
    asset/seed, StatusChangeEmailHandler, send-back email model/builder]
  - acceptance: `IsHandledStatus` includes `InfoRequested`; send-back queues an email to
    the requester with the staff note + a direct fix-it link (sourced from the open
    `AppointmentInfoRequest`); unit test asserts (a) `InfoRequested` is handled and (b) a
    flagged SSN value never appears in the rendered body. No reminder job.

- B1-T4: Fix-it corrections endpoint (server-side locked). ABSORBS former B1-T7.
  - approach: tdd
  - files-touched: [Application/AppointmentInfoRequests/AppointmentInfoRequestsAppService.cs
    + I...AppService.cs, Application.Contracts/AppointmentInfoRequests/ (a SaveCorrections
    input DTO), the AppointmentReadAccessGuard reuse]
  - discovered 2026-06-17: external roles ALREADY hold Create/Edit on the attorney,
    insurance, and claim-examiner child entities, and patient demographics are editable via
    the [Authorize] patient endpoint. The only gaps are the Appointment denorm email columns
    (need Appointments.Edit) + document replace (need Delete). So rather than 4 upsert
    endpoints + broadened grants, add ONE purpose-built endpoint authorized via the existing
    edit-access guard (like ResubmitAsync) -- a single trust boundary with the lock built in,
    no new permissions.
  - acceptance: SaveCorrectionsAsync(appointmentId, input) authorizes via the edit-access
    guard (NOT Appointments.Edit), requires status == InfoRequested, applies ONLY the open
    request's flagged fields to their homes (patient demographics -> Patient; attorney /
    examiner email -> Appointment denorm; insurance name/phone -> AppointmentPrimaryInsurance;
    defense firm -> AppointmentDefenseAttorney; document -> replace), and REJECTS a change to
    any non-flagged field (the server-side lock). Tests: non-flagged change rejected; each
    flagged field type applies; wrong status rejected; non-authorized caller rejected.

- B1-T5: Regenerate Angular proxies for the new/changed endpoints.
  - approach: code
  - files-touched: [angular/src/app/proxy/**]
  - acceptance: proxy exposes the new methods; generated via abp CLI (not hand-edited);
    the app builds.

- B1-T6: Complete the external fix-it page to full parity.
  - approach: test-after
  - files-touched: [external-appointment-detail.component.ts/.html/.scss,
    external-fix-it.util.ts (new)]
  - acceptance: every `sendBackFlaggable` flagged field is inline-editable via its endpoint
    with `sb-flag` (red until edited) + per-field hint (`sb-flaghint`); progress card
    "N of N fixed" + filling bar; document replace flow ("Replace requested" -> upload ->
    "Pending review", "Upload replacement"/"Replace again"); non-flagged fields read-only
    (`sb-locked`); "Save & finish later" toast; "Resubmit to clinic" disabled until
    fixedCount == total; resubmit confirmation modal ("Resubmit this request?") posts
    resubmit, returns to Pending, toasts "Resubmitted - the clinic has been notified."
    fixedCount/editability in the util + unit tests.

- B1-T7: MERGED into B1-T4. The server-side field lock is implemented + tested inside the
  single fix-it corrections endpoint (one trust boundary), not as a separate pass.

- B1-T8: Live-verify the loop and squash-merge B1.
  - approach: code
  - files-touched: []
  - acceptance: on Falkinstein, staff flag fields + note -> email in docker logs with note
    + link -> external user edits only flagged fields (all editable), sees progress,
    resubmits -> appointment returns to Pending; internal list filter offers "Info
    Requested" and Decide-by shows during InfoRequested (fix if absent); no console errors;
    placeholder code removed. On sign-off, squash-merge to feat/internal-user-pages + push.

### Branch 2 -- staff diff review + request history (feat/redesign-send-back-review)

- B2-T1: Snapshot flagged-field values for a per-round before/after.
  - approach: tdd
  - files-touched: [src/.../Domain/AppointmentInfoRequests/AppointmentInfoRequest.cs,
    src/.../Domain.Shared/AppointmentInfoRequests/AppointmentInfoRequestConsts.cs,
    src/.../Application/AppointmentInfoRequests/AppointmentInfoRequestsAppService.cs]
  - acceptance: send-back stores the flagged fields' values at send time; resubmit stores
    the new values at resubmit time (so each round has before + after). Unit test verifies
    both captures round-trip through the entity.

- B2-T2: EF migration for the snapshot column(s).
  - approach: code
  - files-touched: [src/.../EntityFrameworkCore/Migrations/* (generated),
    CaseEvaluationDbContextModelSnapshot.cs]
  - acceptance: `dotnet build` passes; `docker compose run --rm db-migrator` applies the
    migration cleanly; columns are additive/nullable (safe Down()).

- B2-T3: History + diff read endpoint.
  - approach: tdd
  - files-touched: [src/.../Application.Contracts/AppointmentInfoRequests/IAppointmentInfoRequestsAppService.cs,
    src/.../Application/AppointmentInfoRequests/AppointmentInfoRequestsAppService.cs,
    new DTOs (round + per-field diff)]
  - acceptance: `GetHistoryAsync(appointmentId)` returns rounds newest-first, each with
    requester/resubmitter + timestamps, "N of N fixed", the note, and per-field old->new
    diff rows; SSN values are masked in the diff. Unit test: diff reflects changed fields;
    SSN masked; ordering correct. Read gated by the existing read-access guard.

- B2-T4: Regenerate the Angular proxy.
  - approach: code
  - files-touched: [angular/src/app/proxy/appointment-info-requests/*]
  - acceptance: proxy exposes `getHistory`; generated via abp CLI, not hand-edited; app
    builds.

- B2-T5: Staff diff + history cards on the internal appointment detail.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment/components/internal-appointment-detail.component.ts,
    angular/src/app/appointments/appointment/components/internal-appointment-detail.component.html,
    angular/src/app/appointments/appointment/components/internal-detail.util.ts,
    angular/src/app/appointments/appointment/components/send-back-history.util.ts (new)]
  - acceptance: when a resolved round exists, the detail shows "What changed since your
    request" (old strikethrough -> new per field, SSN masked) and "Request history"
    (chronological rounds) above the field sections; the banner shows a "Resubmitted"
    badge + "Resubmitted: {date}" + "Round: {n}"; diff-row building is in the util and
    unit-tested; matches the prototype copy.

- B2-T6: Live-verify B2 and squash-merge B2.
  - approach: code
  - files-touched: []
  - acceptance: a full round (send back -> resubmit) shows the staff the diff + history with
    SSN masked; no console errors; squash-merge to feat/internal-user-pages and push.

## Risk / Rollback

- Blast radius (B1): internal + external appointment detail; the shared
  `StatusChangeEmailHandler` (add a case only; do not alter existing status emails); new
  appointment-scoped edit endpoints (insurance, claim examiner, document replace) +
  appointment denorm-column sync for AA/CE emails; new external seed permissions; the
  external resubmit/save path (server-side lock). The external detail extends the default-CD
  `AppointmentViewComponent`; follow the existing manual-subscribe pattern to avoid CD
  issues. New endpoints are additive; denorm-sync touches the appointment update path --
  keep changes scoped to the new methods to avoid regressing existing staff edit flows.
- Blast radius (B2): additive entity columns + migration (low risk), an additive read
  endpoint, and additive internal-detail cards.
- Rollback: each branch lands as a single squash commit -> revert that commit to undo. The
  B2 migration is additive (nullable columns) with a clean Down(); reversible.

## Verification

End-to-end, on the Falkinstein tenant (http://falkinstein.localhost:4250, pw 1q2w3E*r),
after each branch:

1. Bring up the override stack (docker compose) on shifted ports (Angular 4250, API 44377,
   AuthServer 44418); restart the relevant container after edits (no file-watch); reseed via
   the db-migrator service when seed/migrations change.
2. B1: as Staff Supervisor and as Intake Staff, open a Pending appointment, "Request info",
   check fields across groups, add hints + a note, send back; confirm the toast, the
   InfoRequested status, and the email (note + link) in docker logs. As the external user
   (Falkinstein external account), open the fix-it page; confirm only flagged fields are
   editable, hints show, progress fills, a non-flagged field cannot be changed, document
   replace works, resubmit is gated until all fixed, and resubmit returns the appointment to
   Pending. Confirm Info-Requested chips/counts on internal list, internal detail, external
   home, external detail. No console errors.
3. B2: after a resubmit, as staff confirm "What changed since your request" shows correct
   old->new per flagged field with SSN masked, "Request history" lists the rounds, and the
   banner shows the Resubmitted badge + Round. No console errors.
4. Synthetic data only throughout; SSN never appears unmasked in any list, diff, or email.
