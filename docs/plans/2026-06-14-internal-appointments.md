---
status: in-progress
date: 2026-06-14
slug: internal-appointments
branch: feat/redesign-internal-appointments
parent-branch: feat/internal-user-pages
surface: internal (staff) appointments list at /appointments
backend: YES -- one small per-status counts endpoint (no schema migration)
related:
  - "design_handoff_appointment_portal/Internal Appointments - Redesign.html"
  - "design_handoff_appointment_portal/components/in-appts.jsx"
  - "design_handoff_appointment_portal/styles/in-appts.css"
depends-on: "internal shell + dashboard (merged into feat/internal-user-pages @ f12cf3b)"
---

# Plan: Internal Appointments list (Prompt 10)

## Goal

Replace the legacy `AppointmentComponent` at `/appointments` with the redesigned
staff appointments list rendered inside the internal shell: status chips with
counts, search + filter drawer, a multi-select bulk bar (Export CSV + Delete),
a per-row kebab, a Pending-only "Decide by" column, and pagination -- for Staff
Supervisor + Intake (Intake cannot delete). Honors the dashboard deep-link
`?appointmentStatus=N`.

## Context / findings (research, verified)

- `AppointmentsAppService.GetListAsync(GetAppointmentsInput)` already supports
  status filter (`AppointmentStatus`), text/panel/date-range/type/location
  filters, and `SkipCount`/`MaxResultCount`/`Sorting` paging. The legacy list
  already reads `?appointmentStatus`.
- The list DTO (`AppointmentWithNavigationPropertiesDto`) carries every column:
  confirmation #, patient (SSN masked), type, appointment date, status, panel #,
  location, `DueDate` (decide-by), and Claim #/ADJ # via the eager-loaded
  `appointmentInjuryDetails[]`.
- Single-item actions exist (Approve/Reject/DirectCancel/Delete +
  Request-Reschedule/Cancellation) with `CaseEvaluation.Appointments.*` policies;
  Intake lacks `.Delete` (server-enforced).
- BACKEND-CHANGES.md **section E ("Change requests") is for the Workflow page
  (Prompt 13), NOT this list** -- the playbook's "E before Prompt 10" is a
  misattribution. No hard backend blocker.
- Only real gap: per-status **counts** for the chips (no aggregate endpoint).

## Decisions (2026-06-14)

1. **Data strategy:** server-paged table (existing GetListAsync) + a new small
   per-status counts endpoint so chips show true totals (scales with the queue).
2. **Row-action gating:** match the prototype (Reschedule/Cancel shown for the
   broader actionable set: Pending / Info Requested / Approved / Rescheduled).
   Wire to existing behaviors; flag non-Approved reschedule semantics at verify.
3. **Bulk:** multi-select bulk bar = Export (client-side CSV of selected) +
   Delete (non-Intake; loops the existing single delete endpoint).

## Architecture

- **Backend:** add `GetStatusCountsAsync(GetAppointmentsInput)` (or a slim input)
  returning per-pill counts honoring the SAME visibility + filters as the list,
  so the chips reflect the active filter set. Reuse `StatusPillPolicy` bucketing.
  Manual controller action (the AppService is RemoteService-disabled, like the
  dashboard). No schema migration.
- **Frontend:** `InternalAppointmentsComponent` (replaces AppointmentComponent),
  mounted at `/appointments` `''` child inside the shell. Server-driven: a
  signal-based query (filters + status + skip/take + sort) calls `getList`; chips
  call the counts endpoint; the kebab + bulk bar reuse existing modals/endpoints.
  Status -> pill via the frontend `appointmentStatusToPill` util.

## Tasks (one-by-one; commit each)

### Backend
**B1 -- per-status counts endpoint  [test-after]**
Add `GetStatusCountsAsync` to IAppointmentsAppService + AppointmentsAppService +
the controller, returning counts per the 6 pills (+ total) honoring the list's
visibility + filters. Reuse StatusPillPolicy. abp generate-proxy (appointments
module, scoped). Unit-test the bucketization mapping if a pure helper is added.

### Frontend
**F1 -- InternalAppointmentsComponent  [test-after]**
Build the redesigned list: header + status chips (counts from B1; click filters +
syncs `?appointmentStatus`), search, filter drawer (panel/type/location/date
range/booker), server-paged table (the columns above), per-row kebab
(Review/Reschedule/Cancel/Delete) with prototype gating + Intake-no-delete,
Pending-only Decide-by column (DueDate + client urgency badge), multi-select +
bulk bar (Export CSV + Delete), pagination + page-size, empty state. Reuse the
existing reschedule/cancel modals + delete confirm. Unit-test the row-action
gating + the decide-by urgency mapping.

**F2 -- _in-appts.scss  [code]**
Port `in-appts.css` onto global tokens; `@use` in styles.scss.

**F3 -- retire legacy list + wire route  [code]**
Repoint the in-shell `appointments` `''` child to the new component; delete the
legacy AppointmentComponent (+ abstract list bits no longer used) after live
sign-off. Keep `view/:id` (legacy detail) until Prompt 11.

## Risks
- Non-Approved Reschedule/Cancel semantics (prototype gating is looser than the
  legacy Approved-only); confirm behavior at verify, keep existing modals.
- Bulk delete on the primary queue -- non-Intake only, per-row confirm or a
  single bulk confirm; loops the existing audited delete.
- Server paging + chip counts must share the same filter set so they agree.
- Blast radius: `/appointments` is shared infra (the external detail + booking
  are role-split OUT already); only the internal list `''` child changes.

## Verification
- Backend: unit tests + build; counts endpoint returns correct per-pill numbers.
- Frontend: unit tests (gating, urgency) + build.
- Live (stack :4250): Supervisor + Intake -- chips + counts, `?appointmentStatus`
  deep-link from the dashboard, filters, server paging, kebab per status,
  Intake-no-delete, decide-by urgency, bulk export + delete. External users
  unaffected (they never reach the staff list).

## Out of scope
- Change-request inbox + consent (Prompt 13 Workflow / section E).
- Internal appointment DETAIL + approve/reject/edit (Prompt 11).
- Internal Add (Prompt 12).
