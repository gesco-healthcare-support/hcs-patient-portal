# Plan: Request-an-Appointment Wizard (redesign slice)

Status: in-progress
Branch: `feat/redesign-appointment-wizard` (stacked on `feat/redesign-external-home`)
Date: 2026-06-13

## Goal

Recreate the redesigned multi-step booking wizard from the design handoff
(`Request an Appointment - Redesign.html` + `components/ra-*.jsx` +
`styles/ra-form.css`) as Angular standalone components, wired to the existing
backend. Same wizard serves "Request an Appointment" and "Request a
Re-evaluation" (the existing `?type=2` reval mode adds a prior-appointment
lookup bar on step 1).

## Sources (NOT the OLD app -- replication is done)

1. Design handoff prototype = what to build (9 steps, fields, `.ra-*` CSS).
2. Current `AppointmentAddComponent` (3,713 lines) = the proven FormGroup,
   cascades, and multi-POST submit to mirror.
3. Backend create surface = wire targets (already complete).

## Backend: NO CHANGES

`POST /api/app/appointments` + all lookups (slots via
`/doctor-availabilities/lookup`, types, locations, custom fields, field-configs,
attorney prefill, `patients/for-appointment-booking/get-or-create`) already
exist. Party data (attorneys, injuries, examiner, employer, insurance, docs,
accessors) attaches via separate POSTs after the appointment is created, then
internal bookers call `/approve`. There is no draft status; the wizard's draft
is client-side (`localStorage`) only.

## Route strategy (mirror the home slice)

- Build the new `AppointmentWizardComponent` and route it at a TEMP path
  (`/appointments/request`) for testing so `/appointments/add` keeps working.
- After live sign-off: swap `/appointments/add` to the wizard, update the
  home action cards, and delete `AppointmentAddComponent` + its 8 section
  children.
- The home already navigates to `/appointments/add?type=1` (appointment) and
  `?type=2` (re-evaluation) -- keep those params.

## Component architecture

- `AppointmentWizardComponent` (shell): owns the single reactive `FormGroup`
  (mirrors the existing component's controls), all cascade subscriptions, the
  stepper state machine, per-step validation, `localStorage` draft autosave,
  the submit-confirm modal, the success state, and the multi-POST submit
  orchestration. Renders `.ra-head` + `RaStepper` + the active step + the
  sticky `.ra-foot`.
- One template-only step component per step (take `@Input() form`), styled
  `.ra-*` per the prototype. Reuse the existing section children's field sets +
  wiring as the reference; restyle to `.ra-*` (do not reuse the Bootstrap
  markup).
- Reuses foundation primitives (`app-icon`, `app-status-pill`) + the
  `redesign-shell` body class + `app-external-navbar` from the home slice.

## Steps (9 standard; 7 for Claim Examiner -- attorney steps hidden)

0. Schedule -- type, panel #, location, date, time (cascades:
   type+location -> slot lookup -> dates/times).
1. Patient + Employer -- demographics (+ existing-patient picker for
   non-patient bookers), SSN (write-only), address, language/interpreter,
   employer card.
2. Applicant Attorney -- toggle (self-represented confirm modal); locked for
   AA booker.
3. Defense Attorney -- toggle (none-assigned confirm modal); locked for DA.
4. Insurance -- toggle (no-insurance confirm modal).
5. Claim Examiner -- always required; prefilled for CE booker.
6. Claim -- repeatable injury rows (add/edit modal, body parts), >=1 required.
7. Documents -- drag/drop staging, per-doc label, PQME panel-strike-list gate.
8. Review -- per-section summaries with edit-jumps, additional authorized
   users, submit-confirm modal, success screen.

Field-level inventory + validation: see the design-handoff research and the
existing `AppointmentAddComponent` (mirror exactly).

## Build order

1. Port `ra-form.css` -> wizard SCSS (shared wizard chrome + grid + controls).
2. Scaffold `AppointmentWizardComponent` shell: `.ra-head`, stepper, footer
   nav, step container, draft autosave, temp route. Render placeholder steps.
3. Build steps 0-8 as `.ra-*` step components, wiring the mirrored FormGroup +
   cascades.
4. Wire the multi-POST submit + confirm modal + success state.
5. Reval mode (`?type=2`): prior-appointment lookup bar + prefill.
6. Live test each role variant; sign-off; swap route + delete old component.

## Risks / notes

- Largest component in the app; mirror the proven cascade/submit sequence
  precisely to avoid regressing booking. Build behind the temp route until
  parity is verified live.
- ClaimExaminerEmail is server-overwritten for CE-role bookers (read-only).
- AppointmentLanguage has no seed data -- handle an empty language list.
- `Appointments.Create` is permission-gated -- surface a 403 gracefully.
