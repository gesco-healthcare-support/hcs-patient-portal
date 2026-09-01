---
id: BUG-021
title: Datepicker shows all dates disabled while slot fetch is in flight (no loading indicator on day cells)
severity: low
status: open-low
found: 2026-05-14 hardening Phase 3.14
flow: booking-form-ux
component: angular/src/app/appointments/appointment-add.component.ts (markAppointmentDateDisabled)
---

# BUG-021 - Datepicker shows all dates disabled during initial slot fetch

## Severity downgrade
**Originally filed as "CE cannot book"** - that was wrong. CE has the permission, the slot endpoint returns 347 rows for CE, and the datepicker eventually shows enabled days for CE just like every other booker role. Verified by direct fetch + by re-opening the picker after the SPA finished loading slots.

The remaining concern is a UX issue, not a role/auth issue.

## Symptom
On `/appointments/add`, after selecting Appointment Type + Location, the user opens the datepicker. If the user opens it **before** `fetchAllAvailableSlots` resolves (network round-trip ~50-200 ms on local), every single day cell renders with `aria-disabled="true"`, no day is selectable, and there is no UI indicator on the picker itself that says "loading".

The root cause is `appointment-add.component.ts:1057-1074`:
```ts
readonly markAppointmentDateDisabled = (date: NgbDateStruct): boolean => {
  ...
  if (this.availableDateKeys.size === 0) {
    return true;       // all dates disabled while keys empty
  }
  return !this.availableDateKeys.has(this.toDateKey(...));
};
```

When the slot fetch hasn't yet populated `availableDateKeys`, the picker treats every date as unavailable. The form does have an `isAvailableDatesLoading` flag tied to a separate spinner, but the spinner is positioned outside the picker, so when the user has the picker open they see a fully-disabled grid with no loading hint.

## Hypothesis (single)
This is intentional defensive behavior - the picker should not let the user pick a date that hasn't been confirmed by the slot API. But the UX could be improved: show "Loading slots..." inside the picker grid, or grey out the open trigger until the fetch resolves.

## Functional impact
Low. Users who wait a second see normal behavior. Fast clickers (or automation) experience the all-disabled state and incorrectly conclude they can't book.

## Recommended fix
1. Show a loading state inside the open datepicker (e.g., "Loading availability..." over the day grid) when `isAvailableDatesLoading === true`.
2. Or disable the `<input ngbdatepicker>` element itself until the first slot fetch resolves, so the picker can't be opened during the loading window.

## To do
- Verify on a slow connection (throttle to 3G) - the effect is more pronounced.
- Confirm OLD app parity: did the OLD app's picker grey out during loading?

## Related
- Closes the "CE cannot book" diagnosis - false positive from automation hitting the picker before the SPA finished its fetch.
