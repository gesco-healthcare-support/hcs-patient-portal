---
status: in-progress
date: 2026-06-15
slug: internal-add-appointment
branch: feat/redesign-internal-add
parent-branch: feat/internal-user-pages
surface: internal (staff) Add-appointment at /appointments/add (in-shell) + two detail-page follow-ups
backend: NO -- reuses CreateAsync + the existing attach/auto-approve flow
related:
  - "design_handoff_appointment_portal/Internal Add Appointment - Redesign.html"
  - "design_handoff_appointment_portal/components/ra-steps1.jsx, ra-steps2.jsx, ra-common.jsx, ra-after.jsx"
  - "design_handoff_appointment_portal/styles/ra-form.css"
depends-on: "internal shell + dashboard + list + detail (merged into feat/internal-user-pages)"
---

# Plan: Internal Add appointment (Prompt 12) + detail-page fixes

Two parts. Part A is the page (Prompt 12). Part B folds in two follow-up fixes on
the just-shipped internal appointment DETAIL page (Prompt 11), per Adrian's
request, so one implementing session ships both.

## Part A -- Internal Add appointment (Prompt 12)

### Goal
Replace the legacy `AppointmentAddComponent` form at the in-shell
`/appointments/add` with the redesigned booking WIZARD, rendered inside the
internal shell, for staff booking on a patient's behalf (select-or-create the
patient, all sections, auto-approve on create).

### Context / findings (research, verified)
- The legacy `AppointmentAddComponent` (55-field reactive form + 9 section
  child components) ALREADY implements every internal-specific behavior:
  - `isInternalBooker` -> auto-approve after the post-create attach cascade
    (created Pending, then approve once injuries + active Claim Examiner exist);
  - non-Patient bookers (internal staff) select-or-create the patient via the
    demographics email search (`/patients/for-appointment-booking` +
    `get-or-create`), patientId validator cleared at construction;
  - internal booking horizon = 90 days; the full create + attach sequence
    (employer, AA, DA, insurance, CE, injuries, accessors, documents) + the
    three booking modes (new / reval `?type=2` / re-request `?mode=rerequest`).
- The external `AppointmentWizardComponent` already EXTENDS
  `AppointmentAddComponent` and adds only the redesigned `.ra-*` stepper UI +
  the external navbar chrome + localStorage draft autosave.
- The internal Add PROTOTYPE reuses the SAME `ra-` wizard step components as the
  external wizard; it differs only by: rendering inside the shell (NO external
  navbar), the staff "select/create patient" affordance (already covered by the
  demographics email search for non-Patient bookers), staff confirmation/done
  messaging ("created on the patient's behalf ... now Pending ... Go to
  appointments"), and the sticky footer offset past the sidebar.
- **No backend changes.** Create = `POST /api/app/appointments`
  (`Appointments.Create`); internal auto-approve already chains client-side.

### Decision (ACCEPTED 2026-06-15)
**Architecture -- conditionalize `AppointmentWizardComponent` to serve internal
staff too** (approved), driven by the inherited `isInternalBooker` getter:
- `@if (!isInternalBooker)` around the external navbar (the shell provides chrome
  for the internal route); internal done-screen + submit-confirm copy under
  `@if (isInternalBooker)`.
- Route the in-shell `/appointments/add` to the wizard; the shell wraps it.
- Rationale: the engine + stepper already do everything; the only deltas are
  chrome + copy. Conditionals are ADDITIVE (external path unchanged when
  `isInternalBooker` is false), so low regression risk. Avoids duplicating the
  ~600-line stepper template.
- ALTERNATIVE (more isolation, higher cost): a separate
  `InternalAppointmentAddComponent extends AppointmentWizardComponent`/
  `AppointmentAddComponent` with its own copied stepper template. Rejected
  unless Adrian wants the external wizard left strictly untouched.

### Architecture (accepted option)
Reality check during build (2026-06-15): the wizard has NO done-screen + NO
submit-confirm modal -- `onSubmit()` (inherited) navigates to `/` on success.
So "internal done copy" becomes: gate the navbar, conditionalize the visible
header + review-step note copy, and redirect internal staff to `/appointments`
post-submit. Concretely:
- `appointment-wizard.component.html`: gate `<app-external-navbar>` on
  `!isInternalBooker`; bind the review-step note to a `reviewSubmitNote` getter
  (patient-voiced "contact staff" copy is wrong for staff). (The patient picker
  is already handled by the demographics section for non-Patient bookers.)
- `wizard-copy.util.ts` (NEW, pure functions): `wizardTitle/Subtitle/Eyebrow`
  + `reviewSubmitNote`, keyed off `(isInternal, isReevaluation)`. The wizard
  getters delegate to these; the util is unit-tested (matches the established
  internal-appointments.util / internal-detail.util pattern). Keep external
  copy byte-identical.
- Post-submit navigation: extract `protected navigateAfterBooking()` in
  `AppointmentAddComponent` (defaults to `navigateByUrl('/')`, byte-identical
  external behavior at the two existing call sites -- onSubmit + retry) and
  override it in the wizard to send internal bookers to `/appointments`.
- Routing (`app.routes.ts`): in-shell `INTERNAL_SHELL_CHILDREN` `appointments/add`
  -> load `AppointmentWizardComponent` (was `AppointmentAddComponent`). Keep the
  EXTERNAL top-level `/appointments/add` (legacy form, `externalUserOnlyMatchGuard`)
  for external `?type=` booking. Add `externalUserOnlyMatchGuard` to
  `/appointments/request` so internal staff never land on the chrome-less wizard.
- Footer offset: the `.ra-foot` sticky footer must clear the 256px sidebar when
  in-shell (collapsed: 72px). Scope via the shell (e.g. `.in .ra-foot { left: 256px }`
  / `.in--collapsed .in .ra-foot { left: 72px }`) in `_in-shell.scss`, mirroring
  the prototype's `in-content--flush` + `.in .ra-foot` override.
- Retire: the in-shell legacy `AppointmentAddComponent` route only. KEEP the
  component itself (it's the base class the wizard extends + the external
  `/appointments/add` still uses it).

### Tasks (one-by-one; commit each)
- **A1 -- wizard chrome + copy  [test-after]**: gate the navbar on
  `!isInternalBooker`; add `wizard-copy.util.ts` (+ spec) and delegate the
  header + `reviewSubmitNote` getters to it. External copy unchanged.
- **A2 -- post-submit navigation hook  [code]**: extract
  `navigateAfterBooking()` in `AppointmentAddComponent` (external -> `/`,
  byte-identical) + override in the wizard (internal -> `/appointments`).
- **A3 -- route in-shell /appointments/add -> wizard  [code]**: repoint the
  child route (lazy import); the shell topbar + list "New appointment" buttons
  both target `/appointments/add`, so this covers them.
- **A4 -- footer offset inside the shell  [code]**: `.ra-foot` (position:fixed,
  left:0) clears the sidebar -- `.in .ra-foot{left:256px}` /
  `.in--collapsed .ra-foot{left:72px}` in `_in-shell.scss`.
- **A5 -- guard /appointments/request external-only  [code]**: add
  `externalUserOnlyMatchGuard` so internal users don't get the chrome-less wizard.

## Part B -- Internal appointment DETAIL fixes (Prompt 11 follow-ups)

### B1 -- clear "Back to appointments" button
The detail only exposes a breadcrumb `Appointments` link bound to `back()`
(`/appointments`). Add an explicit, obvious **Back** button (e.g. `af-btn--glass`
in the `.ad-actions` banner row, leading `chevLeft`/`arrowLeft` icon, label
"Back to appointments") wired to the existing `back()`. Keep the breadcrumb.
File: `internal-appointment-detail.component.html` (+ reuse the inherited
`back()` in `internal-appointment-detail.component.ts`).

### B2 -- detail-page margin / whitespace
`_ad-detail.scss` caps `.ad-wrap`, `.ad-banner__in`, and the meta row at
`max-width: 1080px; margin: 0 auto` -- correct for the NARROW external read-only
detail, but too tight for the dense, editable internal detail (more whitespace
than the list). Do NOT widen globally (external detail wants 1080). Add an
internal-only modifier:
- In `internal-appointment-detail.component.html`, add a modifier class on the
  root `<div class="ad">` (e.g. `class="ad ad--wide"`).
- In `_ad-detail.scss` (or a small scoped block), override for the modifier:
  `.ad--wide .ad-banner__in, .ad--wide .ad-wrap { max-width: 1560px; }` (a value
  that fills the shell content area; keep the 26px padding). Verify the external
  detail (plain `.ad`) is unchanged at 1080.

## Verification
- Build clean; karma for any new util/getter; full internal-* specs green.
- Live (stack :4250, Supervisor + Intake):
  - **Add**: in-shell `/appointments/add` renders the wizard inside the shell
    (no external navbar); staff can select-or-create a patient, complete the
    steps, submit -> appointment created + auto-approved (or Pending if approve
    gates unmet) -> internal done copy + "Go to appointments". External wizard
    at `/appointments/request` UNREGRESSED (navbar still shows; stays Pending).
  - **Detail**: Back button returns to `/appointments`; detail content fills the
    width (no 1080 cap); external detail still 1080.
- Squash-merge to `feat/internal-user-pages`, push, resync local (restart angular).

## Risks
- Regressing the shipped external wizard -- mitigate: additive `isInternalBooker`
  conditionals + re-verify external booking end to end.
- Internal `/appointments/add` is declared in two route scopes (external top-level
  legacy + in-shell) -- only change the in-shell child.
- Auto-approve needs >=1 injury + active Claim Examiner before it can approve;
  otherwise the booking stays Pending (expected) -- surface clearly, don't error.
- `.ra-foot` sticky footer + the shell sidebar offset is viewport/collapse
  dependent -- verify expanded + collapsed.

## Out of scope
- External booking changes (the external wizard/legacy add are unchanged beyond
  the navbar conditional).
- Change-request inbox + consent (Prompt 13).
