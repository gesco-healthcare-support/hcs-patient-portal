---
status: draft
issue: slot-rework-phase-3-generation-api
owner: AdrianG
created: 2026-05-15
revised: 2026-05-20 (drift check + locked decisions baked in -- see
  `_2026-05-20-slot-phase-3-readiness-check.md`)
approach: tdd (generation math is pure functions on input shape;
  TDD pays the most here) + code (the persistence wave and
  preview projection are orchestration)
sequence: 4 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-slot-rework-phase-2-domain-logic.md
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. Do not merge to `main` until plans
  2 through 7 are merged together.
decisions-locked-2026-05-20:
  Q1 (route URLs): keep existing `/preview` endpoint (rename
    parameter type only); add new sibling `/create-range`. No
    URL breakage; descriptive verb-noun naming.
  Q2 (generation cap): 5,000 slots per call. Covers a full year
    of dense scheduling; well within SQL Server transaction norms.
  Q3 (error messages): use `L["Key"]` translation system, NOT
    hardcoded English. Matches every other validation error in
    the codebase.
---

# Slot rework Phase 3: multi-type / multi-weekday / multi-range
# generation API

## Goal

Replace the existing single-day / single-time-range / single-type
generation input shape with a multi-axis input that supports:

- A list of `AppointmentTypeIds` per generation (zero-or-more
  types per generated slot).
- A list of `SelectedDays : int[]` representing weekday indices
  (0=Sunday through 6=Saturday) -- each selected weekday in the
  date range produces a calendar date.
- A list of `TimeRanges : List<TimeRangeDto>` -- each range with
  its own `FromTime`, `ToTime`, optional per-range
  `AppointmentDurationMinutes` (otherwise inherits the input-
  level default).
- A single `Capacity : int` per generation (applied to every
  generated slot).

This applies to BOTH the preview (`GeneratePreviewAsync`) and
the persistence wave (currently the SPA loops the preview rows
and calls `CreateAsync` one-by-one -- plan 5 swaps to a single
batched `CreateRangeAsync`).

The Phase 1 (schema) + Phase 2 (domain logic) work is the
prerequisite. This plan does not touch the schema. It touches
contracts and the AppService body of
`DoctorAvailabilitiesAppService.GeneratePreviewAsync` and adds
a sibling `CreateRangeAsync`.

## Why

The slot-generation rework plan
(`W:\patient-portal\main\docs\plans\2026-05-15-slot-generation-rework.md`)
section "Phase 3 -- Generation API" prescribes:

> Generation input gains `Capacity`, `AppointmentTypeIds : List<Guid>`,
> `SelectedDays : List<int>`, `TimeRanges : List<TimeRangeDto>`.
> Returns a richer preview where each row carries the new
> Capacity + AppointmentTypes set so the SPA renders the columns.

OLD's generation flow is the closest reference. OLD's
`spm.spDoctorsAvailabilities` stored proc takes a single
contiguous time block per call and the SPA loops the proc per
range. NEW improves the ergonomics by accepting the multi-axis
shape in a single API call, then expanding it server-side into
per-day per-range slots.

The conflict detection from plan 1 stays. The new persistence
wave (`CreateRangeAsync`) batches the writes in a single UoW so
either every non-conflicted slot gets inserted or none of them
do -- avoiding the partial-failure UX where the SPA loops 50
creates and the 25th fails.

## Non-goals

- No UI work (plan 5).
- No change to the existing `CreateAsync` single-slot path; it
  stays for the detail-modal edit flow. `CreateRangeAsync` is
  additive.
- No support for cross-tenant generation. Tenant scope stays
  ambient via ABP's filter.
- No support for "all weekdays + skip blackout dates" -- OLD
  has no equivalent and the rework plan doesn't ask for it.
- No support for generating slots for past dates -- the existing
  AppService validators reject this and we keep that.

## Decisions locked

1. **Input shape is one `DoctorAvailabilityGenerateInputDto` per
   call**, not a list. The OLD list shape was used to support
   batch "create 5 distinct generations" UX which the new UI
   does not need (the multi-axis shape covers it). The interface
   signature changes from
   `Task<List<...>> GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto>)`
   to
   `Task<List<...>> GeneratePreviewAsync(DoctorAvailabilityGenerateInputDto)`.
   Plan 5 reflects this.

2. **`SelectedDays` is `List<int>` with values 0..6**. The
   server validates membership and rejects out-of-range or
   duplicates with an `UserFriendlyException`.

3. **`TimeRanges` is `List<TimeRangeDto>` with at least one
   entry**. Each range carries optional
   `AppointmentDurationMinutes` (overrides the input-level
   default). Validation:
   - Within a single range, `FromTime < ToTime`.
   - Across ranges within the same generation, the ranges MUST
     NOT overlap. Overlap throws `UserFriendlyException` --
     ambiguous which range owns the contested slot.
   - Per-range duration > 0.

4. **`AppointmentTypeIds` is `List<Guid>`, empty list
   permitted**. Empty list means "loose mode -- slot accepts any
   type", per plan 1's repurpose.

5. **`Capacity` is `int >= 1`, defaults to 1**. Same as plan 1.

6. **Generation determinism**: given the same input and the
   same current data, two calls produce the same preview. The
   preview row ordering is stable: by `AvailableDate ASC`, then
   by range index ASC, then by `FromTime ASC` within each range.

7. **`CreateRangeAsync` accepts the SAME input shape as
   `GeneratePreviewAsync`**. The AppService re-runs the
   preview logic server-side rather than trusting client-
   serialised preview rows (the round-trip can stale; another
   user can create a conflicting slot in between). The
   AppService persists only the non-conflicted slots and
   returns a `CreateRangeResultDto` summarising
   `{ insertedCount, skippedConflictCount, conflictedSlots }`.

8. **The persistence wave is transactional**. A single
   `IUnitOfWorkManager.Begin(requiresNew: true, isTransactional:
   true)` wraps the lot. Any insert failure rolls back every
   inserted row in the same call.

9. **Per-range duration overrides** are an explicit ergonomic
   choice. Common case: 60-minute morning ranges with 30-minute
   afternoon ranges in the same generation. Without
   per-range override, the user would have to submit two
   separate generations.

10. **Overlap detection still runs on the preview**. Plan 1
    preserves the "scoped to same location" overlap rule. The
    multi-range generation does NOT need an additional in-batch
    overlap check because decision 3 already forbids overlapping
    ranges; the preview's `IsConflict` flag still reflects
    overlap with EXISTING (persisted) slots only.

## Files touched

### 1. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs`

Rewrite to the new shape:

```csharp
using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- multi-axis slot generation input. Replaces the
/// pre-rework single-date / single-range / single-type shape.
///
/// Semantics: for every calendar date in [FromDate, ToDate] that
/// matches one of <see cref="SelectedDays"/> (Sunday=0 ...
/// Saturday=6), for every <see cref="TimeRanges"/> entry,
/// produce one slot per per-range duration block. Each generated
/// slot inherits the input-level Capacity and AppointmentTypeIds.
/// </summary>
public class DoctorAvailabilityGenerateInputDto
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    /// <summary>
    /// Weekday indices to include (0=Sunday, 6=Saturday). Empty
    /// or null treated as "all weekdays". Duplicates and
    /// out-of-range entries rejected with UserFriendlyException.
    /// </summary>
    public List<int>? SelectedDays { get; set; }

    /// <summary>
    /// At least one time range. Ranges within the same input
    /// MUST NOT overlap. Each range's duration (per-range
    /// override or the input-level <see cref="AppointmentDurationMinutes"/>)
    /// must be &gt; 0 and the range must satisfy
    /// FromTime &lt; ToTime.
    /// </summary>
    [MinLength(1)]
    public List<TimeRangeDto> TimeRanges { get; set; } = new();

    public BookingStatus BookingStatusId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// Permitted appointment types for every generated slot.
    /// Empty list = "any type accepted" (loose mode).
    /// </summary>
    public List<Guid> AppointmentTypeIds { get; set; } = new();

    /// <summary>
    /// Input-level default duration. Each TimeRange may override.
    /// Must be &gt; 0.
    /// </summary>
    public int AppointmentDurationMinutes { get; set; } = 15;

    /// <summary>
    /// Max simultaneous appointments per generated slot. >= 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; } = 1;
}

public class TimeRangeDto
{
    public TimeOnly FromTime { get; set; }
    public TimeOnly ToTime { get; set; }

    /// <summary>
    /// Optional override; falls back to the input-level
    /// AppointmentDurationMinutes when null.
    /// </summary>
    public int? AppointmentDurationMinutes { get; set; }
}
```

### 2. `IDoctorAvailabilitiesAppService.cs`

Change the `GeneratePreviewAsync` signature; add `CreateRangeAsync`:

```csharp
Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
    DoctorAvailabilityGenerateInputDto input);

/// <summary>
/// 2026-05-15 -- persists every non-conflicted slot from the
/// preview projection of the supplied input. Transactional --
/// all-or-nothing for the inserts; conflicted slots are
/// silently skipped (the count is returned so the SPA can show
/// "N inserted, K skipped" feedback).
/// </summary>
Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(
    DoctorAvailabilityGenerateInputDto input);
```

NEW file `DoctorAvailabilityCreateRangeResultDto.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class DoctorAvailabilityCreateRangeResultDto
{
    public int InsertedCount { get; set; }
    public int SkippedConflictCount { get; set; }
    public List<DoctorAvailabilitySlotPreviewDto> ConflictedSlots { get; set; } = new();
}
```

### 3. `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs`

Two changes:

#### 3a. Rewrite `GeneratePreviewAsync`

```csharp
[Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Default)]
public virtual async Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
    DoctorAvailabilityGenerateInputDto input)
{
    ValidateGenerationInput(input);

    var generatedSlots = ExpandToSlotPreviews(input);
    if (generatedSlots.Count == 0)
    {
        return new List<DoctorAvailabilitySlotsPreviewDto>();
    }

    // Pull existing slots for the location across the date span
    // so we can flag conflicts.
    var minDate = generatedSlots.Min(s => s.AvailableDate).Date;
    var maxDate = generatedSlots.Max(s => s.AvailableDate).Date;
    var existingQuery = (await _doctorAvailabilityRepository
            .WithDetailsAsync(x => x.AppointmentTypes))
        .Where(x =>
            x.LocationId == input.LocationId &&
            x.AvailableDate >= minDate &&
            x.AvailableDate <= maxDate);
    var existing = await AsyncExecuter.ToListAsync(existingQuery);

    var location = await _locationRepository.FindAsync(input.LocationId);
    var groupedByDate = generatedSlots
        .GroupBy(s => s.AvailableDate.Date)
        .OrderBy(g => g.Key)
        .ToList();

    var previewList = new List<DoctorAvailabilitySlotsPreviewDto>();
    var monthIndex = 1;
    foreach (var group in groupedByDate)
    {
        var viewModel = new DoctorAvailabilitySlotsPreviewDto
        {
            Dates = group.Key.ToString("MM-dd-yyyy"),
            Days = group.Key.ToString("dddd"),
            MonthId = monthIndex,
            LocationName = location?.Name,
            Time = string.Empty,  // multi-range generations no longer carry a single time string
            DoctorAvailabilities = new List<DoctorAvailabilitySlotPreviewDto>(),
        };

        var timeId = 1;
        foreach (var slot in group.OrderBy(x => x.FromTime))
        {
            slot.TimeId = timeId++;
            viewModel.DoctorAvailabilities.Add(slot);
        }
        previewList.Add(viewModel);
        monthIndex++;
    }

    // Flag conflicts. Same-location overlap with any existing slot.
    foreach (var date in previewList)
    {
        foreach (var slot in date.DoctorAvailabilities)
        {
            var overlap = existing.FirstOrDefault(x =>
                x.AvailableDate.Date == slot.AvailableDate.Date &&
                x.FromTime < slot.ToTime &&
                x.ToTime > slot.FromTime);

            if (overlap == null) continue;

            slot.IsConflict = true;
            if (overlap.BookingStatusId == BookingStatus.Reserved)
            {
                date.SameTimeValidation =
                    "Time slot overlaps a closed slot at this location.";
            }
            else
            {
                date.SameTimeValidation =
                    "Time slot already exists at this location.";
            }
        }
    }

    return previewList;
}

private void ValidateGenerationInput(DoctorAvailabilityGenerateInputDto input)
{
    // (2026-05-20 from readiness-check Q3: validation messages use the
    // L["..."] translation system; matches every other validation error
    // in the codebase. Required localization keys are listed below.)
    Check.NotNull(input, nameof(input));
    if (input.LocationId == Guid.Empty)
    {
        throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
    }
    if (input.AppointmentDurationMinutes <= 0)
    {
        throw new UserFriendlyException(L["DoctorAvailability:DurationMustBeGreaterThanZero"]);
    }
    if (input.Capacity < 1)
    {
        throw new UserFriendlyException(L["DoctorAvailability:CapacityMustBeAtLeastOne"]);
    }
    if (input.ToDate.Date < input.FromDate.Date)
    {
        throw new UserFriendlyException(L["DoctorAvailability:ToDateBeforeFromDate"]);
    }
    if (input.FromDate.Date < DateTime.Today)
    {
        throw new UserFriendlyException(L["DoctorAvailability:CannotGenerateForPastDates"]);
    }
    if (input.TimeRanges == null || input.TimeRanges.Count == 0)
    {
        throw new UserFriendlyException(L["DoctorAvailability:AtLeastOneTimeRangeRequired"]);
    }

    // Per-range validation.
    foreach (var range in input.TimeRanges)
    {
        if (range.ToTime <= range.FromTime)
        {
            throw new UserFriendlyException(
                L["DoctorAvailability:TimeRangeFromMustBeBeforeTo",
                  range.FromTime, range.ToTime]);
        }
        var duration = range.AppointmentDurationMinutes ?? input.AppointmentDurationMinutes;
        if (duration <= 0)
        {
            throw new UserFriendlyException(
                L["DoctorAvailability:TimeRangeDurationMustBePositive",
                  range.FromTime, range.ToTime]);
        }
    }

    // Cross-range overlap.
    var sortedRanges = input.TimeRanges
        .OrderBy(r => r.FromTime)
        .ToList();
    for (var i = 1; i < sortedRanges.Count; i++)
    {
        if (sortedRanges[i].FromTime < sortedRanges[i - 1].ToTime)
        {
            throw new UserFriendlyException(
                L["DoctorAvailability:TimeRangesOverlap",
                  sortedRanges[i - 1].FromTime, sortedRanges[i - 1].ToTime,
                  sortedRanges[i].FromTime, sortedRanges[i].ToTime]);
        }
    }

    // SelectedDays validation -- null/empty treated as "all 7".
    if (input.SelectedDays != null && input.SelectedDays.Count > 0)
    {
        if (input.SelectedDays.Any(d => d < 0 || d > 6))
        {
            throw new UserFriendlyException(L["DoctorAvailability:SelectedDayOutOfRange"]);
        }
        if (input.SelectedDays.Distinct().Count() != input.SelectedDays.Count)
        {
            throw new UserFriendlyException(L["DoctorAvailability:SelectedDaysDuplicate"]);
        }
    }
}

private static List<DoctorAvailabilitySlotPreviewDto> ExpandToSlotPreviews(
    DoctorAvailabilityGenerateInputDto input)
{
    var allowedDays = (input.SelectedDays == null || input.SelectedDays.Count == 0)
        ? new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 }
        : new HashSet<int>(input.SelectedDays);

    var slots = new List<DoctorAvailabilitySlotPreviewDto>();
    var currentDate = input.FromDate.Date;
    var endDate = input.ToDate.Date;

    while (currentDate <= endDate)
    {
        if (allowedDays.Contains((int)currentDate.DayOfWeek))
        {
            foreach (var range in input.TimeRanges.OrderBy(r => r.FromTime))
            {
                var duration = range.AppointmentDurationMinutes
                    ?? input.AppointmentDurationMinutes;
                var currentTime = range.FromTime;

                while (currentTime.AddMinutes(duration) <= range.ToTime)
                {
                    var toTime = currentTime.AddMinutes(duration);
                    slots.Add(new DoctorAvailabilitySlotPreviewDto
                    {
                        AppointmentTypeIds = new List<Guid>(input.AppointmentTypeIds),
                        AvailableDate = currentDate,
                        BookingStatusId = input.BookingStatusId,
                        LocationId = input.LocationId,
                        FromTime = currentTime,
                        ToTime = toTime,
                        Capacity = input.Capacity,
                        IsConflict = false,
                    });
                    currentTime = toTime;
                }
            }
        }
        currentDate = currentDate.AddDays(1);
    }

    return slots;
}
```

#### 3b. Add `CreateRangeAsync`

(2026-05-20 from readiness-check D2.) Add `IUnitOfWorkManager`
to the constructor + readonly field; add `using Volo.Abp.Uow;`
to the file's import block:

```csharp
// Class field, alongside the existing 7 injected fields:
protected IUnitOfWorkManager _unitOfWorkManager;

// Constructor parameter (last):
IUnitOfWorkManager unitOfWorkManager)  // added 2026-05-20 for CreateRangeAsync transaction

// Constructor body:
_unitOfWorkManager = unitOfWorkManager;
```

The transaction-wrapped method body:

```csharp
[Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Create)]
public virtual async Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(
    DoctorAvailabilityGenerateInputDto input)
{
    var preview = await GeneratePreviewAsync(input);
    var slotsToInsert = preview
        .SelectMany(day => day.DoctorAvailabilities)
        .Where(slot => !slot.IsConflict)
        .ToList();

    var result = new DoctorAvailabilityCreateRangeResultDto
    {
        InsertedCount = 0,
        SkippedConflictCount = preview
            .SelectMany(d => d.DoctorAvailabilities)
            .Count(s => s.IsConflict),
        ConflictedSlots = preview
            .SelectMany(d => d.DoctorAvailabilities)
            .Where(s => s.IsConflict)
            .ToList(),
    };

    if (slotsToInsert.Count == 0)
    {
        return result;
    }

    using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
    {
        foreach (var slot in slotsToInsert)
        {
            await _doctorAvailabilityManager.CreateAsync(
                slot.LocationId,
                slot.AppointmentTypeIds,
                slot.AvailableDate,
                slot.FromTime,
                slot.ToTime,
                slot.BookingStatusId,
                slot.Capacity);
            result.InsertedCount++;
        }
        await uow.CompleteAsync();
    }

    return result;
}
```

### 4. `DoctorAvailabilitySlotPreviewDto.cs` and `DoctorAvailabilitySlotsPreviewDto.cs`

Plan 1 already added `AppointmentTypeIds` and `Capacity` to
`DoctorAvailabilitySlotPreviewDto`. No further changes here.

Update `DoctorAvailabilitySlotsPreviewDto` to drop the `Time`
property (or leave it as legacy `string.Empty` -- the multi-
range generation doesn't have a single time string anymore). The
SPA stops rendering the "Time" column on the per-day row; plan
5 confirms.

### 5. Manual controller

`src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs`

(2026-05-20 from readiness-check D1.) Keep the existing
`preview` route URL -- only its parameter type changes (was
`List<DoctorAvailabilityGenerateInputDto>`, now a single DTO).
Add `create-range` as a NEW sibling route. Keeping `preview`
avoids URL churn for Swagger docs and any saved API tests.

Update the existing route at line 95-100 from list-shape to
single-DTO:

```csharp
[HttpPost]
[Route("preview")]
public virtual Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
    DoctorAvailabilityGenerateInputDto input)
{
    return _doctorAvailabilitiesAppService.GeneratePreviewAsync(input);
}
```

Add the new persistence route immediately after:

```csharp
[HttpPost]
[Route("create-range")]
public virtual Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(
    DoctorAvailabilityGenerateInputDto input)
{
    return _doctorAvailabilitiesAppService.CreateRangeAsync(input);
}
```

Note: this project's existing controllers use the
`[HttpPost]` + `[Route(...)]` two-attribute style rather than
`[HttpPost("...")]`. Match the existing style for consistency.

### 5b. Localization keys (2026-05-20 from readiness-check Q3)

Add these keys to `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`:

```jsonc
"DoctorAvailability:DurationMustBeGreaterThanZero":
  "Appointment duration must be greater than zero.",
"DoctorAvailability:CapacityMustBeAtLeastOne":
  "Capacity must be at least 1.",
"DoctorAvailability:ToDateBeforeFromDate":
  "To date must be greater than or equal to from date.",
"DoctorAvailability:CannotGenerateForPastDates":
  "Cannot generate slots for past dates.",
"DoctorAvailability:AtLeastOneTimeRangeRequired":
  "At least one time range is required.",
"DoctorAvailability:TimeRangeFromMustBeBeforeTo":
  "Time range {0}-{1} must have FromTime < ToTime.",
"DoctorAvailability:TimeRangeDurationMustBePositive":
  "Time range {0}-{1}: duration must be > 0.",
"DoctorAvailability:TimeRangesOverlap":
  "Time ranges overlap: {0}-{1} and {2}-{3}.",
"DoctorAvailability:SelectedDayOutOfRange":
  "SelectedDays must contain values between 0 (Sunday) and 6 (Saturday).",
"DoctorAvailability:SelectedDaysDuplicate":
  "SelectedDays must not contain duplicates.",
"DoctorAvailability:GenerationCountExceedsLimit":
  "This generation would produce more than {0} slots. Split into smaller batches."
```

The `L["The {0} field is required.", L["Location"]]` pattern
already exists in the codebase and reuses the existing
`Location` localization label.

### 6. Auto-proxy regeneration

After build passes:

```bash
cd angular
yarn nswag refresh
```

This rewrites `angular/src/app/proxy/doctor-availabilities/doctor-availability.service.ts`
to expose `generatePreview(input: DoctorAvailabilityGenerateInputDto)`
(single DTO, not array) and the new `createRange(input: ...)`
method. Plan 5 consumes them.

## Test plan

### `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs`

Add 14 facts (TDD: each is a small unit on the pure generation
logic):

**Validation (per ValidateGenerationInput):**

| # | Test | Acceptance |
|---|------|------------|
| 1 | `GeneratePreviewAsync_WhenLocationEmpty_Throws` | Throws on `LocationId = Guid.Empty`. |
| 2 | `GeneratePreviewAsync_WhenDurationNonPositive_Throws` | Throws on `AppointmentDurationMinutes = 0`. |
| 3 | `GeneratePreviewAsync_WhenCapacityZero_Throws` | Throws on `Capacity = 0`. |
| 4 | `GeneratePreviewAsync_WhenFromDatePast_Throws` | Throws when `FromDate` is yesterday. |
| 5 | `GeneratePreviewAsync_WhenNoTimeRanges_Throws` | Throws on empty `TimeRanges`. |
| 6 | `GeneratePreviewAsync_WhenRangeFromGtTo_Throws` | Throws on `FromTime >= ToTime`. |
| 7 | `GeneratePreviewAsync_WhenRangesOverlap_Throws` | Throws on `[08:00-10:00, 09:00-11:00]`. |
| 8 | `GeneratePreviewAsync_WhenSelectedDayOutOfRange_Throws` | Throws on `[9]`. |
| 9 | `GeneratePreviewAsync_WhenSelectedDaysDuplicate_Throws` | Throws on `[1, 1]`. |

**Expansion (per ExpandToSlotPreviews):**

| # | Test | Acceptance |
|---|------|------------|
| 10 | `GeneratePreviewAsync_AllWeekdays_3DayRange_OneRange_60mDuration_Returns9Slots` | 3 days * 3 hours / 60m = 9 slots. |
| 11 | `GeneratePreviewAsync_MondayAndWednesdayOnly_5DayRange_Returns2Days` | Mon + Wed in Mon-Fri = 2 days. |
| 12 | `GeneratePreviewAsync_TwoNonOverlappingRanges_30mAm_60mPm_Sums` | Range [08:00-10:00, 30m] + [13:00-15:00, 60m] = 4 + 2 = 6 slots. |
| 13 | `GeneratePreviewAsync_MultiTypeSet_AppliedToEverySlot` | Input `AppointmentTypeIds = [t1, t2]`. Every generated slot has the same set. |
| 14 | `GeneratePreviewAsync_CapacityAppliedToEverySlot` | Input `Capacity = 3`. Every generated slot has `Capacity = 3`. |

**CreateRangeAsync:**

| # | Test | Acceptance |
|---|------|------------|
| 15 | `CreateRangeAsync_AllNonConflicting_InsertsAllAndReturnsCount` | 5 generated slots, no conflicts. `InsertedCount=5`, `SkippedConflictCount=0`. |
| 16 | `CreateRangeAsync_HalfConflict_InsertsRest` | 4 slots; 2 conflict with pre-existing. `InsertedCount=2`, `SkippedConflictCount=2`. |
| 17 | `CreateRangeAsync_AnyInsertFails_RollsBack` | Force a slot insert to fail (e.g., DB constraint violation via raw SQL pre-seed). No rows inserted. |

### Manual UI verification

After backend ships, the Angular UI still uses the old single-
input list shape (the proxy was regenerated but the form work
is plan 5). To verify the new endpoints from outside the UI:

1. `docker compose up -d --build`.
2. Use Swagger at `https://api.localhost:44327/swagger`.
3. POST `/api/app/doctor-availabilities/generate-preview` with:
   ```json
   {
     "fromDate": "2026-06-01",
     "toDate": "2026-06-07",
     "selectedDays": [1, 3],
     "timeRanges": [
       { "fromTime": "08:00", "toTime": "10:00" },
       { "fromTime": "13:00", "toTime": "15:00", "appointmentDurationMinutes": 30 }
     ],
     "bookingStatusId": 8,
     "locationId": "<seeded-id>",
     "appointmentTypeIds": [],
     "appointmentDurationMinutes": 60,
     "capacity": 2
   }
   ```
   Expect 200 with: 2 dates (Mon Jun 1, Wed Jun 3); each date
   carries 2 + 4 = 6 slots; every slot has `capacity: 2`,
   `appointmentTypeIds: []`.
4. POST `/api/app/doctor-availabilities/create-range` with the
   same body. Expect 200 with
   `{ "insertedCount": 12, "skippedConflictCount": 0, ... }`.
5. SQL probe: `SELECT COUNT(*) FROM AppEntity.DoctorAvailabilities
   WHERE TenantId = '<tenant>' AND AvailableDate BETWEEN
   '2026-06-01' AND '2026-06-07';` returns 12.
6. SQL probe: `SELECT COUNT(*) FROM
   AppEntity.DoctorAvailabilityAppointmentType WHERE
   DoctorAvailabilityId IN (...);` returns 0 (empty list = loose
   mode).

## Risk and rollback

**Blast radius:**
- One AppService method rewritten; one added.
- Interface signature change for `GeneratePreviewAsync` --
  caller (today only the Angular proxy) must regenerate. The
  proxy regenerates as part of this plan.
- No schema delta.

**Rollback:**
- Revert the commit. The Angular UI is unaware (plans 4/5
  haven't shipped). The proxy regeneration backs out via the
  revert. No DB migration to roll back.

**Risk: interface change breaks any non-Angular consumer.**
Mitigated by: the project has only the Angular client; the
`HttpApi` controller is the only public surface. A grep of
`generatePreview` outside `angular/src/app/proxy/` confirms no
direct callers.

**Risk: `CreateRangeAsync` transaction holds a long lock under
high-row counts.** Mitigated by limiting input -- the
AppService should reject inputs that would generate more than
N slots. (2026-05-20 from readiness-check Q2: N = 5,000.
Covers a full year of dense scheduling; well within SQL Server
transaction norms.) Add to `ValidateGenerationInput`:

```csharp
var expected = EstimateSlotCount(input);
if (expected > 5000)
{
    throw new UserFriendlyException(
        L["DoctorAvailability:GenerationCountExceedsLimit", 5000]);
}
```

Add the localization key to `Domain.Shared/Localization/CaseEvaluation/en.json`:

```jsonc
"DoctorAvailability:GenerationCountExceedsLimit":
  "This generation would produce more than {0} slots. Split into smaller batches."
```

(2026-05-20 from readiness-check D4: the original plan body's
formula `range.duration / range.duration` was incorrect -- it
always evaluated to 1. Corrected helper below counts allowed
calendar days * sum-over-ranges-of (range-minutes / slot-duration).)

```csharp
private static int EstimateSlotCount(DoctorAvailabilityGenerateInputDto input)
{
    if (input.TimeRanges == null || input.TimeRanges.Count == 0)
    {
        return 0;
    }
    var dayCount = 0;
    for (var day = input.FromDate.Date; day <= input.ToDate.Date; day = day.AddDays(1))
    {
        if (input.SelectedDays == null
            || input.SelectedDays.Count == 0
            || input.SelectedDays.Contains((int)day.DayOfWeek))
        {
            dayCount++;
        }
    }
    var slotsPerDay = input.TimeRanges.Sum(range =>
    {
        var duration = range.AppointmentDurationMinutes ?? input.AppointmentDurationMinutes;
        if (duration <= 0)
        {
            return 0;
        }
        var minutes = (range.ToTime - range.FromTime).TotalMinutes;
        return (int)Math.Floor(minutes / duration);
    });
    return dayCount * slotsPerDay;
}
```

The check runs BEFORE expansion so we never allocate a
large preview list to discover it's too big.

**Risk: race between preview and create-range.** Two admins
generating slots for the same date may both see "no conflict"
in their previews then both call `CreateRangeAsync`. The
second to commit succeeds anyway because the slot rows are
distinct GUIDs -- the only collision is "two overlapping
slots exist". The SPA flow today already accepts overlapping
admin-generated slots if no preview-time conflict was visible;
plan 5 doesn't change that. The risk is purely UX (two
overlapping slots in the calendar); the booking gate handles
the rest.

## Verification

End-to-end procedure:

1. `docker compose down -v && docker compose up -d --build`.
2. Confirm no new migration ran (this plan has no schema delta).
3. Run Swagger interactions above (steps 3-6 in manual section).
4. Run the 17 new unit tests; all green.
5. Run the full test suite to confirm no regression elsewhere:
   `dotnet test --filter Category!=Slow`.

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Plan 5 (Angular generation UI) is the immediate follow-up.
