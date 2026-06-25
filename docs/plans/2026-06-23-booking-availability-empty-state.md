---
feature: booking-availability-empty-state
date: 2026-06-23
status: in-progress
base-branch: development
related-issues: []
---

## Goal

Stop the booking flow from silently hiding availability: (A) the booking wizard
must explain WHY no dates are selectable, and (B) the slot-generation flow must
warn when staff create slots inside the lead-time window (which are permanently
non-bookable).

## Context

Diagnosed root cause (NOT a logic bug): the booking lead time
(`SystemParameter.AppointmentLeadTime`, default 3) correctly hides slots dated
within `today + leadTime`. `DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync`
filters `AvailableDate >= today + leadTime`; the booking wizard
(`appointment-add.component.ts`) disables EVERY calendar date when the resulting
`availableDateKeys` set is empty, with no message. So when the only slots are
within the window (e.g. all on "today"), the booker sees an all-grey calendar and
no explanation. Confirmed live: lookup returned 0 for in-window slots; a slot
generated beyond the window made exactly that date selectable.

A slot dated within `today + leadTime` can never be booked (booking would have to
happen in the past), so generating one is always futile -- yet the generate flow
gives no feedback.

Decision (2026-06-22, Adrian): lead time stays UNIFORM (no internal-staff bypass);
no demo-slot seed. Fix the two UX/guidance gaps only. The lead time itself is a
correct, intentional, per-tenant business rule.

## Approach

- **A. Wizard empty-state message (FE-only).** After the slot lookup resolves with
  a type + location selected and zero bookable dates, render an informative message
  by the date picker explaining the lead-time rule, instead of a silent grey
  calendar. Use the authoritative `appointmentLeadTime` (via `SystemParametersService`)
  for the day count rather than the hardcoded `minimumBookingDays = 3`.
- **B. Generate-slots in-window warning (FE-only).** In the slot-generation flow,
  fetch `appointmentLeadTime` and flag any previewed/selected slot date earlier than
  `today + leadTime`, surfacing a clear warning that those slots will not be
  bookable. Non-blocking (staff may still have a reason to record them), but loud.

Rejected: internal-staff lead-time bypass (Adrian: lead time is uniform/intentional);
demo-slot seed (declined); a backend "why empty" flag (FE has enough -- empty set +
known lead time -- to message accurately without a server change).

## Tasks

- T1: Booking-wizard empty-availability message.
  - Add a computed signal/getter in `appointment-add.component.ts` (e.g.
    `noBookableDatesMessage`) true when `checkForAppointmentTypeSelected &&
    !isAvailableDatesLoading && availableDateKeys.size === 0`; message names the
    lead-time days. Source the lead time from `SystemParametersService` (fall back
    to `minimumBookingDays` if the fetch fails). Render in the schedule section
    template near the date picker.
  - approach: test-after
  - files-touched: [angular/src/app/appointments/appointment-add.component.ts,
    angular/src/app/appointments/sections/appointment-add-schedule.component.ts,
    angular/src/app/appointments/sections/appointment-add-schedule.component.html]
  - acceptance: with type+location selected and no bookable dates, the wizard shows
    a clear message explaining the N-day lead time (not a silent grey calendar);
    when bookable dates exist, no message and the calendar enables them.

- T2: Generate-slots in-window warning.
  - In the generate flow, fetch `appointmentLeadTime` (SystemParametersService) and
    mark any preview/selected slot whose date < `today + leadTime`; show a visible
    warning naming the lead time. Reuse the slot-date computation in
    `gen-slots.util.ts` for the comparison.
  - approach: test-after
  - files-touched:
    [angular/src/app/doctor-availabilities/doctor-availability/internal-generate-slots.component.ts,
    angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts,
    angular/src/app/doctor-availabilities/doctor-availability/gen-slots.util.ts (+ templates)]
  - acceptance: previewing/generating slots dated within `today + leadTime` shows a
    warning naming the lead time; slots beyond the window show none.

## Risk / Rollback

- Blast radius: booking wizard schedule step + the generate-slots admin flow.
  FE-only; no backend change, no migration, no booking-policy change.
- Rollback: revert the per-task commits.
- Observation (out of scope, flag): the server uses UTC (`GETUTCDATE` / container
  `DateTime.Today` = 2026-06-23) while the stated local date is 2026-06-22 PT. A
  UTC-vs-local-midnight gap can shift the lead-time boundary by a day at edges;
  worth a follow-up if booking-window off-by-one reports appear. Not addressed here.
- Follow-up (out of scope): the wizard hardcodes `minimumBookingDays = 3` while the
  server reads `AppointmentLeadTime`; T1 sourcing the real value reduces this drift,
  but the standalone constant could later be removed in favor of the parameter.

## Verification

1. Wizard (the original repro): as staff, select AME + Demo Clinic North with only
   within-window slots -> message shown, calendar greyed. Generate a slot beyond the
   lead window -> that date becomes selectable and the message clears. (Confirmed
   manually during diagnosis that the calendar enables a beyond-window slot.)
2. Generate flow: create slots on a within-window date -> warning appears naming the
   lead time; on a beyond-window date -> no warning.
3. `npx ng build --configuration development` green; add focused component tests for
   the empty-state flag (T1) and the in-window predicate (T2).
