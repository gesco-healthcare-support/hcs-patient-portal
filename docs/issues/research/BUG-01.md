[Home](../../INDEX.md) > [Issues](../) > Research > BUG-01

# BUG-01: Slot Conflict Detection Logic Inverted -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` lines 265-294

---

## Current state (verified 2026-04-17)

```csharp
// Line 269-272: find overlapping existing slot
var overlap = existingAvailabilities.FirstOrDefault(x =>
    x.AvailableDate.Date == timeSlot.AvailableDate.Date &&
    x.FromTime < timeSlot.ToTime &&
    x.ToTime > timeSlot.FromTime);

if (overlap == null) continue;

var isSameLocation = overlap.LocationId == timeSlot.LocationId;

// Line 281 -- BUG:
if (isSameLocation || overlap.BookingStatusId == BookingStatus.Available)
{
    timeSlot.IsConflict = true;
    isAlreadyExist = true;
}

// Line 287: booked/reserved flag
if (overlap.BookingStatusId == BookingStatus.Booked ||
    overlap.BookingStatusId == BookingStatus.Reserved)
{
    timeSlot.IsConflict = true;
    isBookedByUser = true;
}
```

Facts:

- Line 281 flags `isAlreadyExist = true` on `isSameLocation || overlap.BookingStatusId == Available`.
- Practical effect: an overlapping slot that is Available at a *different* location is flagged as conflict -- blocks legitimate multi-location scheduling.
- Line 287 separately handles Booked/Reserved overlaps (`isBookedByUser = true`) -- correct logic for "same doctor cannot be in two places".
- The existing bug doc suggests the intent was `isSameLocation || overlap.BookingStatusId != BookingStatus.Available` -- "flag if same location at all, or if the overlap is booked anywhere".
- Overlap detection formula itself (`FromTime < ToTime && ToTime > FromTime`) is correct per interval math.

---

## Official documentation

- [ABP Testing -- Integration tests with xUnit and Shouldly](https://abp.io/docs/latest/framework/testing/integration-tests) -- the project already uses this pattern; regression tests for the fixed logic should live in `test/HealthcareSupport.CaseEvaluation.Application.Tests`.
- [Microsoft -- Domain events design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) -- clean separation of business rules into testable methods.

## Community findings

- [GeeksforGeeks -- Find all conflicting appointments](https://www.geeksforgeeks.org/dsa/given-n-appointments-find-conflicting-appointments/) -- canonical interval-overlap formula: two intervals `(s1, e1)` and `(s2, e2)` overlap iff `s1 < e2 && s2 < e1`. Current code uses strict `<` / `>` -- back-to-back slots (10:00-10:30 and 10:30-11:00) correctly do NOT overlap.
- [Wikipedia -- Interval scheduling](https://en.wikipedia.org/wiki/Interval_scheduling) -- algorithm taxonomy for larger scale (sort-by-end, sweep line).
- [Shyft -- Overlapping appointment prevention](https://www.myshyft.com/blog/overlapping-appointment-prevention/) -- industry practice: "a single doctor cannot be physically in two locations at once; any same-doctor time overlap is a physical conflict regardless of location."
- [AlgoMaster -- Non-overlapping intervals](https://algomaster.io/learn/dsa/non-overlapping-intervals) -- sort + sweep recipe if brute-force scales poorly.

## Recommended approach

Split the conflict predicate into two orthogonal guards matching the two calendars the code is reasoning about:

1. **Doctor calendar**: any overlap where the existing slot is Booked or Reserved -- the doctor is physically unavailable. Current line 287 handles this correctly.
2. **Location calendar**: any overlap at the **same location** -- the location is double-booked. Current line 281 handles this via `isSameLocation` but adds a spurious OR branch.

Corrected predicate matching the existing bug doc: `if (isSameLocation || overlap.BookingStatusId != BookingStatus.Available) { ... }`. Or cleaner: split into two separate `if` blocks:

```csharp
if (isSameLocation) { timeSlot.IsConflict = true; isAlreadyExist = true; }
if (overlap.BookingStatusId != BookingStatus.Available) { timeSlot.IsConflict = true; isBookedByUser = true; }
```

Add regression tests covering the matrix: (same-location / different-location) x (Available / Booked / Reserved / other) x (overlap / no-overlap).

## Gotchas / blockers

- Enumerate all values of `BookingStatus` before fixing -- negating `== Available` assumes Available is the only "free" state. If there's a `Reserved` or `Blocked` that should also be treated as free, the boolean changes.
- Time-zone handling: `DoctorAvailability.FromTime`/`ToTime` storage semantics (UTC vs local). IME practice is local time at exam location; confirm with business.
- Existing callers depend on `timeSlot.IsConflict` / `isAlreadyExist` / `isBookedByUser` flags; Angular `doctor-availabilities` feature module may display different messages per flag. Check the UI before changing semantics.
- **No existing test coverage** for `DoctorAvailabilitiesAppService` (per repo CLAUDE.md -- only Doctors/Books/framework services tested). Behaviour change has no safety net; add tests as part of the fix.

## Open questions

- **Product decision**: can a doctor legitimately have concurrent `Available` slots at multiple locations (offering availability broadly and narrowing down later)? Industry practice says no; the current code implies the business treats `Available` as "offered for booking" rather than "doctor is at this location". Resolve before coding the fix.
- What are ALL the `BookingStatus` values in `Domain.Shared/Enums/BookingStatus.cs`? Need to enumerate before deciding which count as "occupying".
- CA DWC regulation on IME scheduling density per doctor per day -- did not find a definitive public source in research.
- Does the Angular preview UI currently rely on `isAlreadyExist` vs `isBookedByUser` to show different messages? Changing the predicate may silently change the shown text at line 298-307.

## Related

- [DAT-01](DAT-01.md) -- slot-booking race is independent but shares the `BookingStatus` enum
- [docs/issues/BUGS.md#bug-01](../BUGS.md#bug-01-slot-conflict-detection-logic-is-inverted)
- [docs/business-domain/DOCTOR-AVAILABILITY.md](../../business-domain/DOCTOR-AVAILABILITY.md) -- intended rules
