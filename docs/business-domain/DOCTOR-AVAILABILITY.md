# Doctor Availability

> Purpose: the availability slot model, how a slot fills under the capacity model, bulk generation, and
> what the booking gate checks. Audience: developers.

[Home](../index.md) > [Business Domain](./) > Doctor Availability

## Overview

Availability is the scheduling backbone of the portal. Each `DoctorAvailability` record is one time
window on one date at one location. Office staff publish slots -- Intake Staff hold full create, edit
and delete rights on them (`InternalUserRoleDataSeedContributor.IntakeStaffGrants`) -- and appointments
are booked against them. Slots belong to the office, so each office database holds its own.

## The slot

`src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs`:

| Property | Meaning |
|----------|---------|
| `AvailableDate` | The calendar date |
| `FromTime`, `ToTime` | The time window (`TimeOnly`) |
| `LocationId` | Where the appointment takes place |
| `AppointmentTypes` | The appointment types the slot offers -- a collection, so one slot can offer several types |
| `Capacity` | How many active appointments the slot takes. Default 3, at least 1 |
| `BookingStatusId` | Available, Booked or Reserved -- see below |

## How a slot fills: the capacity model

A slot does **not** change status as it is booked. Whether it can take another appointment is decided at
booking time by counting its **active** appointments against its `Capacity`. Appointments in the five
terminal statuses (Rejected, CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate) do not
count, so a cancellation frees capacity without anything writing to the slot.

`BookingStatus` (`Domain.Shared/Enums/BookingStatus.cs`):

| Value | Status | Meaning today |
|-------|--------|---------------|
| 8 | **Available** | The normal state, full or not |
| 9 | **Booked** | Legacy. The capacity model no longer sets it. The booking gate treats it as Available, but the booking-form slot picker lists only Available slots, so a Booked slot is not offered there |
| 10 | **Reserved** | Closed to booking: set when staff close a slot, and used as the transient hold on a slot proposed for a reschedule (released when the request is decided) |

## The booking-form slot picker

`DoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync` feeds the date and time picker of the
booking form. It returns the slots at the chosen location that are **Available**, fall on or after today
plus the office's lead time (`SystemParameter.AppointmentLeadTime`), offer the chosen appointment type (a
slot with no types offers any type), and still have room. Each carries its `RemainingCapacity`; full slots
are left out.

## The booking gate

The picker is a convenience; the server re-checks the slot when the appointment is booked
(`AppointmentsAppService.ValidateDoctorAvailabilityForBookingAsync`), in this order:

1. **Closed** -- a `Reserved` slot always refuses (`AppointmentBookingSlotClosed`).
2. **Full** -- active appointments >= `Capacity` refuses (`AppointmentBookingSlotFull`).
3. **Wrong type** -- if the slot lists appointment types and the requested type is not among them, it
   refuses (`AppointmentBookingSlotTypeMismatch`).
4. **Mismatch** -- the booking's location, date and time must match the slot.

The lead-time and maximum-horizon rules for the date are checked separately by `BookingPolicyValidator`.

## Bulk generation

Staff generate many slots at once on the generate page (see [Pages](#pages)). The input is
`DoctorAvailabilityGenerateInputDto`:

- **Dates**: a `FromDate`-`ToDate` span, optionally limited to weekdays (`SelectedDays`, 0 = Sunday; an
  empty list means every day), or an explicit list of dates (`SelectedDates`, no duplicates). No date may
  be in the past (Pacific time).
- **Time ranges**: one or more `TimeRanges`, applied to every chosen date, each cut into slots of the
  range's duration (or the default `AppointmentDurationMinutes`, 15). Ranges may not overlap each other.
- **Location** (required), **appointment types**, **capacity** (default 3, at least 1) and the initial
  booking status.
- At most 5000 slots per call (`GenerationSlotLimit`).

The page runs two server calls:

1. **Preview** -- `GeneratePreviewAsync` expands the input and flags a generated slot as a conflict when it
   overlaps an existing slot at the **same location** on the same date. The message differs when the
   overlapped slot is Reserved.
2. **Create** -- `CreateRangeAsync` re-runs the same preview on the server rather than trusting the page's
   copy (another user may have created a colliding slot in between), inserts the non-conflicting slots, and
   returns how many were inserted and how many were skipped.

## Pages

All under `angular/src/app/doctor-availabilities/`; routes in `angular/src/app/app.routes.ts`.

| Page | Route | Component |
|------|-------|-----------|
| Availability list | `doctor-management/doctor-availabilities` | `doctor-availability/internal-availabilities.component.ts` |
| Generate | `doctor-management/doctor-availabilities/generate` (and `/add`) | `doctor-availability/internal-generate-slots.component.ts` |
| Schedule week view | `doctor-management/schedule` | `schedule/internal-schedule.component.ts` |

## Source references

- Entity: `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs`
- Enum: `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/BookingStatus.cs`
- Generation and conflicts: `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs`
- Booking gate: `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`
  (`ValidateDoctorAvailabilityForBookingAsync`)

## Related documentation

- [Appointment Lifecycle](APPOINTMENT-LIFECYCLE.md)
- [Domain Overview](DOMAIN-OVERVIEW.md)
- [Appointment Booking Flow](../frontend/APPOINTMENT-BOOKING-FLOW.md)
