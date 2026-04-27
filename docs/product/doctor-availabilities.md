[Home](../INDEX.md) > [Product Intent](./) > Doctor Availabilities

# Doctor Availabilities -- Intended Behavior

**Status:** draft -- Phase 2 T3, interview in progress
**Last updated:** 2026-04-24
**Primary stakeholder:** the doctor's admin (tenant-level admin role at the medical-examiner practice)

> This document captures INTENDED behaviour for the Doctor Availabilities feature -- how the doctor's office publishes the bookable schedule that the Appointments feature consumes. It does NOT describe what the code currently does (that is `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md` and `docs/features/doctor-availabilities/overview.md`). Every claim carries a source tag. Code is never cited as authoritative for intent. Observations from code appear ONLY in the Known Discrepancies section, tagged `[observed, not authoritative]`.

## Purpose

The Doctor Availabilities feature lets the doctor's office publish their bookable schedule so the four booker personas can request appointments against real openings. It is MVP pipeline step 3 (per `appointments.md`), between user login and appointment booking. Availability is per-tenant (one practice), typed by appointment type, and may span multiple locations. The booker's view of availability is filtered by the appointment type and location they choose on the booking form, and only slots in an appropriate state for booking are shown. [Source: Adrian-confirmed 2026-04-24 on MVP role, type-awareness, and multi-location support]

## Personas and goals

Persona definitions live in [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md). Appointment-side goals are captured in [appointments.md](appointments.md). This section captures Doctor-Availabilities-specific goals.

### Doctor's office (author of the schedule)

Authority: the tenant-level admin role (working name **doctor's admin**; final name open) owns the schedule. The medical examiner (Doctor role) can also maintain the schedule. Other staff users VIEW-only. [Source: implied from appointments.md 2026-04-22/2026-04-23 office-staff authority]

Feature-specific goals:

- Publish the doctor's upcoming availability in bulk with minimal ceremony (three publishing modes: date-range bulk, weekday-pattern bulk, one-at-a-time). [Source: Adrian-confirmed 2026-04-24]
- Correct individual slots when errors or exceptions occur (e.g., doctor needs to block a specific slot, correction to a typo in a published range). [Source: Adrian-confirmed 2026-04-24]
- Avoid directly editing booked slots -- instead route any change through the formal review / modification flow so every commitment to a booker has a clean audit trail. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION per Q19]
- [UNKNOWN: does the office have a "block out this time" workflow that doesn't delete the slot but hides it from bookers? Tied to the Reserved-status interpretation in Q17.]

### Bookers (consumers of the schedule)

All four booker personas (patient, applicant attorney, defense attorney, claim examiner) see the published schedule as part of the booking flow and pick a slot when they submit a request. Booker-side goal is "find a time that works and book it"; they do not publish slots, they only consume them. [Source: implied from appointments.md booking-flow intent]

Feature-specific consumer goals -- what a booker expects to see when picking a slot -- is [UNKNOWN -- queued for Adrian].

### Host admin (Gesco-side)

Inherits the broader oversight / break-glass role from `appointments.md`. Whether host admin has direct schedule-editing authority in DoctorAvailabilities (beyond the modification authority already captured for Appointments) is [UNKNOWN -- queued for Adrian].

## Intended workflow

### Publishing the schedule (MVP)

The office publishes the schedule through **three modes**, used in combination as needed: [Source: Adrian-confirmed 2026-04-24]

1. **Bulk by date range.** The office selects a range and a granularity -- explicit from / to dates, or a preset window (this week, next week, this month, next month, this quarter, this year, etc.) -- plus a daily time window and slot duration. The system generates slots across the range.
2. **By weekday pattern.** The office specifies which weekdays (e.g., Monday / Wednesday / Friday) and how far the pattern repeats -- a single week, N weeks, N months, or a full year. Slots are generated on matching weekdays within the window with the chosen time window and duration.
3. **One-at-a-time modal.** Escape hatch for error corrections, exceptions, and individual adjustments. The office edits or creates a single slot without touching the bulk schedule.

All three modes share the same downstream outcome: individual slot records in the database. The difference is authoring convenience. [Source: Adrian-confirmed 2026-04-24]

**Open implementation intent:**

- [UNKNOWN -- queued for Adrian: conflict detection in the bulk modes -- does the system show conflicts against existing slots and let the office resolve them before submission (matching the current code's behaviour), or does bulk generation always succeed and the office manages conflicts afterward?]
- [UNKNOWN -- queued for Adrian: preview-before-submit -- is the bulk preview a required step of the workflow, or can the office submit bulk generation without previewing?]

### Booker consumption

Inside the booking form, the booker first picks an **appointment type** (from a dropdown) and a **location** (from a dropdown of the practice's locations). Once both are set, the form shows the available dates + times filtered to that combination. Before both type and location are chosen, no availability is shown. [Source: Adrian-confirmed 2026-04-24]

At a single-location practice, the location dropdown degenerates (collapses, hides, or pre-selects the only option). At a multi-location practice, the booker sees the practice's full list of locations and picks one explicitly. [Source: Adrian-confirmed 2026-04-24]

**Open implementation intent:**

- [UNKNOWN -- queued for Adrian: the "slot type filter" interpretation. If slots are fully type-specific (AppointmentType required on slot), the date / time list shows only slots tagged for the chosen type. If slots are mixed (AppointmentType optional), should the list include (a) only slots tagged for the chosen type, or (b) tagged-for-this-type PLUS untagged "general" slots? Tied to the open model decision on `AppointmentTypeId` required vs. optional.]

### Slot lifecycle

**Confirmed transitions (manager-validated 2026-04-24).** The manager did not recall the `Reserved` status either and indicated "you might be correct" on Adrian's pending-review interpretation from T3. That interpretation is now the working intent: **`Reserved` is the state of a slot with a pending appointment request awaiting office review**. Under this interpretation:

1. Slot is created by the office as `Available`.
2. A booker submits an appointment request against the slot -- slot moves to `Reserved` (pending review).
3. Office approves the request -- slot moves to `Reserved -> Booked`.
4. Office rejects the request -- slot moves to `Reserved -> Available` (returned to the pool).
5. Office sends-back-for-info -- slot stays `Reserved` until booker responds (matching the "Awaiting more info from booker" appointment status).

[Source: Adrian best-guess 2026-04-24 at T3 interview; manager-validated 2026-04-24 in email response round ("you might be correct"). Resolves OUTSTANDING-QUESTIONS.md Q17.]

Two rejected candidate meanings (documented here so the manager conversation can rule them out):

- **"Doctor is busy"** -- the slot is blocked because the doctor has something else at that time. Adrian: "in which case, why even have that slot open?" Reasonable self-rebuttal -- if the doctor is busy, the office should not publish the slot at all. Unlikely to be intent.
- **"Approved appointment"** -- `Reserved` means the slot has an approved booking attached. Adrian: "we would just remove the slot from availability list instead of marking it reserved." Reasonable self-rebuttal -- an approved appointment is already `Booked`; a separate `Reserved` marker for the same thing would be redundant. Unlikely to be intent.

**Slot release on cancel / reschedule.** Tied to Q1 (is cancel/reschedule in MVP at all?). If yes, intent is presumably that a cancelled appointment returns its slot to `Available`; a rescheduled appointment releases the original slot and books the new one. [Source: Adrian best-guess 2026-04-23 -- NEEDS CONFIRMATION per Q1 in OUTSTANDING-QUESTIONS.md.]

## Business rules and invariants

### Confirmed (partial)

- **Slots carry appointment-type awareness.** A slot is NOT a general "any-exam-fits" time window. Each slot is either pre-tagged by the office with a specific appointment type, OR some slots are tagged while others are open -- Adrian is not sure which of those two models MVP will use, but has ruled out the fully-general model. The booker UX will therefore take the booker's selected exam type into account when showing available times, not show all slots regardless of type. [Source: Adrian-confirmed 2026-04-24]
  - [UNKNOWN -- pending manager or Adrian follow-up: is `AppointmentTypeId` on a slot REQUIRED (type-specific model) or OPTIONAL (mixed model)? The difference decides whether the office has to pick a type every time they publish a slot, and whether the booker sees "all slots matching type" vs "typed-or-general slots matching type".]
- **A practice may be single-location or multi-location; MVP supports both.** Some practices operate at one location; others have a schedule that spans several locations (e.g., Mondays in LA, Wednesdays in San Diego). MVP handles both patterns from day one. When a practice has a single location, UI elements that would otherwise ask for a location collapse or are hidden. When a practice has multiple locations, the office picks the location for each bulk-generation run or single-slot add, and the booker sees location information with each slot. [Source: Adrian-confirmed 2026-04-24]
- **Publishing supports three authoring modes.** Bulk by date range (with preset granularities: this / next week, month, quarter, year, or explicit dates), bulk by weekday pattern (for a week, N weeks, N months, or a year), and one-at-a-time single-slot modal for corrections and exceptions. All three modes create individual slot records; they differ only in authoring convenience. [Source: Adrian-confirmed 2026-04-24]
- **Booker picks appointment type and location before seeing availability.** Booking form flow is: pick appointment type -> pick location -> availability list is filtered to that combination. No availability is shown before both are chosen. [Source: Adrian-confirmed 2026-04-24]
- **Booked slots are locked from direct office edits (best-guess pending confirmation).** Once a slot is booked, the office cannot directly change its time or location; any change must go through the formal cancel / reschedule flow (subject to Q1 on whether that flow is in MVP at all). Rationale: keep the audit trail clean and avoid back-door edits on commitments to bookers. [Source: Adrian best-guess 2026-04-24 -- NEEDS CONFIRMATION per OUTSTANDING-QUESTIONS.md Q19]

### Open (to resolve during interview)

- Duration -- **RESOLVED 2026-04-24 by manager response.** 15 min is the default; the office can change the duration in the new-slot publishing form before publishing. Matches the "independently set per slot" candidate model. No type-derived durations; no type-default with override; just one default (15) that the office can change at publishing time. Once a slot is published and then booked, duration is locked per the T7 universal post-submit lock rule. [Source: manager-confirmed 2026-04-24; fine detail Adrian-confirmed 2026-04-24 (set at publishing only, not editable post-booking). Resolves OUTSTANDING-QUESTIONS.md Q18.]
- Overlap rules -- can two slots at the same location overlap in time? Same time, different locations?
- Reserved status -- what does it mean and when is it used?
- Slot editing after booking -- can the office edit a `Booked` slot's time / location?
- Bulk delete -- any MVP safety (confirmation prompts, undo, blocked when slots are booked)?
- Preview-before-submit on bulk generation -- required step or optional?

## Integration points

### Upstream

None from outside the portal at MVP -- the office publishes slots directly in the portal. [Source: inferred from Adrian 2026-04-24 MVP narrowing; confirm during interview]

### Downstream (Appointments)

On appointment booking, the chosen slot's `BookingStatus` changes to `Booked`. On appointment cancellation / reschedule, the slot's status transition is [UNKNOWN -- tied to Q1 outstanding question].

## Edge cases and error behaviors

[UNKNOWN -- candidate edge cases to resolve in interview:]

- Office deletes a slot that already has a booking attached. Hard-block, cascade-delete the booking, or orphan the booking?
- Office changes a `Booked` slot's time. Booker auto-notified? Or blocked?
- Two office staff generate for the same location / date simultaneously. Race behaviour?
- Booker attempts to book a slot that just got `Booked` by another booker between form-open and submit.
- Office publishes availability in the past (via datepicker misuse). Hard-block?

## Success criteria

First-pass sketch (to be tightened as open items resolve):

- Doctor's admin can publish a month of slots using the bulk-date-range mode without manual slot-by-slot data entry.
- Doctor's admin can publish a weekday-pattern (e.g., "every Monday, Wednesday, Friday through Q3") across multiple months or a full year in a single action.
- Doctor's admin can correct a specific slot via the one-at-a-time modal after bulk publishing.
- Booker sees availability filtered to (appointment type, location) without stale cross-contamination.
- Multi-location practice's booker sees the location picker; single-location practice's booker does not.
- A slot moved to `Booked` (or `Reserved`, pending Q17) is invisible in the booker's availability list.
- Per the ex-parte rule in `appointments.md`, any change to a booked slot that the office eventually executes (pending Q1 + Q19) triggers an all-parties notification in the strict format.

## Known discrepancies with implementation

Pending Phase 3 cross-reference pass. Candidate entries surfaced during evidence load:

- `[observed, not authoritative]` The code defines a `Reserved` status value but no automated code path writes `Reserved`. **Intent divergence (Adrian 2026-04-24):** Adrian did not recognise the `Reserved` status and offered three candidate meanings. The leading best-guess (most consistent with the confirmed two-step booking flow) is that `Reserved` is the state of a slot with a **pending appointment request awaiting office review**. If confirmed, this means the current code's behaviour -- marking a slot `Booked` on appointment create (request phase) -- is wrong: it should mark the slot `Reserved` on request, then transition to `Booked` only on office approval (and to `Available` on reject / send-back-and-expire). The state-machine wiring between Appointment creation and slot status needs a re-spec once the manager confirms the Reserved semantics. See Slot lifecycle subsection and OUTSTANDING-QUESTIONS.md Q17.
- `[observed, not authoritative]` `DoctorAvailabilityManager.CreateAsync` and `UpdateAsync` perform no domain-level validation (no overlap checking, no date validation). All constraint enforcement lives in the AppService / Angular UI. Whether domain-level validation is intent is pending interview.
- `[observed, not authoritative]` `UpdateAsync` accepts `BookingStatusId`, so staff can flip a `Booked` slot back to `Available` even when an Appointment still references it. No guard prevents this.
- `[observed, not authoritative]` Generate-preview conflict detection is stateless; a race between two concurrent generation attempts for the same location / date can create overlapping slots.
- `[observed, not authoritative]` The weekday-pattern generation mode in the current code is bounded to a SINGLE month. **Intent divergence (Adrian 2026-04-24):** confirmed MVP intent is that the weekday-pattern mode spans a single week, multiple weeks, multiple months, or a full year. The month-only scope is a code gap vs. intent.
- `[observed, not authoritative]` The current bulk modes are limited to "explicit date range" and "weekday pattern within a month". **Intent divergence (Adrian 2026-04-24):** bulk by date range should also support preset granularities (this/next week, month, quarter, year). Preset granularities do not exist as a code concept today.
- `[observed, not authoritative]` Three delete modes (single / by slot / by date) are available without MVP-scope confirmation that all three are needed.
- `[observed, not authoritative]` No tests exist for DoctorAvailabilities (per `FEAT-07`).
- `[observed, not authoritative]` `docs/business-domain/DOCTOR-AVAILABILITY.md` describes a bidirectional state transition (`Booked -> Available` on appointment cancellation) that does NOT exist in the code. The business-domain file is classified OBSERVATION-ONLY per `docs/product/README.md` and is reverse-engineered from code + intent guess; this specific transition claim is aspirational, not observed.

## Outstanding questions

Each bare `[UNKNOWN]` above rolls up to [OUTSTANDING-QUESTIONS.md](OUTSTANDING-QUESTIONS.md) once surfaced as manager-facing.

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
