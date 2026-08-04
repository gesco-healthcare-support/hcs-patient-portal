---
feature: Extract a reusable availability calendar from the booking component
date: 2026-08-03
status: done
base-branch: main
related-issues: []
---

# Phase 4a: extract a reusable availability calendar

Phase 4a of `2026-07-31-reschedule-cancel-calendar-integration-epic.md`. PREREQUISITE for 4b
(staff pick the reschedule date). Phases 1, 2 and 3 are shipped; the 4a-4e chain is strictly
sequential.

## Goal

Date-and-time availability picking lives in ONE standalone component that the booking page, the
booking wizard and (in 4b) the reschedule flow all use, so the lead-time and horizon rules cannot
drift between them.

## Context & decisions

Why now: 4b must let staff pick a reschedule date under the SAME rules as booking. Today those rules
live as ~13 members on `AppointmentAddComponent` (3763 lines), and the reschedule modal has its own
crude `<select>` with NO lead-time or horizon gating at all -- so without this extraction 4b would
either duplicate the rules or ship a second, inconsistent set.

Blast radius, VERIFIED against `main` at `61b43501` (the epic-wide research had assumed worse):

- `AppointmentWizardComponent` EXTENDS `AppointmentAddComponent`
  (`appointment-wizard.component.ts:121-122`) but overrides exactly ONE method,
  `navigateAfterBooking()` (`:619`), and references ZERO calendar members in TypeScript. The
  dependency is template-only.
- Exactly TWO templates consume the calendar members, binding the SAME five:
  `appointment-add.component.html` and `appointment-wizard.component.html:121-130`.
- `appointment-view.component.ts` is NOT a consumer -- it names `AppointmentAddComponent` only in
  comments (`:566`, `:966`, "mirroring...").

Resolved decisions:

- Decision: build a STANDALONE component rather than extracting a service (Adrian, 2026-08-03,
  choosing the fuller consolidation after being shown that the service route could ship with zero
  template churn). Consequence accepted: this rewrites the schedule step of the live booking flow,
  so live re-verification of BOTH booking surfaces is a HARD GATE in this plan, not an optional step
  -- phase 3 proved a frontend change can type-check, pass every test, and still render wrong.
- Decision: the reusable unit is DATE + TIME ONLY. The shell
  (`appointment-add-schedule.component`) is a four-column card -- AppointmentType selector, Panel
  Number, Location selector, then the date picker + time options. Only the last is availability.
  Retiring the shell wholesale would pull the type/panel/location selectors into an "availability
  calendar", i.e. a kitchen sink. This is also what 4b needs: rescheduling an EXISTING appointment
  has a fixed location and type, so it wants date+time and nothing else.
- Decision: therefore the shell is SLIMMED (keeps its three selectors, embeds the new component),
  not deleted. That also means the two parent templates change only by DROPPING the five calendar
  bindings they currently pass down.
- Decision: the new component is CONTROLLED -- inputs `selectedDate` / `selectedSlotId`, output
  `slotSelected` -- rather than taking a `FormGroup`. One mechanism, idiomatic Angular, and it works
  for the reschedule modal in 4b, which does not share booking's form. The shell adapts the output
  onto its existing `formControlName` bindings so the booking form contract is unchanged.
- Decision: the SERVER remains authoritative on lead time. The client rules stay a UX mirror, as
  today (`DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync` computes
  `minDate = (AvailableDateFrom ?? Today) + SystemParameter.AppointmentLeadTime`). This phase must
  NOT change server behaviour or the booking policy.

Non-goals: changing any availability RULE, touching the booking policy validator, the type/panel/
location selectors, and the reschedule modal itself (that is 4b).

## All needed context

Members to move off `angular/src/app/appointments/appointment-add.component.ts` (all verified
present on current main):

| Kind      | Member                                             | Line                                   |
| --------- | -------------------------------------------------- | -------------------------------------- |
| state     | `isAvailableDatesLoading`                          | 243                                    |
| state     | `availableDateKeys` (private Set)                  | 252                                    |
| state     | `appointmentTimeOptions`                           | 272                                    |
| rule      | `minimumBookingDays = 3`                           | 258                                    |
| rule      | `minimumBookingRuleMessage`                        | 259                                    |
| rule      | `externalMaxBookingDays = 60`                      | 265                                    |
| rule      | `internalMaxBookingDays` + `maxBookingDays` getter | ~266-270                               |
| behaviour | `loadAvailableDatesBySelection()`                  | body ~3371-3426; called 718, 720, 2022 |
| behaviour | `fetchAllAvailableSlots()`                         | 3373                                   |
| behaviour | `toDateKeyFromApi()`                               | 3437                                   |
| behaviour | `markAppointmentDateDisabled`                      | 2219                                   |
| behaviour | `isAvailableAppointmentDate`                       | (beside the above)                     |
| behaviour | `isBeyondAbsoluteBookingCeiling`                   | 2231                                   |

- Shell to slim: `angular/src/app/appointments/sections/appointment-add-schedule.component.ts`
  (104 lines, 12 `@Input`s at `:74-100`) + its `.html`. Its date column is `:49-101`; the
  `ngbDatepicker` + custom `[dayTemplate]` live at `:51-97`, the time options at `:98-101`.
  The five inputs that go away: `isAvailableDatesLoading`, `appointmentTimeOptions`,
  `minimumBookingRuleMessage`, `markAppointmentDateDisabled`, `isAvailableAppointmentDate`.
  The ones that STAY: `form`, `checkForAppointmentTypeSelected`, `getAppointmentTypeLookup`,
  `getLocationLookup`, `isFieldInvalid`, `isPqmeType`, `noBookableDatesMessage`.
- Data source (unchanged): `DoctorAvailabilityService.getDoctorAvailabilityLookup` in
  `angular/src/app/proxy/doctor-availabilities/doctor-availability.service.ts`.
- Pattern to mirror for a pure, unit-tested helper beside a thin component:
  `angular/src/app/doctor-availabilities/schedule/schedule-calendar.util.ts` +
  `.util.spec.ts` from phase 3.
- HARD-WON phase 3 lesson that applies directly: a pure mapper tested only against itself proves
  NOTHING about a third-party contract. `ngbDatepicker`'s `[markDisabled]` / `[dayTemplate]` are the
  third-party contract here, so at least one spec must render the component and assert on the
  DOM, not only call the helpers.
- Tooling traps (from the roadmap): `ng lint` is broken locally, use `npx eslint <paths>`; run
  `npx prettier --check` before committing; the angular container serves a STATIC build made at
  container start, so `docker restart main-angular-1` + ~75s before any live check.

## Tasks

### Task 1 - pure availability rules, extracted and tested

- what: CREATE `angular/src/app/appointments/availability-calendar/availability-rules.ts` exporting
  pure functions: `toDateKey(value)`, `isWithinLeadTime(date, today, leadDays)`,
  `isBeyondHorizon(date, today, maxDays)`, `buildAvailableDateKeys(slots)`, and
  `isSelectableDate(date, { today, leadDays, maxDays, availableKeys })`. Move the logic from
  `toDateKeyFromApi` (`:3437`), `isBeyondAbsoluteBookingCeiling` (`:2231`) and the predicate bodies
  of `markAppointmentDateDisabled` (`:2219`) / `isAvailableAppointmentDate` VERBATIM in behaviour --
  this task changes no rule.
- pattern: `schedule-calendar.util.ts` + its spec (phase 3): pure exported functions, no DI.
- approach: tdd (these decide whether a date is bookable; a wrong answer double-books or blocks
  booking)
- acceptance (EARS):
  - WHEN a date is fewer than `leadDays` after today, THE SYSTEM SHALL report it not selectable.
  - WHEN a date is beyond the ABSOLUTE 90-day ceiling, THE SYSTEM SHALL report it not selectable,
    for every role.
  - WHEN a date has no matching availability key, THE SYSTEM SHALL report it not selectable.
  - WHERE a date satisfies lead time, the ceiling AND has an availability key, THE SYSTEM SHALL
    report it selectable.
  - WHEN no availability has loaded at all, THE SYSTEM SHALL report every date not selectable.
  - WHERE the appointment type is not yet chosen, THE SYSTEM SHALL disable nothing.

  CORRECTED DURING BUILD (2026-08-03): an earlier acceptance line said a date beyond `maxDays` is
  not selectable and that the horizon is 60 for external bookers. READING THE CODE DISPROVED IT.
  `markAppointmentDateDisabled` (`:2219`) disables on `isBeforeMinimumBookingDate` and
  `isBeyondAbsoluteBookingCeiling`, and the latter compares against `internalMaxBookingDays` (90)
  for EVERY role -- the comment at `:3585-3590` is explicit that between 60 and 90 days external
  users still SEE the dates and instead get the contact-staff notice on SELECTION (handled in
  `onAppointmentDateChanged`). So the 60-day external horizon is an interception, not a disable.
  Encoding it as a disable would have silently removed dates external users can currently pick.
  The extracted rules therefore take a single `ceilingDays` and know nothing about roles.

### Task 2 - the standalone availability calendar component

- what: CREATE `angular/src/app/appointments/availability-calendar/availability-calendar.component.ts`
  (+ `.html`, `.scss`): standalone, `OnPush`, importing `NgbDatepickerModule`. Inputs
  `locationId`, `appointmentTypeId`, `isInternalBooker`, `selectedDate`, `selectedSlotId`,
  `disabled`. Output `slotSelected` emitting `{ date: string; doctorAvailabilityId: string }`. It
  owns the fetch (`getDoctorAvailabilityLookup`), the loading flag, the derived time options, and the
  `[markDisabled]` / `[dayTemplate]` wiring, delegating every decision to task 1's rules.
- pattern: the date column of `appointment-add-schedule.component.html:49-107` for the datepicker +
  dayTemplate markup, and the TIME column at `:109-129` for the `<select>`;
  `internal-schedule.component.ts` (phase 3) for a standalone OnPush component that fetches on
  input change.
- approach: test-after (UI wiring; rules are unit-tested in task 1)

SCOPE CORRECTED DURING BUILD (2026-08-03) -- task 2 is BIGGER than the 13-member table above.
Reading the loader body (`appointment-add.component.ts:3360-3426`) and the rest of the shell
template showed the reusable unit is DATE + TIME + SLOT ID, not date alone:

- The shell also renders the TIME `<select>` (`formControlName="appointmentTime"`, options from
  `appointmentTimeOptions`, with a `Appointment:NoSlotsRemaining` message), so the component must own
  that column too or the two halves would live in different places.
- The loader maintains a second structure, `availableSlotsByDate`
  (`Map<dateKey, {time, doctorAvailabilityId}[]>`), which is what turns a chosen DATE into the time
  options and the `doctorAvailabilityId`.
- It is RACE-GUARDED by `availableSlotsRequestVersion` (`:3370`); a stale response is dropped. This
  MUST be preserved -- without it, switching location/type quickly can apply the wrong office's
  slots.
- It writes THREE form values (`appointmentDate`, `appointmentTime`, `doctorAvailabilityId`) and
  calls `clearTimeSlots()`, so the component's output has to carry the slot id, not just a date.
  Additional members to move beyond the table: `availableSlotsByDate`,
  `availableSlotsRequestVersion`, `clearTimeSlots`, and the slot-population half of
  `onAppointmentDateChanged`. The output therefore emits
  `{ date: string; time: string | null; doctorAvailabilityId: string | null }` and the shell patches
  all three controls, keeping the form contract identical.
- acceptance (EARS):
  - WHEN location and appointment type are both set, THE SYSTEM SHALL load availability and enable
    only selectable dates.
  - WHEN the user picks a date, THE SYSTEM SHALL emit `slotSelected` with that date and its slot id.
  - WHILE availability is loading, THE SYSTEM SHALL show a loading state and select nothing.
  - WHERE no bookable date exists in range, THE SYSTEM SHALL show the no-bookable-dates message
    rather than an empty calendar with no explanation.
  - THE SYSTEM SHALL include at least one spec that RENDERS the component and asserts disabled days
    in the DOM, not merely that the helper returned false.
  - WHEN a date is selected, THE SYSTEM SHALL DISPLAY it as text in the picker input. (Added after
    defect 6; the original acceptance list covered state and disabled days but never the one
    observable that was broken.)

SECOND SCOPE ADDITION (2026-08-03) -- one more file: `availability-date-adapter.ts` (+ spec), an
`NgbDateAdapter<string>` PINNED on the component via `providers`. Not foreseen when the plan was
written, because the plan assumed the datepicker's model shape was a property of the markup being
moved. It is not: it is resolved from the HOST injector, so it changed meaning the moment the markup
moved into a component that different hosts embed. See defect 6.

### Task 3 - slim the shell onto the new component

- what: MODIFY `appointment-add-schedule.component.html` -- replace the date column
  (`:49-101`) with `<app-availability-calendar>`, wiring its `slotSelected` output onto the existing
  `appointmentDate` / time form controls so the FORM CONTRACT IS UNCHANGED. MODIFY
  `appointment-add-schedule.component.ts` -- delete the five now-unused `@Input`s and keep the other
  seven.
- pattern: the existing `formControlName="appointmentDate"` binding it replaces; keep the card
  markup and the three selector columns untouched.
- approach: test-after
- acceptance (EARS):
  - THE SYSTEM SHALL keep writing the chosen date and time into the same form controls with the same
    value shapes as before.
  - THE SYSTEM SHALL no longer declare `isAvailableDatesLoading`, `appointmentTimeOptions`,
    `minimumBookingRuleMessage`, `markAppointmentDateDisabled` or `isAvailableAppointmentDate` as
    inputs.

### Task 4 - drop the moved members from the base component and both parents

- what: MODIFY `appointment-add.component.ts` -- remove the 13 members listed above and the now-dead
  cascade calls at `:718`, `:720`, `:2022`. MODIFY `appointment-add.component.html` AND
  `appointment-wizard.component.html` (`:121-130`) -- drop the five calendar bindings from
  `<app-appointment-add-schedule>`. Leave `navigateAfterBooking()` and every non-calendar member
  alone.
- pattern: n/a (deletion). The wizard's TS needs NO change -- verified it references none of these.
- approach: test-after
- acceptance (EARS):
  - WHEN the solution builds, THE SYSTEM SHALL contain no remaining reference to the removed members.
  - THE SYSTEM SHALL leave `AppointmentWizardComponent`'s TypeScript unmodified.

### Task 7 - collapse booking onto ONE route (ADDED 2026-08-04, after a live 404)

- what: DELETE `/appointments/add` entirely. The internal shell child moves to
  `path: 'appointments/request'`, the task-6 redirect route and `legacy-add-redirect.ts` (+ spec) are
  deleted, and all three navigations are repointed: the topbar button
  (`internal-shell-layout.component.html:139`), the list action
  (`internal-appointments.component.ts:157`) and internal re-request
  (`appointment-view.component.ts:808`).
- WHY, and this supersedes task 6: task 6's redirect BROKE internal booking. An internal Intake
  Staff clicking New Appointment was redirected to `/appointments/request`, which is external-only,
  so they fell through to the `**` wildcard and got "Page not found". **`canMatch` did NOT prevent a
  `redirectTo` route from applying** -- the access token proved `role: "Intake Staff"` while the
  external-only guard's route still fired. Never combine `canMatch` with `redirectTo`; and more
  usefully, never keep two paths to one screen split by role, because the split is the failure.
- result: ONE booking path, `/appointments/request`, with the role split only on CHROME -- internal
  renders it as a shell child (sidebar), external matches the chrome-less copy declared before the
  shell parent. `/appointments/add` now 404s for both roles, verified live.
- approach: code (routing) -- the deleted redirect spec is not replaced, because the construct it
  tested no longer exists.
- acceptance (EARS):
  - WHEN an internal user clicks New Appointment, THE SYSTEM SHALL render the wizard inside the shell.
  - WHEN an external user books from home, THE SYSTEM SHALL render the chrome-less wizard with
    `?type` preserved.
  - WHEN anyone opens `/appointments/add`, THE SYSTEM SHALL render the 404 page.

### Task 6 - retire the legacy add route (2026-08-03, SUPERSEDED by task 7)

Kept for the record because its failure is the lesson. The redirect described below was the wrong
mechanism; task 7 deletes the path instead.

- why: task 5 exposed that "both booking surfaces" was a stale premise. `/appointments/add` resolves
  to the WIZARD for internal staff, and the legacy `AppointmentAddComponent` sat behind the same path
  under `externalUserOnlyMatchGuard` with NOTHING navigating to it -- external booking had already
  moved to `/appointments/request` (`external-home.component.ts:355`,
  `external-appointment-detail.component.ts:270`). It was reachable only by typing the URL, so it was
  a surface that had to be re-verified on every booking change while silently drifting.
- what: MODIFY `app.routes.ts` -- replace the legacy route's `loadComponent` with
  `redirectTo: legacyAddRedirect`, and drop the now-unused eager import. CREATE
  `appointments/legacy-add-redirect.ts` (+ spec).
- a REDIRECT, not a deletion: the internal shell is gated by `internalUserOnlyMatchGuard`
  (`app.routes.ts:469`), so an external user who no longer matched the removed route would fall past
  the shell to the `**` 404 instead of reaching any booking form.
- returns a `UrlTree`, not a path string, so `?type=1/2` (new vs RE-EVALUATION) is carried across
  explicitly. Extracted out of the route array purely so that is testable -- an inline arrow in
  `APP_ROUTES` cannot be reached from a spec, and a dropped `?type=2` would quietly turn every
  re-evaluation link into a new booking.
- NOT done, deliberately: `AppointmentAddComponent` the CLASS stays, because
  `AppointmentWizardComponent extends` it (`appointment-wizard.component.ts:122`), so its
  `@Component` still compiles `appointment-add.component.html`. Deleting that template means
  de-componentising the base class -- a separate refactor, not smuggled into this phase.
- MEASURED BENEFIT: the legacy component was imported EAGERLY
  (`loadComponent: () => Promise.resolve(...)`), putting a 3763-line component and its template in
  the INITIAL bundle. Removing it took initial from 2.32 MB to 2.15 MB (over-budget warning
  322.62 kB -> 146.51 kB). The pre-existing budget warning is now less than half what it was.
- approach: code (routing config) + a unit spec for the redirect
- acceptance (EARS):
  - WHEN a pure-external user opens `/appointments/add`, THE SYSTEM SHALL redirect to
    `/appointments/request` rather than render the legacy form or a 404.
  - WHEN that URL carries `?type=2`, THE SYSTEM SHALL preserve it through the redirect.
  - WHEN an internal user opens `/appointments/add`, THE SYSTEM SHALL still render the in-shell
    wizard, unchanged.

### Task 5 - live re-verification of BOTH booking surfaces (HARD GATE)

PREMISE CORRECTED (2026-08-03): there is no longer a "legacy page vs wizard" split. After task 6 both
surfaces are the WIZARD, differing only in chrome and guard: internal in-shell at
`/appointments/add`, external chrome-less at `/appointments/request`. The gate is therefore one
booking as internal staff and one as an external user, NOT two different components.

- what: MODIFY nothing. Restart the angular container, then drive BOTH surfaces: the legacy add page
  and the wizard. For each, confirm the calendar loads, that dates inside lead time are disabled,
  that a pick populates the form, and that a booking still submits end to end.
- pattern: the phase 3 live recipe -- office Demo Clinic South, and note lead time 3 means near-term
  dates are legitimately disabled.
- approach: test-after
- acceptance (EARS):
  - WHEN a booking is submitted from the legacy add page after this change, THE SYSTEM SHALL create
    the appointment with the selected date and slot.
  - WHEN a booking is submitted from the wizard after this change, THE SYSTEM SHALL do the same.
  - If either surface fails, then the phase SHALL NOT be shipped.

## What the live gate caught (2026-08-03) -- SIX defects, all invisible to 452 green specs

This is the entry that justifies the gate. After tasks 1-4 the suite was fully green, the build was
clean, and THE BOOKING FORM WAS BROKEN. Every one of these was found by driving the real app.

1. `checkForAppointmentTypeSelected` was assigned INSIDE `loadAvailableDatesBySelection()`. When the
   parent stopped calling that (because the child now fetches), the flag was never set, so the
   date/time UI never unhid at all. FIXED by deriving it from the form -- a stored flag that only one
   removed method maintained was the whole problem, so removing the possibility beats re-wiring it.
2. `OnPush` + async mutation with no `markForCheck()`: "Loading available dates..." stuck forever.
   The flag DID clear; the view never re-rendered. FIXED with `ChangeDetectorRef.markForCheck()` on
   every off-template state change, plus resetting `isLoading` on the early-return path.
3. The "no bookable dates" message fired FALSELY -- it told the booker no dates existed while 48 were
   published, because the parent computed it from `availableDateKeys` / `isAvailableDatesLoading`,
   which the parent no longer populates. FIXED by deriving it in the child, which owns availability.
4. `ngbDatepicker` did NOT emit an `NgbDateStruct` on selection -- the value arrived as a formatted
   string. Assuming the struct threw
   "Cannot read properties of undefined (reading 'toString')" inside `onDatePicked`, so the time
   options never populated. FIXED by normalising both shapes. (Defect 6 later explained WHY: the
   emitted value is whatever the ambient `NgbDateAdapter.toModel` produces, and that adapter is
   string-based here.)
5. Clicking a date WEDGED THE BROWSER -- hard enough that even a synchronous `browser_evaluate` hung
   for 1800s, which initially looked like a tooling fault rather than an app fault. Cause:
   `[ngModel]` was bound to a GETTER that built a fresh object on every call, so every
   change-detection pass saw a new reference, wrote it back, and scheduled another pass. FIXED by not
   deriving the bound value on the fly. Lesson: never bind `ngModel` (or any two-way binding) to an
   expression that allocates -- reference identity IS the change signal.
6. THE PICKED DATE DISPLAYED AS AN EMPTY INPUT, while everything else was correct (the clear button
   and lead-time note appeared, 13 real time slots loaded, lead time disabled 73 of 77 day cells).
   Three successive mechanism changes did not fix it -- a memoised getter, then a plain field, then a
   component-owned `FormControl` -- because THE MECHANISM WAS NEVER THE FAULT. `ngbDatepicker` runs
   every control value through the ambient `NgbDateAdapter` inside `writeValue`, and the ambient one
   here is ABP's `DateAdapter`, an `NgbDateAdapter<string>` expecting `YYYY-MM-DD`. All three attempts
   fed an `NgbDateStruct`, so `fromModel` returned null (`new Date({year,month,day})` is an Invalid
   Date), the parser-formatter was handed null, and it wrote ''.

   FIXED in two parts. The control now holds the `YYYY-MM-DD` key it already receives (converting
   nothing), and the adapter is PINNED on the component via a local `AvailabilityDateAdapter`. The
   pin is the part that matters beyond this bug: `ngbDatepicker` resolves `NgbDateAdapter` from the
   HOST injector, so the required model shape silently differed per host -- both booking surfaces
   provide ABP's, while the reschedule modal (4b) and this component's own spec fall back to
   ng-bootstrap's struct-based default. Left unpinned, the same control value renders correctly in one
   host and blank in another, which would have re-introduced this bug in 4b. Pinning makes the model
   shape part of the component's own contract. A local adapter rather than ABP's keeps the component
   free of the ABP DI barrel (matching its no-ABP design note) and is itself unit-tested.

Pattern across all six: THREE of them are state that used to be maintained by the method that moved.
When extracting behaviour out of a large component, grep for everything the moved method ASSIGNED,
not just what it read -- the assignments are the silent breakages.

The other transferable lesson, from 5 and 6: EACH WAS INTRODUCED BY THE FIX TO THE PREVIOUS ONE. Three
attempts at defect 6 all changed the binding MECHANISM while leaving the model SHAPE wrong. When the
second attempt at the same symptom fails, stop tuning the mechanism and go read what the third-party
directive actually does with the value -- here, ten minutes in `NgbDateAdapter` would have replaced
three rounds of guessing.

Why no spec caught 6, and what now does: the specs asserted component state, the emitted output, the
disabled days and the availability highlight -- every one of which was CORRECT throughout. Nothing
asserted the input's DISPLAYED TEXT, the single observable that was wrong. There is now a spec that
does (`DISPLAYS the selected date in the input`), and it was FALSIFIED before being trusted: restoring
the struct shape makes it fail with `Expected '' to be '08/13/2026'`, which is defect 6 reproduced in
a unit test. `AvailabilityDateAdapter` also has its own round-trip spec.

TOOLING TRAP, cost ~30 minutes: do NOT put `async` / `await sleep(...)` inside Playwright's
`browser_evaluate`. It deadlocked the MCP server for 1800s before aborting. Click and read must be
separate calls. Note the second 1800s hang was NOT the tool -- it was defect 5 wedging the page, so
treat a repeat hang as a possible app fault, not just a tooling one.

## Task 5 gate: PASSED (2026-08-04), both surfaces, with database proof

Driven through the CORRECT doors, which is itself the finding below:

| Conf#  | Surface                       | Date + time         | Location          | Type | Slot FromTime |
| ------ | ----------------------------- | ------------------- | ----------------- | ---- | ------------- |
| A00036 | internal, Intake Staff, shell | 2026-08-13 09:00:00 | Demo Clinic South | IME  | 09:00:00      |
| A00037 | external, Patient, chromeless | 2026-08-08 09:30:00 | Demo Clinic North | AME  | 09:30:00      |

Both rows carry a resolved `DoctorAvailabilityId` whose `FromTime` matches the chosen time, so the
component's three-value output (date, time, slot id) reaches the form and the API intact.

Also observed live, i.e. the defects above are genuinely fixed and not merely unit-green: the date
DISPLAYS (`08/13/2026`, `08/20/2026`, `08/08/2026`), the label/input association is present, the time
list fills, no stuck "Loading available dates", no false no-dates warning, and lead time disables
correctly (on 2026-08-04 the 6th had fallen inside the 3-day window; 13/20/27 stayed open, and Demo
Clinic North's 08-04 slots were disabled while 08-08/11/14/18/20/25 + Sep 1/8/15/22/29 matched the DB
exactly -- which also proves the location-change reload and its race guard).

Console errors during the run were all Smarty address-validation 401s (an external dev key),
unrelated to this phase.

### THE ENVIRONMENT LESSON, worth more than the gate itself

The FIRST attempt at this gate tested the wrong application entirely. Internal staff are HOST users
(`AbpUsers.TenantId IS NULL`: Intake Staff, IT Admin, Staff Supervisor) who sign in at
`admin.<base>`, land on `/host/my-offices`, and "Enter practice" IMPERSONATES them into a tenant
(the access token carries `impersonator_userid`). EXTERNAL users (Patient, Applicant/Defense
Attorney, Claim Examiner) live in the TENANT databases and sign in at `{tenant}.<base>`.

`admin@falkinstein.test` is a TENANT admin, so testing internal booking as that user on
`falkinstein.localhost` exercised neither real path. Verify through the door the actual role uses, or
the verification is theatre -- this is exactly how the `/appointments/add` 404 below reached Adrian
instead of being caught here.

## Validation loop

Frontend-only diff, so no `dotnet` commands are required by `~/.claude/rules/testing.md` -- but the
whole point of task 5 is that these commands are NOT sufficient on their own.

```bash
cd /c/src/patient-portal/main/angular && npx prettier --check "src/app/appointments/**/*.{ts,html,scss}" && npx eslint src/app/appointments
```

```bash
cd /c/src/patient-portal/main/angular && npx ng build
```

```bash
cd /c/src/patient-portal/main/angular && export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe" && npx ng test --watch=false --browsers=ChromeHeadless
```

```bash
cd /c/src/patient-portal/main && docker restart main-angular-1
```

Then task 5's manual gate on both surfaces. Do not ship on green automated checks alone.

## Risk / rollback

Blast radius: the schedule step of the LIVE booking flow, on two surfaces. This is the riskiest
phase so far precisely because booking is the product's core path and the failure mode is silent --
phase 3 shipped a calendar that type-checked, passed 432 specs, and rendered no colour.

Mitigations actually in the plan: rules move verbatim (task 1 changes no behaviour); the form
contract is preserved so nothing downstream of the form changes (task 3); the wizard's TypeScript is
untouched; and task 5 gates the phase on both surfaces booking successfully.

Rollback: revert the commits. No server change, no migration, no dependency change, so revert is
clean and complete.
