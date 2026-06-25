# F-M05 (MED, UX) -- Re-evaluation submit is a silent dead-end unless a prior appointment is loaded

Status: OPEN. Confirmed on `main` @ 74e91563 (post frontend-rework merge #322), 2026-06-24.
No code changed for this. Written so it can be routed to the multi-tenant (db-per-tenant) session
and sequenced without conflicts.

## TL;DR
A re-evaluation (`Request a Re-evaluation` -> wizard at `?type=2`) cannot be submitted from the
Review step UNLESS the booker first loads a prior APPROVED appointment via the "Load prior
appointment" lookup on the Schedule step. The submit is correctly GATED on that source, but the
wizard (a) lets you complete all 9 steps and reach Review without ever loading one, and (b) renders
the guard's only feedback on the Schedule step -- so clicking "Submit request" from Review does
nothing visible (no POST, no toast, no error). The feature WORKS when the source is loaded (verified:
created A00019 via create-reval). This is a frontend-only UX defect.

## Symptom (what a user sees)
- Home -> "Request a Re-evaluation" -> fill all 9 wizard steps -> Review -> click "Submit request".
- Nothing happens: no network call, no error, no navigation. Button is enabled; form has no invalid
  controls; no console error/warning. Looks like a broken button.
- (Easy to fall into: the Patient step's "Find Existing Patient" lookup reuses patient details but is
  NOT the reval anchor; only the Schedule step's "Load prior appointment" sets the anchor.)

## Root cause (exact)
Frontend only. The submit is gated on a loaded source confirmation number.

1. Re-eval mode + the source lookup live in the wizard:
   - `angular/src/app/appointments/wizard/appointment-wizard.component.ts` extends
     `AppointmentAddComponent`; route `?type=2` sets `bookingMode='reval'`
     (`appointment-add.component.ts` ~699).
   - Template `appointment-wizard.component.html` (~73-99): only inside `@case ('schedule')` and
     `@if (isReevaluation)` does it render the "Prior confirmation number" input + "Load prior
     appointment" button + the `sourceLoadMessage`:
     ```html
     @case ('schedule') {
       @if (isReevaluation) {
         <div class="ra-reval"> ... <input #revalConf .../>
           <button (click)="loadRevalSource(revalConf.value)">Load prior appointment</button>
           @if (sourceLoadMessage) { <div class="ra-reval__msg">{{ sourceLoadMessage }}</div> }
         </div>
       }
       <app-appointment-add-schedule .../>
     }
     ```
2. `loadRevalSource` -> `loadSourceForPrefill(conf,'reval')` (`appointment-add.component.ts` ~1348):
   looks up the source by confirmation #, enforces a status gate (reval needs Approved --
   `checkSourceStatusForFlow` ~1408), prefills the form, and on success sets
   `this.sourceConfirmationNumber = confirmationNumber` (~1392).
3. The submit guard (`onSubmit`, `appointment-add.component.ts:1810`):
   ```csharp
   if (this.bookingMode !== 'new' && !this.sourceConfirmationNumber) {
       if (this.bookingMode === 'reval')
           this.sourceLoadMessage = 'Look up the prior approved appointment by confirmation number before submitting.';
       else
           this.sourceLoadMessage = 'The prior appointment could not be loaded, so this re-request cannot be submitted.';
       return;                       // <-- returns; no POST
   }
   ```
4. Create routing (`createAppointmentForCurrentMode`, ~1733) only calls
   `createReval(this.sourceConfirmationNumber, payload)` when the source is set; otherwise a reval
   would fall through to a plain create -- which is exactly what the guard above exists to PREVENT.

The two UX gaps that make the guard a silent dead-end:
- (a) No step gating: `stepState()` / the stepper let you advance through all steps to Review with
  no source loaded (no "error" state on the Schedule step, no block on Continue).
- (b) Wrong-screen feedback: `sourceLoadMessage` is only in the Schedule-step template. Submitting
  from Review sets it off-screen, so the user gets no feedback where they are.

## Reproduction (clean)
1. External user -> home -> "Request a Re-evaluation" (`/appointments/request?type=2`).
2. On Schedule, DO NOT use "Load prior appointment". Fill type/location/date/time. Continue.
3. Fill all remaining steps (you may use "Find Existing Patient" on the Patient step -- it does NOT
   set the reval source). Reach Review. Click "Submit request" -> nothing happens (no POST).
4. Jump back to the Schedule step -> the message "Look up the prior approved appointment by
   confirmation number before submitting." is now visible there.
Control (works): on Schedule, type an APPROVED appointment's confirmation # (e.g. A00010) into
"Load prior appointment" -> "Prior appointment loaded." -> pick a new slot -> Review -> Submit ->
`POST /api/app/appointments/create-reval/A00010` 200 -> new appointment created with full child
cascade. (Verified this run: produced A00019.)

## The fix (frontend-only) -- small + contained
In the wizard (`appointment-wizard.component.*`) and/or `AppointmentAddComponent`:
1. Surface the blocker where the user acts: when `isReevaluation && !sourceConfirmationNumber`, either
   disable the Review "Submit request" button (with a tooltip / inline note) OR show the
   `sourceLoadMessage` on the Review step.
2. Gate progression: mark the Schedule step `stepState()` as `'error'` and/or block Continue past
   Schedule until a source is loaded for reval/re-request, so the user can never reach Review without
   one.
No backend change required for the UX fix. Add a wizard spec: reval with no source -> Submit shows a
visible blocker on Review and does not POST; reval with a loaded approved source -> create-reval fires.

## Secondary observation (separate, BACKEND -- flag for the same session)
The reval child created via `create-reval/{conf}` has `OriginalAppointmentId = NULL` -- it is NOT
linked back to its source appointment via that column (reschedule children DO set
`OriginalAppointmentId`). So a re-evaluation is currently untraceable to the prior appointment via
that field. Decide whether create-reval should set `OriginalAppointmentId` (or another link). This
lives in the appointment app-service / `createReval` path -- the SAME backend area the multi-tenant
work may touch, so it IS conflict-relevant (see below).

## Relationship to the multi-tenant (db-per-tenant) work -- the decision to make
- The PRIMARY fix is FRONTEND ONLY (Angular wizard template + `AppointmentAddComponent` submit-gate
  UX + stepper). The multi-tenant epic is BACKEND (IMultiTenant entities, EF, migrations,
  Disable<IMultiTenant> sites). These do NOT overlap -- UNLESS the multi-tenant branch also edits
  `appointment-add.component.ts` / the wizard (e.g., tenant-scoped lookups). If it does not, F-M05's
  UX fix can be done as an independent small frontend PR with zero conflict risk.
- The SECONDARY item (set `OriginalAppointmentId` on reval) is BACKEND in the appointment app-service
  / create-reval flow. If the multi-tenant branch touches the appointment app-service or the
  reval/create-reval endpoints, FOLD this small change in there to avoid two branches editing the
  same service; otherwise do it as a separate backend follow-up after the merge.

## Severity
MED (UX). Re-evaluation is a top-level external action; today it dead-ends silently for any booker who
does not discover the Schedule-step "Load prior appointment" lookup. Not data-corruption or security.
The feature itself works once the source is loaded.

## Not a conflict with F-H01
Unrelated to F-H01 (attorney register-after-booking 500). Different surface (wizard UX vs
ExternalSignupAppService).
