---
type: deep-dive
parent-audit: staff-supervisor-doctor-management.md
audited: 2026-05-01
status: investigation-complete
---

# Slot generation -- deep dive

Comprehensive walkthrough of how OLD's Staff Supervisor generates `DoctorsAvailability` rows. Reverse-engineered from `DoctorsAvailabilityDomain.cs` (475 lines) + `doctors-availability-add.component.ts` (305 lines) + the model file.

## What slot generation produces

A `DoctorsAvailability` row per slot. Each slot is one bookable time window:

```
DoctorsAvailability {
  DoctorsAvailabilityId  PK
  DoctorId               FK
  AppointmentTypeId      FK (nullable -- if null, slot accepts any type)
  LocationId             FK
  AvailableDate          datetime  (the calendar day)
  FromTime               time      (start of slot)
  ToTime                 time      (end of slot; FromTime + AppointmentDurationTime)
  StatusId               Active / Delete
  BookingStatusId        Available / Reserved / Booked
}
```

Slot duration is fixed system-wide via `SystemParameters.AppointmentDurationTime` (in minutes; e.g., 60 for one-hour slots). Supervisor cannot pick variable slot lengths -- it is one number per deploy.

## The two generation modes (UI toggle: `slotByDate`)

The Add form has a toggle that switches between two input modes for picking which calendar days get slots. The time-window-per-day inputs (FromTime, ToTime) and AppointmentTypeId + LocationId are common to both.

### Mode 1 -- By date range (`slotByDate = true`, `slotType = 2`, default)

Inputs:

- `AppointmentTypeId`
- `LocationId`
- `FromDate` (calendar date)
- `ToDate` (calendar date)
- `FromTime` (e.g., 09:00)
- `ToTime` (e.g., 17:00)

Behavior: generate slots for every calendar day in `[FromDate, ToDate]` inclusive. Each day gets the same set of time slots (FromTime to ToTime, divided by `AppointmentDurationTime`).

Example: FromDate = 2026-06-01, ToDate = 2026-06-03, FromTime = 09:00, ToTime = 12:00, AppointmentDurationTime = 60. Result: 3 days x 3 slots/day = 9 slots: (Jun 01 09-10, 10-11, 11-12), (Jun 02 09-10, 10-11, 11-12), (Jun 03 09-10, 10-11, 11-12).

### Mode 2 -- By day-of-week + month (`slotByDate = false`, `slotType` toggled via `checkForSlot()`)

Inputs:

- `AppointmentTypeId`
- `LocationId`
- `isSelectedMonth` (bool) -- true = current month; false = supervisor picks a month
- `forMonth` (1-12) -- only required when `isSelectedMonth = false`
- `fromDays` (0-6) -- start day-of-week (0 = Sunday)
- `toDays` (0-6) -- end day-of-week
- `FromTime`, `ToTime` (time window per day)

Behavior: within the chosen month, generate slots only on days whose day-of-week is between `fromDays` and `toDays`.

Example: month = June 2026, fromDays = 1 (Monday), toDays = 5 (Friday). Result: slots for every Mon-Fri in June 2026.

The Angular helpers `getNextFromDay()` and `getNextToDay()` compute the actual `FromDate` and `ToDate` from these day-of-week selections within the chosen month, then the backend generation loop runs the same per-day loop as Mode 1 over the resolved date range.

End-of-month guard: if `getNextFromDay()` resolves a date past the last day of the chosen month, the form rejects with `validation.message.custom.monthEnd`.

### Status of `slotType = 1`

`slotType = 1` is referenced in code but never set in the default-form-init. The on-init default is `slotType = 2`. The `checkForSlot()` toggle flips a boolean, not the int. Likely `slotType = 1` was an earlier per-day or per-week mode that was deprecated. Strict parity: support only `slotType = 2` (date range) and the day-of-week+month mode. The `slotType` field can stay as an int discriminator with values 1 (deprecated; do not surface in NEW UI) and 2 (date range).

## Per-day slot subdivision (inside both modes)

Per `DoctorsAvailabilityDomain.GenerateDoctorsAvailabilityByDays`:

```
appointmentDurationTime = SystemParameters.AppointmentDurationTime  // minutes

for each day in resolved date range:
    duration = ToTime - FromTime                  // TimeSpan
    totalMinutes = duration.TotalHours * 60
    numberOfSlots = Math.Floor(totalMinutes / appointmentDurationTime)

    if totalMinutes >= appointmentDurationTime:
        for j in 0 .. numberOfSlots - 1:
            slot.AvailableDate = day
            slot.LocationId    = input.LocationId
            slot.AppointmentTypeId = input.AppointmentTypeId
            if j == 0:
                slot.FromTime = input.FromTime
                slot.ToTime   = input.FromTime + appointmentDurationTime
            else:
                slot.FromTime = previousSlot.ToTime
                slot.ToTime   = slot.FromTime + appointmentDurationTime
            generated.Add(slot)
```

Notes:

- **Trailing partial slot is dropped.** If FromTime=09:00, ToTime=12:30, duration=60, you get 3 slots (09-10, 10-11, 11-12); the 12:00-12:30 leftover is discarded by `Math.Floor`.
- **Zero or negative duration** results in zero slots; no error -- silent no-op.
- **Per-slot back-to-back.** No gap between slots. If a coffee break is needed between slots, the supervisor must run two separate generations.
- **All generated slots inherit the same AppointmentTypeId** -- this means a generation run is single-type. To make a slot accept any type, the supervisor would need to pass `AppointmentTypeId = null`; the form does not currently expose that option (the form requires `AppointmentTypeId` per the model annotation).

## The two-step UX (preview-then-confirm)

The form does not save slots directly on submit. Instead:

1. Supervisor fills the form and clicks **Generate** -> calls `manageDoctorAvailabilities()` which POSTs to the backend.
2. Backend runs the generation logic + the validation pass (overlap / duplicate / booked-conflict checks) and returns a list of `DoctorsAvailabilitySlotsViewModel` -- grouped by date, with per-slot rows.
3. UI displays the generated slots in a table; supervisor can:
   - Delete an individual time slot via `deleteTimeSlot(timeId)`
   - Delete an entire day via `deleteMonthSlot(monthId)`
4. If all looks good, supervisor clicks **Submit** -> `submitData()` flattens the curated list and POSTs again. The backend's `Add(DoctorsAvailability[])` method writes them all in one Commit.

Key implication for NEW: the AppService for generation must support a "preview" mode (returning the proposed list without writing) and a "save" mode that writes the curated list. Two endpoints, OR one endpoint with a `Save: bool` flag.

## Validation -- complete rule set

### CommonValidation (runs before generation)

Per `DoctorsAvailabilityDomain.CommonValidation` lines 179-222:

1. `TimeSpan.Compare(FromTime, ToTime) == 1 || == 0` -> reject with `ValidationFailedCode.FromTime` ("Please select proper from time"). I.e., FromTime must be strictly less than ToTime.
2. `ToDate < FromDate` -> reject with `ValidationFailedCode.SelectValidDate`.
3. `FromDate <= today OR ToDate <= today` AND `FromTime < currentTime OR ToTime < currentTime` -> reject with `"Please select 'From Date'/'To Date' grater than today's date & time"` (typo "grater" preserved in OLD).

The lead-time check (require new slots to be at least `SystemParameters.AppointmentLeadTime` days out) is **commented out** in OLD. Strict parity: do not enforce it on the supervisor side -- only on the patient booking side.

### Generated-slot validation (runs after generation, before preview)

Per `GenerateDoctorsAvailabilityByDays` lines 400-446 and the `Add` flow:

For each generated slot, four conflict checks against existing rows where `StatusId != Delete`:

1. **Contained-in overlap (any location, available):** an existing `Available` slot whose `FromTime > new.FromTime AND ToTime < new.ToTime` -- existing slot is fully inside the new one. Cross-location check (the where-clause does NOT scope by LocationId in the Generate path -- comparing line 408 vs line 59, a noteworthy inconsistency). Strict parity: replicate as-is.
2. **Exact duplicate (any location, available):** existing `Available` slot with same FromTime + ToTime + AvailableDate -> reject.
3. `TimeSlotValidation(timeSlot)`: at the SAME location and date, any existing slot whose time range overlaps the new slot (FromTime falls within or ToTime falls within) -> reject. This is the location-scoped overlap check.
4. **Conflict with Booked/Reserved (any location):** existing slot with same FromTime + ToTime + AvailableDate AND `BookingStatusId in (Booked, Reserved)` -> reject with "TimeSlot Booked".

If any fail: the response payload's `SameTimeValidation` field is set to a friendly message and the supervisor sees the conflict on the preview screen, not as a blocking error -- the preview still renders the proposed slots, but with the warning. Strict parity: keep the "show in preview, do not block" UX. The supervisor decides whether to remove the conflicting slots or skip submission.

### UpdateValidation (single slot, post-save edit)

Per lines 111-132:

1. Existing slot at same date with `FromTime > new.FromTime AND ToTime < new.ToTime` (other than the one being updated) -> reject (contained-in).
2. Existing slot at same date with same FromTime + ToTime (other than self) -> reject (duplicate).
3. The slot being updated must currently be `Available` -> reject if Booked/Reserved with "you can not be change this doctor availbity beacuse this is booked by user" (typo preserved).

### DeleteValidation

Per lines 141-157:

- Bulk-delete path (`id == 0` + `dates` + `locationId`): reject if any slot at that date+location is `Booked` or `Reserved`.
- Single-delete path: reject if any `vAppointment` or `vAppointmentChangeRequestRecord` row references the slot's `DoctorAvailabilityId` -- this catches slots that are referenced by historical or in-flight appointments even if their booking status was somehow reset.

### Delete action

- Both paths: soft-delete via `StatusId = Status.Delete`. Hard delete is never used -- preserves referential integrity for historical appointments.

## Cross-feature implications

- **Booking validation reads slots filtered to `BookingStatusId == Available`.** Slots in Reserved (pending external booking) or Booked (approved appointment) are excluded from the patient-facing slot picker.
- **Reschedule submission** sets the new slot to `Reserved` immediately on submit; reschedule-approve promotes it to `Booked`; reschedule-reject reverts to `Available`.
- **Cancellation-approve** releases the slot from `Booked` to `Available`.
- **Slot's `AppointmentTypeId` filter** is loose: if slot has a non-null AppointmentTypeId, only that type can book it. If null, any type can. Strict parity: preserve this dual mode.

## NEW-implementation checklist (cross-link `staff-supervisor-doctor-management.md`)

When implementing slot generation in NEW, the audit's gap table requires (verbatim from existing audit):

- `IDoctorAvailabilityManager.GenerateSlotsAsync(GenerateSlotsDto { Mode, DateRange | MonthAndDayOfWeek, FromTime, ToTime, LocationId, AppointmentTypeId? })` with preview-or-save flag.
- `BookingStatus` enum extended with `Reserved` value; state-machine-style transitions on slot.
- The 4 conflict checks replicated as repository-level queries.
- Preview UX with per-slot delete (single + bulk-by-day).
- AppointmentDurationTime read from SystemParameters at generation time (do not cache; allow IT Admin to change duration and have new generations pick up the new value).
- All validation messages localized; preserve OLD copy verbatim except typos -- typo fixes ARE allowed per the strict-parity directive's "OLD bug, fixed for correctness" exception. Specifically:
  - `"you can not be change this doctor availbity beacuse this is booked by user"` -> `"You cannot change this doctor availability because it is booked."`
  - `"grater than today's date & time"` -> `"greater than today's date and time."`

## Open items (to verify in NEW impl)

1. Does NEW's existing `DoctorAvailability` entity have AppointmentTypeId nullable, or is it required? (Audit listed it as required; verify against OLD's nullable.)
2. Does NEW's slot-picker filter by both LocationId AND optional AppointmentTypeId per OLD's loose-or-strict pattern?
3. Whether the `slotType = 1` historical mode has any residual references that need cleanup. Recommend: remove from NEW entirely; document as intentional drop.
