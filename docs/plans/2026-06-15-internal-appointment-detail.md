---
status: in-progress
date: 2026-06-15
slug: internal-appointment-detail
branch: feat/redesign-internal-appointment-detail
parent-branch: feat/internal-user-pages
surface: internal (staff) appointment detail at /appointments/view/:id
backend: NO -- every lifecycle endpoint + modal already exists
related:
  - "design_handoff_appointment_portal/Internal Appointment Detail - Redesign.html"
  - "design_handoff_appointment_portal/components/in-detail.jsx"
  - "design_handoff_appointment_portal/styles/ad-view.css"
depends-on: "internal shell + dashboard + appointments list (merged into feat/internal-user-pages)"
---

# Plan: Internal appointment detail (Prompt 11)

## Goal

Replace the legacy `AppointmentViewComponent` template at the in-shell
`/appointments/view/:id` with the redesigned staff detail: status banner +
office actions (approve / reject / reschedule / cancel / request-info), all
appointment sections as `.ad-card`s, an Edit-details mode over the existing
reactive form, the claim-info table, documents, authorized-users management,
and an internal staff panel. Inherits the legacy component's full data + form +
action engine; only the presentation changes.

## Context / findings (research, verified)

- `AppointmentViewComponent` (1768-line monolith) already implements: load via
  `getWithNavigationProperties(id)`, the reactive `form` (panel + patient +
  employer + applicant/defense attorney, ~60 controls), atomic `save()` (patient
  + appointment + employer + AA + DA), the lifecycle modals + flags (approve /
  reject / direct-cancel / reschedule-request / cancellation-request /
  request-info), authorized-user CRUD, the read-only claim-info table
  (`injuryDetails`), read-only Claim Examiner + Insurance (nav-props), demographics
  download, and re-request.
- `ExternalAppointmentDetailComponent` is the EXTENDS precedent: `super.ngOnInit()`,
  re-inject the parent's private services under local names, new template binding
  inherited public members, re-list modal imports.
- All backend endpoints + the 5 modal components already exist. **No backend work.**
- CSS already global: `_ad-detail.scss` (ad-banner/card/dl/field/table/note/
  statepill/actions/wrap), `_ra-wizard.scss` (ra-grid/field/input/select/modal/
  scrim/rowbtn/radios + tint-*), `_mp-profile.scss` (mp-editbtn/foot), `_af-buttons.scss`.
  Documents reuse `<app-appointment-documents>`. **Little/no new CSS.**

## Decisions (2026-06-15)

1. **Extend, don't fork**: `InternalAppointmentDetailComponent extends
   AppointmentViewComponent` -- inherit the whole engine, override only the
   template + the change-request success handler.
2. **Single Edit-details toggle** (not the prototype's per-section toggle): the
   engine's `save()` is atomic across patient/appointment/employer/AA/DA, so a
   per-section Save would misleadingly persist everything. One `editMode` flips
   the editable sections (panel + patient + employer + AA + DA) between read
   ledgers and the form inputs, with Save (-> `save()`) / Cancel (snapshot revert).
   CE + Insurance + Claim-info + Staff panel stay read-only (the engine never
   persisted them; booking form is canonical for those).
3. **Reschedule / Cancel = request modals + auto-approve** (same as the list,
   Prompt 10): reuse `RescheduleRequestModalComponent` +
   `CancellationRequestModalComponent`, override `onChangeRequestSucceeded` to
   chain `planAutoApprove` when the caller can approve. (Not the parent's
   external "stays Pending" handler, not direct-cancel.)
4. **Actions per status** (prototype ID_ACT + request-info): Pending =
   approve/reject/reschedule/cancel/request-info; Approved + Rescheduled =
   reschedule/cancel; Rejected = re-request (creator only); else none. Plus
   change-log + demographics on all. Server permissions remain authoritative.

## Architecture

- `internal-detail.util.ts` (pure, tested): `detailActions(pill)` action gating +
  `bannerVariant(pill)` / `statusLabel(pill)`.
- `internal-appointment-detail.component.ts`: extends the parent; re-injects
  Router/Route/AppointmentService/ConfigState + ChangeRequestApprovalService +
  Permission/Toaster/Localization; adds banner/meta/fv accessors, `editMode` +
  snapshot revert, action helpers, staff-panel accessors (reuse `decideByInfo`
  from the appointments list util), override `onChangeRequestSucceeded`.
- `internal-appointment-detail.component.html`: re-skin of the legacy template
  into `.ad` banner + `.ad-card` sections + `ra-grid` edit inputs (bindings
  lifted verbatim from the legacy template) + the 5 lifecycle modals + a
  re-skinned authorized-user `ra-modal`.
- Route: in-shell `view/:id` (APPOINTMENT_ROUTES) -> the new component. External
  `view/:id` already role-split out. Keep `AppointmentViewComponent` as the base.

## Tasks (one-by-one; commit each)

**T1 -- util + spec  [test-after]**  detailActions + banner helpers + karma spec.
**T2 -- component  [code]**  the extends class + accessors + editMode + override.
**T3 -- template  [code]**  the redesigned .ad template (lift form bindings).
**T4 -- route swap  [code]**  point view/:id at the new component.
**verify**  build + util tests + live per role/status (load, banner, actions,
edit+save, reschedule/cancel auto-approve, request-info, claim/docs/auth-users,
staff panel), then squash-merge + push + resync local stack.

## Risks
- Large template; bindings lifted from the proven legacy template to de-risk.
- Reschedule/Cancel on non-Approved (request semantics) -- same risk as the list;
  reuse the same modals + verify behavior live.
- Edit snapshot revert must restore the full form (single section editing at a
  time is not enforced; one global editMode avoids partial-state confusion).
- Blast radius: only the in-shell `view/:id` template; external detail untouched;
  parent base class unchanged.

## Out of scope
- Inline claim add/edit/delete (booking form is canonical; parent treats claims
  read-only here) -- the prototype's claim modal is NOT wired to a backend path
  on this page.
- Internal Add (Prompt 12); change-request inbox (Prompt 13).
