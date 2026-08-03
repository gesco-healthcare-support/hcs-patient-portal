---
feature: Google-Calendar-style staff schedule with clickable appointment chips
date: 2026-07-31
status: in-progress
base-branch: main
related-issues: []
---

# Phase 3: staff schedule calendar

Phase 3 of `2026-07-31-reschedule-cancel-calendar-integration-epic.md`. Independent of phases 1,
2 and the 4x chain.

## Goal

Staff open a Schedule screen and see a week or day calendar where every slot shows booked /
requested / free against its real capacity, each appointment appears as a chip at its actual date
and time carrying the patient name and confirmation number, and clicking a chip opens that
appointment.

## Context & decisions

Why now: no staff calendar exists. The closest screen, `InternalAvailabilitiesComponent` at
`/doctor-management/doctor-availabilities`, is a slot week-GRID whose patient names are plain
non-clickable `<span>`s, and whose booked/free colouring is driven by `DoctorAvailability.BookingStatusId`.

VERIFIED root problem (re-checked against `main` at `baa1fee6`, because the earlier epic-wide
research got this wrong): occupancy IS already computed -- `DoctorAvailabilityDto.RemainingCapacity`
(`DoctorAvailabilityDto.cs:38`, `int?`) is filled from the batch
`IAppointmentRepository.GetActiveCountsForSlotsAsync` -- but ONLY inside the booking-picker path
`GetDoctorAvailabilityLookupAsync`, which filters `BookingStatusId == BookingStatus.Available` and
then drops full slots (`RemainingCapacity > 0`). Those are exactly the slots staff need to SEE.
Confirmed the consequence: the only `RemainingCapacity` assignment in the service is line 612, and
`GetListAsync` (the grid's source, returning `DoctorAvailabilityWithNavigationPropertiesDto`) never
assigns it. Separately, `BookingStatus.Booked` is assigned by NO code -- the only reference is a read
in a predicate (`DoctorAvailabilitiesAppService.cs:650`) -- so the grid's "Booked" colour is
reachable only by a manual admin edit and is not ground truth.

Resolved decisions (Adrian, 2026-07-31):

- Decision: NEW screen, keep the existing grid, because the grid is also the slot-MANAGEMENT tool
  (generation + manual close); replacing it would drag slot administration into this phase.
- Decision: ONE new purpose-built read endpoint rather than extending the two existing ones,
  because the calendar needs slots + occupancy + appointments in a single round-trip, and a
  dedicated DTO keeps the booking picker's shared contract untouched.
- Decision: the endpoint is VIEW-AGNOSTIC (no FullCalendar vocabulary in the DTO, status sent as
  the enum, no pre-computed colours or labels), because Adrian may replace FullCalendar with a
  hand-built calendar -- that swap must stay frontend-only.
- Decision: week + day views only (`timeGridWeek`, `timeGridDay`), both free MIT plugins. No month
  view: with `Capacity` defaulting to 3, a month cell cannot show named chips legibly.
- Decision: a location must be selected (default to one clinic, not All), because the chips carry
  patient names and the amount of PHI on screen should stay proportionate to the task.
- Decision: reuse the existing `CaseEvaluation.DoctorAvailabilities` permission, which already
  gates both the grid and the patient-names endpoint -- no new permission.
- Decision: FullCalendar MIT only (`@fullcalendar/angular` 7.0.2, MIT, peer `@angular/core` 16-22
  against this project's `~20.3.19`). NOT the paid resource/timeline views. No licence key, which
  also matters because this repo is public.
- Decision: the calendar derives booked/free from REAL occupancy and ignores `BookingStatusId`
  entirely, so it cannot inherit the never-set-`Booked` bug.

Non-goals: slot generation/editing, month view, changing the booking picker, and any external-facing
surface (patient names stay internal-only).

## All needed context

- Slot entity `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs`:
  `AvailableDate` (`DateTime`, :18), `FromTime`/`ToTime` (`TimeOnly`, :20/:22), `BookingStatusId`
  (:24), `LocationId` (:26), `Capacity` (`int`, default 3, :36).
- Occupancy source: `IAppointmentRepository.GetActiveCountsForSlotsAsync(slotIds)` -- batch,
  already used at `DoctorAvailabilitiesAppService.cs:605`; returns a dictionary, missing key = 0.
  Mirror that usage exactly (it exists to avoid N+1).
- App service to extend: `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs`
  and its interface in `Application.Contracts/DoctorAvailabilities/`. NOTE: the controller
  `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs`
  IMPLEMENTS the interface (`[RemoteService] [Area("app")] [ControllerName("DoctorAvailability")]
[Route("api/app/doctor-availabilities")]`), so a new interface member MUST also be added there or
  the build breaks.
- Permission pattern to mirror: `[Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]`
  on `GetSlotPatientNamesAsync` (`DoctorAvailabilitiesAppService.cs:627-628`), whose doc comment
  already states patient names are internal-only.
- Appointment fields for the chips: `Appointment.Id`, `RequestConfirmationNumber`,
  `AppointmentStatus`, and the patient name via the existing navigation-property mapping used by
  `AppointmentWithNavigationPropertiesDto`.
- Statuses that mean "requested" vs "booked": `Pending`, `RescheduleRequested`,
  `CancellationRequested`, `InfoRequested` are pending-ish; `Approved` is booked. Terminal
  (`CancelledNoBill`, `CancelledLate`, `Rejected`) must NOT occupy a slot -- occupancy already
  counts only non-terminal appointments, so reuse that definition rather than inventing one.
- Angular: standalone components, `OnPush`. Existing grid to mirror for filters/date-range state:
  `angular/src/app/doctor-availabilities/doctor-availability/internal-availabilities.component.ts`
  (`availableDateMin`/`Max` + `locationId`, `:117-133`).
- Nav: add to the `Scheduling` group beside `availabilities`
  (`internal-nav.config.ts`, the entry with `route: '/doctor-management/doctor-availabilities'`,
  `roles: ['supervisor','intake']`, `requiredPolicy: 'CaseEvaluation.DoctorAvailabilities'`).
- Chip target route: `/appointments/view/:id` (`appointment-routes.ts`, `path: 'view/:id'`).
- Proxy regeneration target: `http://localhost:44327` (see `angular/src/environments/environment.docker.ts:27`).
- Yarn 4.16.0 (`packageManager`), so use `yarn add`, not npm.
- GOTCHA from phases 1-2, applies here: `ng lint`/`yarn lint` is broken locally (angular-eslint 20
  vs ESLint 8) -- use `npx eslint <files>`; run `npx prettier --check` before committing; the
  dev containers build bind-mounted source at container START so restart `angular` before any live
  check; re-check `git rev-parse --abbrev-ref HEAD` immediately before each commit.

## Tasks

### Task 1 - pure projection from slots + appointments to a schedule

- what: CREATE `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/ScheduleProjection.cs`
  with a static `ScheduleProjection.Build(IReadOnlyCollection<DoctorAvailability> slots,
IReadOnlyCollection<ScheduleAppointmentDto> appointments, IReadOnlyDictionary<Guid, long> activeCounts)`
  returning `List<ScheduleSlotDto>`: group appointments by `DoctorAvailabilityId`, set
  `ActiveCount` from the dictionary (missing = 0) and `RemainingCapacity = Max(0, Capacity - ActiveCount)`,
  and order by date then `FromTime`.
- pattern: the same computation inline at `DoctorAvailabilitiesAppService.cs:601-614`, extracted so
  it is testable without the ABP host; pure-helper style mirrors `BillingStatusWire` /
  `admin-hub.util.ts`.
- approach: tdd (capacity arithmetic drives what staff believe is bookable)
- acceptance (EARS):
  - WHEN a slot has no appointments, THE SYSTEM SHALL report `ActiveCount` 0 and
    `RemainingCapacity` equal to `Capacity`.
  - WHEN a slot's active appointments equal its capacity, THE SYSTEM SHALL report
    `RemainingCapacity` 0 AND still include the slot in the result.
  - THE SYSTEM SHALL never report a negative `RemainingCapacity`, even if active exceeds capacity.
  - THE SYSTEM SHALL order slots by `AvailableDate` then `FromTime`.

### Task 2 - the schedule read endpoint

- what: CREATE `Application.Contracts/DoctorAvailabilities/GetScheduleInput.cs`
  (`Guid LocationId` REQUIRED, `DateTime FromDate`, `DateTime ToDate`),
  `ScheduleSlotDto.cs` (`SlotId`, `AvailableDate`, `FromTime`, `ToTime`, `Capacity`, `ActiveCount`,
  `RemainingCapacity`, `List<ScheduleAppointmentDto> Appointments`) and `ScheduleAppointmentDto.cs`
  (`AppointmentId`, `RequestConfirmationNumber`, `PatientName`, `AppointmentStatusType Status`).
  ADD `GetScheduleAsync(GetScheduleInput)` to `IDoctorAvailabilitiesAppService`, implement it on
  `DoctorAvailabilitiesAppService` (query slots by `LocationId` + date range with NO
  `BookingStatusId` filter and NO full-slot exclusion, load that range's non-terminal appointments,
  batch `GetActiveCountsForSlotsAsync`, hand to `ScheduleProjection.Build`), gate it with
  `[Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]`, and add the matching member
  to `DoctorAvailabilityController`.
- pattern: `GetSlotPatientNamesAsync` for the authorize attribute + internal-only doc comment;
  `GetDoctorAvailabilityLookupAsync` for the query + batch-count shape (but WITHOUT its two filters).
- approach: tdd (an authorization boundary carrying PHI)
- acceptance (EARS):
  - WHEN a caller without `CaseEvaluation.DoctorAvailabilities` calls the endpoint, THE SYSTEM SHALL
    refuse the request.
  - WHEN a date range is requested, THE SYSTEM SHALL return every slot in it for the given location
    INCLUDING slots that are full and slots whose `BookingStatusId` is not `Available`.
  - THE SYSTEM SHALL exclude terminal appointments (cancelled / rejected) from a slot's
    `Appointments` and from `ActiveCount`.
  - THE SYSTEM SHALL require `LocationId`; a request without one SHALL be rejected rather than
    returning every clinic.
  - THE SYSTEM SHALL NOT contain any FullCalendar-specific field, colour or label.

### Task 3 - add the FullCalendar MIT packages

- what: MODIFY `angular/package.json` via
  `yarn add @fullcalendar/angular fullcalendar temporal-polyfill`. Commit the lockfile.

  CORRECTED DURING BUILD (2026-07-31): the plan first named the v6-style package set
  (`@fullcalendar/core` + `@fullcalendar/timegrid`). That is WRONG for v7 and yarn proved it --
  `@fullcalendar/timegrid` has no v7 release so it resolved to `6.1.21` against a `7.0.2` core, and
  `yarn explain peer-requirements` flagged unmet peers for `fullcalendar` and `temporal-polyfill`.
  In v7 the Angular package peers on the `fullcalendar` BUNDLE (which carries dayGrid/timeGrid/list)
  rather than on separate plugin packages. Installed set, all peers met, all MIT:
  `@fullcalendar/angular` 7.0.2, `fullcalendar` 7.0.2, `temporal-polyfill` 1.0.2. Verified
  `node_modules/@fullcalendar/` holds only `angular` + `core` -- no `resource-*`/timeline package
  was pulled in, so nothing commercial is present. Import plugins FROM the bundle, not from
  `@fullcalendar/timegrid`.

- pattern: n/a (dependency addition). Do NOT add any `@fullcalendar/resource-*` or timeline package
  -- those are the commercial ones.
- approach: code
- acceptance (EARS):
  - THE SYSTEM SHALL depend only on FullCalendar packages whose npm `license` field is `MIT`.
  - THE SYSTEM SHALL NOT contain a FullCalendar licence key in source or config.

### Task 4 - pure mapping from the DTO to calendar events

- what: CREATE `angular/src/app/doctor-availabilities/schedule/schedule-calendar.util.ts` exporting
  `toBackgroundEvents(slots)` (one `display: 'background'` event per slot, classed by whether it is
  full / partly booked / free) and `toAppointmentEvents(slots)` (one timed event per appointment,
  `id` = appointment id, title composed from confirmation number + patient name, classed by
  requested-vs-booked), plus `isRequestedStatus(status)`.
- pattern: `angular/src/app/admin/admin-hub.util.ts` -- pure exported functions unit-tested without
  DI, which is how this codebase keeps components thin. Closer still: `avail-grid.util.ts` in this
  same area, whose `.slice(0, 10)` date-portion guard this util reuses.
- approach: tdd (the classification decides what staff see as free; a wrong answer causes a
  double-booking)

  BLOCKER FOUND + RESOLVED DURING BUILD (2026-08-03): merely importing a TYPE from `fullcalendar`
  failed the build -- FullCalendar v7's transitive `@full-ui/headless-calendar` references
  `Intl.DateTimeRangeFormatPart`, which is declared in `lib.es2021.intl.d.ts`, but
  `angular/tsconfig.json` sets `"lib": ["es2018", "dom"]` while `"target"` is ALREADY `ES2022` --
  an inherited inconsistency, untouched since the initial commit. `skipLibCheck` is unset (so
  `false`) repo-wide, which is why a dependency's `.d.ts` can break the build at all.
  Decision (Adrian, 2026-08-03): widen to `"lib": ["es2022", "dom"]` rather than set
  `skipLibCheck: true`, because it fixes the actual inconsistency and KEEPS dependency `.d.ts`
  type-checking (Angular's own v20 template sets both; we deliberately take only the lib half).
  Verified before deciding: full `npx ng build` passes and the initial bundle stays at its
  pre-existing 2.32 MB. Rejected alternatives: `skipLibCheck: true` alone (hides future dependency
  type regressions), both, and dropping FullCalendar.

- acceptance (EARS):
  - WHEN a slot has `RemainingCapacity` 0, THE SYSTEM SHALL class its background event as full.
  - WHEN an appointment's status is `Pending`, `RescheduleRequested`, `CancellationRequested` or
    `InfoRequested`, THE SYSTEM SHALL class its chip as requested, and as booked for `Approved`.
  - THE SYSTEM SHALL set each appointment event's `id` to the appointment id so a click can route
    to it.
  - THE SYSTEM SHALL produce chip titles containing both the confirmation number and the patient
    name.

### Task 5 - the Schedule screen

- what: CREATE `angular/src/app/doctor-availabilities/schedule/internal-schedule.component.ts`
  (+ `.html`, `.scss`): standalone, `OnPush`, `FullCalendarModule` with `timeGridPlugin`, default
  view `timeGridWeek` and a day toggle, a REQUIRED location selector defaulting to one clinic, and
  `eventClick` routing to `/appointments/view/<id>` for appointment events only (background events
  are not clickable). Fetch via the generated proxy for `GetScheduleAsync` on view/date/location
  change. REGENERATE the Angular proxy so the service exists.
- pattern: `internal-availabilities.component.ts` for the location + date-range filter state and
  its loading/empty handling.
- approach: test-after (UI wiring; the logic it depends on is unit-tested in tasks 1 and 4)
- acceptance (EARS):
  - WHEN the Schedule screen opens, THE SYSTEM SHALL show the current week for the selected
    location with slots rendered at their real times.
  - WHEN a staff member clicks an appointment chip, THE SYSTEM SHALL navigate to that
    appointment's detail page.
  - WHEN the visible date range or location changes, THE SYSTEM SHALL reload the schedule for the
    new range.
  - THE SYSTEM SHALL NOT render patient names for a location the caller has not selected.

### Task 6 - route + sidebar entry

- what: MODIFY `angular/src/app/app.routes.ts` to add `doctor-management/schedule` (guards
  `[authGuard, permissionGuard]`, `data: { requiredPolicy: 'CaseEvaluation.DoctorAvailabilities' }`)
  and `internal-nav.config.ts` to add a `Scheduling` item `id: 'schedule'`, `label: 'Schedule'`,
  `route: '/doctor-management/schedule'`, `roles: ['supervisor','intake']`, same `requiredPolicy`.
- pattern: the sibling `availabilities` nav item and the `/admin/integration-failures` route added
  in phase 1 (guard + `requiredPolicy` matching the nav gate so visibility equals access).
- approach: test-after
- acceptance (EARS):
  - WHERE a caller holds `CaseEvaluation.DoctorAvailabilities`, THE SYSTEM SHALL show a "Schedule"
    sidebar item under Scheduling.
  - WHERE a caller lacks it, THE SYSTEM SHALL neither render the item nor allow the route.

### Task 7 - make the existing grid's occupancy honest (SEPARABLE)

Included because the epic roadmap assigned the never-set-`Booked` bug to this phase. It is
independent of tasks 1-6 and can be dropped without affecting them.

- what: MODIFY `DoctorAvailabilitiesAppService.GetListAsync` to populate `RemainingCapacity` on each
  returned DTO from the same batch `GetActiveCountsForSlotsAsync`, and MODIFY
  `internal-availabilities.component.html` to colour a slot from real occupancy instead of
  `BookingStatusId`, keeping `Reserved` as the manual-close indicator it actually is.
- pattern: the count+assign block at `DoctorAvailabilitiesAppService.cs:601-614`.
- approach: test-after

  DEVIATION (2026-08-03): the template needed NO change. The plan assumed
  `internal-availabilities.component.html` read `BookingStatusId` directly; it actually binds
  only `slot.statusKey`, which `avail-grid.util.buildWeekColumns` computes. So the colour fix
  is one new pure function (`slotStatusKey`) plus its call site -- the template is untouched.
  `slotStatusKey` falls back to the stored status when `remainingCapacity` is null, so reads
  that do not compute occupancy are not silently reported as free; that fallback is what keeps
  the pre-existing `buildWeekColumns` specs passing unchanged.

- acceptance (EARS):
  - WHEN the availabilities grid loads, THE SYSTEM SHALL colour each slot from its active-count
    versus capacity, not from `BookingStatusId`.
  - THE SYSTEM SHALL still show a manually closed (`Reserved`) slot as unavailable.

## Validation loop

Touches backend AND Angular, so per `~/.claude/rules/testing.md` the loop needs both test commands,
not just builds.

```bash
cd /c/src/patient-portal/main && dotnet format --verify-no-changes && dotnet build -warnaserror
```

```bash
cd /c/src/patient-portal/main && dotnet test test/HealthcareSupport.CaseEvaluation.Domain.Tests test/HealthcareSupport.CaseEvaluation.Application.Tests
```

```bash
cd /c/src/patient-portal/main && dotnet test
```

```bash
cd /c/src/patient-portal/main/angular && npx prettier --check "src/app/doctor-availabilities/**/*.{ts,html,scss}" && npx eslint src/app/doctor-availabilities src/app/app.routes.ts
```

```bash
cd /c/src/patient-portal/main/angular && npx ng build
```

```bash
cd /c/src/patient-portal/main/angular && export CHROME_BIN="/c/Program Files/Google/Chrome/Application/chrome.exe" && npx ng test --watch=false --browsers=ChromeHeadless
```

Live check (needs the local stack; restart the `angular` container first so it serves this branch):
open `/doctor-management/schedule` as a seeded staff user, confirm slots render at real times with
booked/requested/free distinguishable, and click a chip through to the appointment.

DONE 2026-08-03, as `admin@falkinstein.test` on `falkinstein.localhost:4200`, Demo Clinic South,
week of 2026-07-12. Verified: nav item present; 24 slot bands + 24 chips matching the endpoint
response exactly (1 `appt-booked` = the single Approved appointment, 23 `appt-requested`); the
endpoint reloads on both location change and week navigation; chip click routed to
`/appointments/view/2e0acb63-...` and that page rendered A00005 / the right patient; anonymous
call to the endpoint returns 401.

TWO REAL DEFECTS THE AUTOMATED LOOP COULD NOT CATCH -- both found only by looking at the running
app, and both fixed:

1. FullCalendar v7 replaced v6's `classNames: string[]` with `class`/`className` as a STRING
   (`EVENT_UI_REFINERS`, `refineClassName`). Events carrying the v6 shape TYPE-CHECK -- `EventInput`
   tolerates unknown properties because of `extendedProps` -- but FullCalendar silently ignores
   them, so every slot band and chip rendered with NO occupancy colour. The unit tests passed
   throughout because they asserted the object shape we invented, not the shape the library
   consumes. Lesson: a pure mapper tested only against itself proves nothing about the contract.
2. v7 emits HASHED class names (`fc-classic-dl1`, `fc-BO`), so the v6 hooks `.fc` and
   `.fc-bg-event` match nothing. Styling now uses the `full-calendar` ELEMENT plus our own event
   classes, which are the only stable handles. Related: v7 wraps the chip title in an inner div
   that sets `color: #fff`, so the colour must be forced onto descendants (`&, *`) -- otherwise
   chips render white-on-pale. Measured after the fix: 5.53:1 (booked) and 5.36:1 (requested),
   both above the WCAG AA 4.5:1 floor.

OPEN UX OBSERVATION (not a defect, not fixed): because the plan deliberately sets no
`slotMinTime`/`slotMaxTime` -- clipping the day could hide a real slot -- the grid renders all 24
hours, so roughly a third of the screen is empty night hours. A compact fix that still hides
nothing would derive the bounds from the loaded slots' min/max times. Deferred: it needs the
options object to become reactive, which the component deliberately avoids.

## Risk / rollback

Blast radius: one new read endpoint, one new screen, one nav item, one dependency. Task 7 is the
only change to existing behaviour (the grid's colours) and is separable.

The dependency is the main new risk: FullCalendar goes through the repo's Dependency Review CI
gate, Dependabot version PRs are currently DISABLED here so updates would be manual, and it pulls a
`temporal-polyfill` peer. Bundle impact must be checked -- the initial-bundle budget is ALREADY over
at 2.32 MB against a 2.00 MB budget (pre-existing), so confirm the calendar lands in a LAZY chunk
and does not worsen the initial bundle.

PHI: this screen renders patient names, so it is internal-only, gated by
`CaseEvaluation.DoctorAvailabilities`, and requires an explicit location. Names must not be logged,
and the endpoint must not be reachable anonymously.

Rollback: revert the commits and `yarn remove` the FullCalendar packages. Nothing persists and there
is no migration, so revert is clean.
