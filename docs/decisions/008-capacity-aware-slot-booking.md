# ADR-008: Capacity-aware slot booking

**Status:** Accepted
**Date:** 2026-05-15
**Verified by:** code-inspect

## Context

The initial booking model marked a `DoctorAvailability` slot `BookingStatus = Booked` when an
appointment was created, so each slot held exactly one appointment. Doctors' offices needed to
accept several IME appointments in the same time block (overbooking up to a per-slot capacity),
which the one-appointment-per-slot model could not express.

## Decision

A slot stays `BookingStatus.Available` after a booking. Fullness is computed at booking time:
count the active (non-terminal) appointments referencing the slot and compare to
`DoctorAvailability.Capacity` (default 3). The five terminal statuses (Rejected, CancelledNoBill,
CancelledLate, RescheduledNoBill, RescheduledLate) do not count, so a cancellation automatically
frees capacity. `CreateRangeAsync` bulk-generates slots in one transaction. `BookingStatus.Booked`
is retained only as a legacy enum value; the new flow never sets it, and `SlotCascadeHandler` is
reduced to a log-only stub.

## Consequences

- Multiple appointments per slot, up to capacity; cancellations free capacity with no extra work.
- "Is this slot full?" becomes a computed query (`EfCoreAppointmentRepository.GetActiveCountForSlotAsync`),
  not a single column read.
- `DoctorAvailability.BookingStatusId` is now largely vestigial and can be flipped without a guard
  (a known gotcha tracked in the DoctorAvailabilities CLAUDE.md), since it no longer gates booking.

## Alternatives Considered

- Keep per-slot `Booked` plus a separate `SlotCapacity` table -- rejected: extra schema for the same
  computed answer.
- Add a denormalized `BookedCount` column on the slot -- rejected: a counter can drift from the
  actual active-appointment count; deriving from appointments is the source of truth.
