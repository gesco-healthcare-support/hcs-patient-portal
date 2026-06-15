---
status: draft
date: 2026-06-15
slug: global-datetime-resolver
branch: feat/global-datetime-resolver
parent-branch: feat/internal-user-pages
surface: app-wide date/time -- true-UTC storage + per-location clinic timezone + browser-localized display
backend: YES (ABP Clock.Kind=Utc + bare-date opt-outs, Location.TimeZoneId, appointment UTC derivation, reseed)
timing: DEFERRED -- build as ONE fix after the UI rework (Prompts 14-17) is complete
decision: "Adrian 2026-06-15: store UTC; appointment slots in clinic TZ; instants in viewer browser TZ (always labeled); per-Location TZ field; reseed dev data (no migration); single fix (no split PRs)"
supersedes: "the earlier Option A (display-only, no-shift) design previously in this file"
---

# Plan: UTC storage + timezone-aware display

Store datetimes in UTC; show them localized + always labelled so no one guesses a
timezone and no raw DB format ever reaches the UI.

## Model (confirmed + grounded in the backend research)

| Kind | Fields | Stored | Displayed in | Label |
| --- | --- | --- | --- | --- |
| Instant | creationTime, lastModificationTime, change-log time, packet generated/regenerated, requestedOn, approveDate, decision deadline | UTC | viewer browser TZ | yes |
| Appointment moment | `Appointment.AppointmentDate` | UTC (derived at booking from slot + clinic TZ) | the appointment's clinic TZ | yes |
| Clinic availability hours | `DoctorAvailability.AvailableDate` + `FromTime`/`ToTime` | clinic-local, naive | clinic TZ (as-is) | yes |
| Bare date | Patient DOB, injury dates | naive date | not converted | no |

**Key nuance:** "store in UTC" applies to instants + the appointment moment. Clinic
**availability hours** are recurring clinic-local wall-clock definitions; storing them
as UTC instants breaks `.Date`-based conflict detection + past-date gates (UTC midnight
!= clinic midnight). So availability stays clinic-local-naive; the appointment's UTC
moment is derived from it + the clinic TZ at booking. This is the correct model and
avoids off-by-a-day bugs.

## Backend

### B1 -- Clock + serialization
- `Configure<AbpClockOptions>(o => o.Kind = DateTimeKind.Utc)` in the HttpApiHost module.
  Effect: ABP stamps `Kind=Utc` on instants and the JSON serializer emits a trailing
  `Z`, so the SPA reliably parses them as UTC. (Verified: currently `Unspecified` ->
  naive no-`Z` strings.)
- Opt bare-date + clinic-local fields OUT with `[DisableDateTimeNormalization]` so they
  stay naive (no `Z`, no day-shift): `Patient.DateOfBirth`,
  `AppointmentInjuryDetail.DateOfInjury`/`ToDateOfInjury`,
  `DoctorAvailability.AvailableDate`, and any other pure-date column. **Highest-risk
  item -- without it bare dates serialize as `...Z` and the browser can move the day.**

### B2 -- Location timezone (per-Location, Adrian's choice)
- Add required `TimeZoneId` (IANA string, max 64) to `Location`: entity + ctor +
  `LocationManager` validate + Create/Update DTOs + `LocationDto` + EF config + a new
  migration + Mapperly auto-map. Seed both CA demo clinics = `America/Los_Angeles`.
- Angular: add `timeZoneId` to the location proxy DTOs + the location admin form (a
  select of common US IANA zones, default `America/Los_Angeles`).
- Surface it on the read paths the frontend needs: `AppointmentWithNavigationPropertiesDto.Location`
  already bundles the Location (so adding it to `LocationDto` carries it), and the
  booking slot lookup `DoctorAvailabilityDto` gets the clinic `TimeZoneId` so the picker
  can label slots.

### B3 -- appointment moment = UTC, derived from the slot
- In `AppointmentsAppService.CreateAsync` (+ reval/reSubmit): set `AppointmentDate` to a
  true UTC instant derived from the chosen slot (`DoctorAvailability.AvailableDate` +
  `FromTime`) converted via the Location's `TimeZoneId`
  (`TimeZoneInfo.FindSystemTimeZoneById(iana)` + `ConvertTimeToUtc`). The slot is the
  source of truth -- do not trust the client-posted naive `appointmentDate`.
- Fix lead-time + not-in-past checks (`BookingPolicyValidator`,
  `EnsureAppointmentDateNotInPast`) to compare in UTC (the stored value is now UTC;
  they currently compare against server-local `DateTime.Today`).
- DoctorAvailability generation / conflict / past-date logic stays clinic-local
  (UNCHANGED); audit its `.Date` + `DateTime.Today` uses to confirm they remain
  clinic-local-correct (research flagged these).

### B4 -- reseed (no migration)
- Reseed the dev DB: locations get `TimeZoneId`; recreate test appointments through the
  booking flow (now storing UTC) or a small seeder using `Clock.UtcNow`. Wipe the SQL
  volume + re-run DbMigrator + seed. No production data -> no row migration.

## Frontend

### F1 -- the resolver (TDD)
- `AppDateTimePipe` (`appDt`) + `clinic-time.util.ts` under `angular/src/app/shared/date/`.
  Intents:
  - `appDt:'instant'` (default) -> parse UTC, format in the BROWSER tz via
    `Intl.DateTimeFormat` (IANA, DST-correct) + append the browser tz abbr (e.g. `PST`).
  - `appDt:'clinic':clinicTzId` -> format in the clinic IANA tz + clinic abbr.
  - `appDt:'bareDate'` -> date portion only, no conversion, no label (DOB/injury).
  - `appDt:'<literal Angular format>'` -> escape hatch for compact visual chips (no label).
  - Built on `Intl.DateTimeFormat` (no new dependency; DST-correct, unlike Angular
    DatePipe's fixed-offset `timezone` param). Null -> ''.
- Unit-test: instant->browser tz, clinic->clinic tz, bareDate no shift across a DST
  boundary, label presence/absence per intent.

### F2..F6 -- apply across the audited ~30 sites
- Map each display to an intent (from the earlier audit): appointment date/time ->
  `clinic` (clinic TZ from the appointment's Location); created/modified/changelog/
  packet/requestedOn/decideBy -> `instant`; DOB/dateOfInjury -> `bareDate`; compact
  home/dashboard chips -> literal. Fix the 4 raw leaks (wizard review `fieldVal`,
  public-consent `requestedNewDateTime`).
- Booking SLOT picker: label the available-times list with the clinic tz ("9:00 AM PST"),
  clinic tz from the location lookup. Picker selects clinic-local; B3 derives UTC on save.

## Risks / gotchas (from research)
- Bare-date normalization (B1) is the top risk -- test DOB/injury/AvailableDate stay
  un-shifted after `Clock.Kind=Utc`.
- Do NOT convert availability to UTC; keep clinic-local + verify the `.Date`/`DateTime.Today`
  comparisons.
- `TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles")` must resolve in the Linux
  api container (.NET 6+ supports IANA cross-platform) -- verify early.
- Reseed avoids mixed-kind audit rows (old Unspecified + new Utc -> inconsistent `Z`).
- Ripple to NON-reworked pages: legacy patient list (`abpUtcToLocal`) will start showing
  correct browser-local; legacy `appointment-view` (naive) would shift -> out of scope,
  flag for its redesign.

## Verification
- Backend: `dotnet build`; api emits `Z` for instants + NO `Z` for DOB/AvailableDate; an
  appointment booked at clinic-local 9:00 AM stores the right UTC instant and reads back
  as 9:00 AM in the clinic TZ; DOB unchanged.
- Frontend: build + karma (pipe spec); live -- book an appointment, confirm it shows
  "9:00 AM PST" everywhere (list/detail/reports) regardless of viewer; instants show in
  browser tz with a label; DOB unchanged; the 4 raw leaks gone.
- Reseed + regression across the reworked surfaces.

## Build as ONE fix (Adrian 2026-06-15)
Build B1-B4 then F1-F6 in sequence on ONE branch (`feat/global-datetime-resolver`),
per-task commits, then a SINGLE squash-merge to `feat/internal-user-pages` (no split
PRs). Within the one branch, still land + smoke-test the backend (B1-B4 -> API emits
correct UTC) before the display rollout (F1-F6 depends on the `Z` suffix).

## Timing: DEFERRED (do NOT implement yet)
Adrian is finishing the UI rework (Prompts 14-17) first, then will fix date/time while
testing. This plan is parked (status stays draft). Coupling to keep in mind during the
remaining UI rework: Prompt 14 (Scheduling) redesigns the SAME `Location` form (which
will gain the `TimeZoneId` field from B2) and the availability grid (slots will later
gain a clinic-TZ label). The Scheduling rework should build with the CURRENT naive date
handling but leave clean seams -- this plan retrofits the timezone model afterward.

## Out of scope
- Non-reworked legacy pages' display (their own redesign prompts).
- Multi-timezone tenant UI beyond the per-location field.
- A user-profile TZ override (browser-detected per Adrian's decision).
