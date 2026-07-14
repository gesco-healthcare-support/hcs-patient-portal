---
status: in-progress
date: 2026-06-15
slug: internal-scheduling
branch: feat/redesign-internal-scheduling
parent-branch: feat/internal-user-pages
surface: internal Scheduling -- doctor availabilities (week grid + table), generate slots, locations CRUD, WCAB offices CRUD
backend: YES -- one contained add (item 13 SelectedDates on the generate input) -- Task B1
prompt: Prompt 14 (design_handoff_appointment_portal/PROMPTS.md row 14)
decision: "Adrian 2026-06-15: D1 = (A) FULL SCOPE -- build both generate patterns incl. the SelectedDates backend add (B1). Execution = build straight-through, then verify (Playwright + karma) before the squash-merge."
---

# Plan: Internal Scheduling redesign (Prompt 14)

Re-skin the four internal Scheduling surfaces into the internal shell using the
Prompt 9-13 redesign pattern (standalone + OnPush + signals + shared `ia-*` / `af-btn`
/ `ra-*` SCSS), reusing the existing backend engines and proxies. Leave clean seams for
the deferred date/time UTC + per-location-timezone plan; do NOT start that work here.

## Surfaces (design source of truth = Internal Scheduling - Redesign.html + in-sched.jsx + in-sched.css)

1. **Doctor availabilities** -- location + status filters, week navigation, Week-grid
   <-> Table toggle. Grid: 7 day columns, status-colored slots (available / booked /
   reserved), per-day capacity bar, per-slot delete (x), per-day delete (confirm modal).
   Table: collapsible day rows with status counts, expand to per-slot rows, per-slot +
   per-day delete.
2. **Generate slots** -- form (location, schedule-pattern toggle, multi time-range with
   per-range duration override, capacity, default duration, appointment types) ->
   "Generate preview" -> conflict-highlighted slot grid -> "Submit N slots" (disabled
   while any conflict remains or zero slots). Conflict gate: red slots must be removed
   first.
3. **Locations** -- table (name, address, state, parking fee, types, status) + modal
   CRUD. Form: name, address, city, state, zip, parking fee, appointment types (multi),
   active toggle.
4. **WCAB offices** -- table (name, code) + modal CRUD (name, code).

## Research grounding (verified against the code, 2026-06-15)

**Availabilities backend (reuse as-is):** `IDoctorAvailabilitiesAppService` ->
`GeneratePreviewAsync(DoctorAvailabilityGenerateInputDto)` returns grouped
`DoctorAvailabilitySlotsPreviewDto` (per-date, with `IsConflict` per slot +
`SameTimeValidation` per date); `CreateRangeAsync` re-runs preview server-side, inserts
non-conflicted slots, returns `{ InsertedCount, SkippedConflictCount, ConflictedSlots }`;
`DeleteBySlotAsync(location+date+from/to)`; `DeleteByDateAsync` partial (skips
Booked/Reserved, returns `{ DeletedCount, SkippedSlotIds }`); single `DeleteAsync(id)` is
FK-protected. 5000-slot cap enforced before expansion. Generate input today:
`FromDate, ToDate, SelectedDays(int[] 0-6, null=all), TimeRanges[(FromTime, ToTime,
AppointmentDurationMinutes?)], AppointmentTypeIds[], Capacity(=3), AppointmentDurationMinutes(=15),
BookingStatusId, LocationId`.

**Availabilities UI today:** flat grouped table (group key locationId+date), expand to
slots, per-day + per-slot delete; ABP ListService pagination by group. Routes:
`/doctor-management/doctor-availabilities` ('' list), `/generate` and `/add` (both load
`DoctorAvailabilityGenerateComponent`). No week-grid exists yet.

**Locations backend (reuse as-is):** `ILocationsAppService` standard CRUD;
`LocationManager.EnsureCanDeleteAsync` blocks delete when referenced by Appointment or
DoctorAvailability (LocationInUse). Fields: `Name*`(50), `Address`(100), `City`(50),
`ZipCode`(regex,15), `ParkingFee*`(decimal>=0), `IsActive`, `StateId?`, M2M
`AppointmentTypeIds`. `UpdateDto` carries `ConcurrencyStamp`. NO `TimeZoneId` today.

**WCAB backend (reuse as-is):** `IWcabOfficesAppService` standard CRUD. Fields:
`Name*`(50), `Abbreviation*`(50), `Address`(100), `City`(50), `ZipCode`(15), `StateId?`,
`IsActive`(=true). `UpdateDto` carries `ConcurrencyStamp`.

**Routing/shell/SCSS:** three routes already nest under `/doctor-management` in
`INTERNAL_SHELL_CHILDREN` (app.routes.ts ~lines 113-132); `internal-nav.config.ts`
already has a `sect: 'Scheduling'` group (supervisor-gated). Global SCSS partials live in
`angular/src/styles/` and register via `@use` in `styles.scss`. Existing scheduling
components are legacy ABP CRUD (not standalone, Default CD); Prompt 13 redesigns are
standalone + OnPush + signals + `*-util.ts` + spec.

## Decisions (ADR-style)

### D1 -- generate-pattern scope (NEEDS ADRIAN; the only backend question)
The design's Generate page offers TWO patterns: "Date range + weekdays" (backend
supports today) and "Pick days on calendar" (irregular dates). The second requires
`SelectedDates: DateOnly[]` on the generate input -- BACKEND-CHANGES item 13, NOT yet
built. Options:
- **(A) Full scope (recommended):** add a contained backend extension -- `SelectedDates:
  List<DateOnly>?` on `DoctorAvailabilityGenerateInputDto`, mutually exclusive with
  `FromDate/ToDate + SelectedDays`; expand it in `GeneratePreviewAsync` / `CreateRangeAsync`
  (same conflict + 5000-cap logic); regenerate the Angular proxy. Build both patterns.
  Rationale: item 13 is small and fully specified; PROMPTS.md row 14 + the design
  explicitly require "both patterns + conflict gate"; deferring ships a visibly
  half-built toggle.
- **(B) Defer:** build only "Date range + weekdays" now; hide/disable the calendar-pick
  toggle with a "coming soon" note; ship item 13 later. Keeps this page frontend-only
  (matches the original "no backend rework" framing) but partially unmet design.
This plan is written for (A); if Adrian picks (B), drop Task B1 and gate the calendar
pattern in Task T3.

### D2 -- build approach (decided; latitude was granted)
Per-surface hybrid, reusing existing engines/proxies/services (no backend rework beyond
D1):
- **Availabilities + Generate:** NEW standalone OnPush components (the week grid, calendar
  picker, and conflict-preview grid are a large visual departure that does not map onto
  the legacy ngx-datatable). Reuse the proxy (`generatePreview`, `createRange`,
  `deleteByDate`, `deleteBySlot`) and the grouping math via a new pure util.
- **Locations + WCAB:** NEW standalone components for the list + `ra-modal` form, but
  REUSE the existing detail-view form services (`*-detail.abstract.service.ts`) where the
  reactive-form lifecycle + validators are already built and validated, and the proxies.
Legacy components stay in the tree (not deleted, per branch policy); routes repoint to the
new components.

### D3 -- delete protection (item 32) DEFERRED
Locations already have an in-use guard; surface its `LocationInUse` error as a friendly
toast. WCAB delete uses existing backend behavior behind a confirm modal. Full item-32
work (seeded-row protection + usage counts on list DTOs) is a separate backend task --
out of scope here, flagged.

### D4 -- WCAB modal minimal
Modal edits Name + Code (= `Abbreviation`) per the design. On update, preserve the other
persisted fields (address/city/zip/state/isActive) unchanged; on create they take backend
defaults. No multi-field WCAB form (design intentionally simplified).

### D5 -- date/time seams (build naive now; do NOT implement the deferred plan)
- Location form: lay out fields so a required `TimeZoneId` IANA select can drop in later
  (reserve a grid cell / ordering); do not add the field now, do not rename existing
  fields.
- Availability slot times: render the time string so a trailing clinic-TZ label
  ("9:00 AM PST") can be appended later without layout surgery (wrap the time in its own
  span). Keep all dates naive; no UTC conversion anywhere in this page.

## Tasks (ordered; approach flag per ~/.claude/rules/rpe-workflow.md)

- **T0 [code]** Create `angular/src/styles/_in-sched.scss` porting `in-sched.css`
  (av-legend / av-week / av-toggle / av-grid / av-day / av-slot / gn-days / gn-range /
  gn-types / gn-summary / gn-cal / lw-active / lw-chip) onto the global tokens; register
  with `@use 'styles/in-sched';` in `styles.scss`.

- **B1 [tdd] (CONDITIONAL on D1=A)** Backend: add `SelectedDates: List<DateOnly>?` to
  `DoctorAvailabilityGenerateInputDto`; validate mutual exclusivity with
  `FromDate/ToDate + SelectedDays`; branch expansion in `GeneratePreviewAsync` /
  `CreateRangeAsync` to iterate the explicit dates (reuse the existing per-date
  time-range expansion, conflict detection, and 5000-cap). Unit-test the expansion +
  validation. Regenerate the Angular proxy (`abp generate-proxy`) and restart `api`.

- **T1 [tdd]** `avail-grid.util.ts` (+ spec): group flat slots into the 7 week-day
  columns for a given week offset; map slot status (8/9/10) -> 'available'|'booked'|
  'reserved' class; compute per-day capacity totals (booked/total). Pure functions only.

- **T2 [test-after]** `InternalAvailabilitiesComponent` (standalone, OnPush, signals):
  location + status filters, week nav, Week-grid <-> Table toggle, capacity bars, legend,
  per-slot delete (`deleteBySlot`) + per-day delete (`deleteByDate`) with confirm modal
  (`ra-scrim`/`ra-modal`), partial-delete feedback (`SkippedSlotIds` -> toast). Reuse
  proxy + T1 util.

- **T3 [test-after]** `InternalGenerateSlotsComponent` (standalone, OnPush): the form
  (location, pattern toggle, multi time-range, capacity, duration, types), `genPreview`
  via `generatePreview`, conflict-highlighted grid with per-slot remove, submit via
  `createRange` showing `InsertedCount`/`SkippedConflictCount`. Client-side 5000-cap warn.
  `gen-slots.util.ts` (+ spec) [tdd] for: build input DTO from form, estimate slot count,
  count conflicts, map preview -> grid. If D1=B, gate the calendar pattern.

- **T4 [test-after]** `InternalLocationsComponent` (standalone): `ia-table` list +
  `ra-modal` CRUD form reusing the location detail form service + proxy; multi-select
  appointment types; in-use delete-guard error surfaced as toast; D5 timezone seam.

- **T5 [test-after]** `InternalWcabOfficesComponent` (standalone): `ia-table` list +
  `ra-modal` CRUD (Name + Code) reusing the WCAB detail form service + proxy; preserve
  other fields on update (D4).

- **T6 [code]** Repoint the three route arrays
  (`doctor-availability-routes.ts`, `location-routes.ts`, `wcab-office-routes.ts`) to the
  new components (keep `/generate` + `/add` -> generate component). Verify the existing
  Scheduling nav group labels/icons/routes still match.

- **T7 [verify]** Live verification (below) + karma for the new util + component specs.

## Verification

- Build: `ng build` clean; `dotnet build` clean (if B1).
- Karma (headless, Windows): `avail-grid.util.spec`, `gen-slots.util.spec`, component specs.
- Live (Playwright, http://falkinstein.localhost:4250, stafsuper1@gesco.com): availabilities
  Week<->Table toggle; generate both patterns (if D1=A) -> conflict gate blocks overlap;
  per-slot + per-day delete (incl. partial-delete feedback); location create/edit/delete
  (in-use guard); WCAB CRUD; no console errors; backend untouched beyond B1. Restart
  `angular` after edits.

## Risks / gotchas (from research)

- Angular location proxy models still reference `appointmentTypeId` (singular) vs the
  backend `appointmentTypeIds` (array); the existing detail service maps around it -- reuse
  that mapping, do not "fix" the proxy here.
- `deleteByDate` is partial (booked/reserved kept) -- the UI must show "N deleted, K kept".
- 5000-slot cap is server-enforced before expansion -- warn client-side to avoid a hard
  `UserFriendlyException`.
- `CreateRangeAsync` ignores the client preview and re-runs server-side -- after submit,
  trust the returned counts, refresh from the server.
- Internal shell is Default CD on purpose -- new components are OnPush, but do NOT change
  the shell.

## Out of scope

- The date/time UTC + per-location-timezone retrofit
  (docs/plans/2026-06-15-global-datetime-resolver.md) -- only leave seams (D5).
- Item-32 full delete-protection + usage counts (D3).
- Multi-field WCAB form (D4).
