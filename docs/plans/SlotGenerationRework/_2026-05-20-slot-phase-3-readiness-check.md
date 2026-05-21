---
status: resolved
issue: slot-phase-3-readiness-check
owner: AdrianG
created: 2026-05-20
resolved: 2026-05-20 (Q1=keep preview + add create-range, Q2=5000
  cap, Q3=L[...] translation system locked; D1-D6 baked into plan
  body; localization keys section added)
approach: code (research-only; updates plan body in a follow-up)
sequence: drift check for `2026-05-15-slot-rework-phase-3-generation-api.md`
depends-on: _2026-05-20-slot-phase-2-readiness-check.md (Phase 2
  must ship first; this plan's CreateRangeAsync assumes the new
  DoctorAvailabilityManager.CreateAsync signature from Phase 1
  and the capacity-aware booking gate from Phase 2)
---

# Phase 3 (generation API) readiness check

## Goal

Compare the Phase 3 plan against the current source on
`feat/replicate-old-app` and surface drift. This phase rewrites
the multi-axis generation input + adds `CreateRangeAsync`. The
biggest blast radius is in `DoctorAvailabilitiesAppService` and
the manual controller route.

This check does NOT modify the plan body. Once the questions
below are answered, a follow-up edit pass updates the plan.

## What was verified

| Cited location | Status | Notes |
|---|---|---|
| `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs` (23 lines) | MATCHES `from` state | Single-day / single-range / single-type / no-capacity. Plan rewrites entirely. |
| `src/.../Application.Contracts/DoctorAvailabilities/IDoctorAvailabilitiesAppService.cs` line 22 | MATCHES `from` state | `GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto> input)` -- list shape. Plan changes to single DTO + adds `CreateRangeAsync`. |
| `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` `GeneratePreviewAsync` lines 215-366 (~152 lines, cognitive complexity 41) | MATCHES `from` state | The existing list-iteration + per-item validation + nested while-loops + grouped output + conflict-flag logic. Plan rewrites entirely with helper extraction. |
| `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotPreviewDto.cs` | MATCHES `from` state | Single `Guid? AppointmentTypeId`. Phase 1 adds `AppointmentTypeIds` + `Capacity`. Phase 3 uses them. |
| `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotsPreviewDto.cs` | MATCHES `from` state | Has `Time : string` field. Plan's section 4 leaves it as `string.Empty`. |
| `src/.../HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs` line 95-100 | DRIFT | Plan says "Update the existing `generate-preview` route" but actual route is `[Route("preview")]` at line 96, NOT `generate-preview`. See D1. Proxy URL: `/api/app/doctor-availabilities/preview`. |
| `DoctorAvailabilitiesAppService` constructor (lines 35-51) -- 7 injected deps | DRIFT | Plan section 3b uses `_unitOfWorkManager` without showing the constructor injection. Current ctor has 7 deps, NONE of them `IUnitOfWorkManager`. See D2. |
| Proxy `angular/src/app/proxy/doctor-availabilities/doctor-availability.service.ts` line 50-56 | MATCHES `from` state | `generatePreview(input: DoctorAvailabilityGenerateInputDto[], ...)` -- array shape on the URL `/preview`. Regeneration after backend change will rewrite to single DTO + new `createRange` method. |
| Angular `doctor-availability-generate.component.ts` | OUT-OF-SCOPE | Plan 5 owns the SPA changes; Phase 3 only ships backend + proxy regen. The component will break at compile time after proxy regen; plan 5 fixes it. |

## Drift items

### D1 -- Controller route name mismatch (`preview` vs `generate-preview`)

**Plan body (section 5):**

```csharp
[HttpPost("generate-preview")]
public virtual Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
    DoctorAvailabilityGenerateInputDto input)
    => _doctorAvailabilitiesAppService.GeneratePreviewAsync(input);
```

**Reality:** the current controller at line 95-100:

```csharp
[HttpPost]
[Route("preview")]
public virtual Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(
    List<DoctorAvailabilityGenerateInputDto> input)
{
    return _doctorAvailabilitiesAppService.GeneratePreviewAsync(input);
}
```

The route token is `preview`, not `generate-preview`. The
Angular proxy currently posts to `/api/app/doctor-availabilities/preview`
(verified at proxy service line 53).

**Options:**

- **A** -- Keep the existing `preview` route to minimize URL
  changes (rename only the parameter type). Plan's `create-range`
  becomes a new sibling route at `/api/app/doctor-availabilities/range`
  (matches the "preview" / "range" verb-noun symmetry).
- **B** -- Migrate the route to `generate-preview` as plan body
  suggests. URL change. Old route stops working immediately;
  proxy regen masks this in the SPA but breaks any external
  consumer (none in this repo, but a behaviour change in
  Swagger docs and any saved API tests).
- **C** -- Same as A, but use `create-range` instead of `range`
  for the new endpoint. Better readability; slightly longer URL.

**Recommendation:** **C**. Keep `preview` to avoid the URL
churn; use `create-range` for the new endpoint to make the
verb explicit. Update the plan body accordingly.

### D2 -- `IUnitOfWorkManager` is not currently injected

**Plan body section 3b uses `_unitOfWorkManager.Begin(...)`:**

```csharp
using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
```

**Reality:** `DoctorAvailabilitiesAppService`'s constructor
(lines 35-51) does NOT inject `IUnitOfWorkManager`. Grepping
the class for `_unitOfWorkManager` returns nothing.

**Fix:** the plan body must explicitly add the constructor
parameter + readonly field. Mirror the Phase 2 decision Q3
(also locked on "explicit constructor injection with dated
comment"). Add to section 3b:

```csharp
private readonly IUnitOfWorkManager _unitOfWorkManager;

public DoctorAvailabilitiesAppService(
    ...existing 7 deps...,
    IUnitOfWorkManager unitOfWorkManager)  // added 2026-05-20 for CreateRangeAsync transaction
{
    ...existing assignments...
    _unitOfWorkManager = unitOfWorkManager;
}
```

The `using Volo.Abp.Uow;` import needs to be added too.

**Risk:** zero -- the build catches the missing dependency, but
the plan body should be explicit so the reviewer isn't caught
off-guard.

### D3 -- `Time` field default and SPA-visible behaviour

**Plan body section 3a:**

```csharp
Time = string.Empty,  // multi-range generations no longer carry a single time string
```

**Plan body section 4:**

> Update `DoctorAvailabilitySlotsPreviewDto` to drop the `Time`
> property (or leave it as legacy `string.Empty` -- the multi-
> range generation doesn't have a single time string anymore).

**Reality:** `DoctorAvailabilitySlotsPreviewDto.Time` has a
non-null default `string.Empty` initialization. SPA's
`doctor-availability-generate.component.ts` renders `Time` in
the preview header (verified by grep result -- it's the
component that owns this UI). Leaving it as `string.Empty`
renders a blank cell, which is what plan 5's UI redesign wants.

**Decision needed:**

- **A** -- Leave the field on the DTO + populate with
  `string.Empty` (plan's "leave it as legacy" branch).
  Backward compatibility is moot because the SPA is
  regenerated, but the C# property stays for serialization
  symmetry.
- **B** -- Drop the field. Cleaner DTO; plan 5 must redesign
  the preview header anyway.

**Recommendation:** **A**. The DTO change is the smallest
delta; plan 5 just stops binding to `Time` in its template.
Dropping the field forces a Mapperly mapper update and a
proxy regen for no functional gain.

### D4 -- `EstimateSlotCount` helper signature

**Plan body, Risk and Rollback section:**

```csharp
var expected = EstimateSlotCount(input);
if (expected > 1000)
{
    throw new UserFriendlyException(...);
}
```

**Reality:** no such helper exists. Plan body says "pure helper
computing date-count * range-count * (range.duration / range.duration)"
but the formula `range.duration / range.duration` is `1`, which
is wrong. The intent was likely
`sum-over-ranges-of(range-duration / slot-duration)`.

**Fix:** correct the formula in the plan body to:

```csharp
private static int EstimateSlotCount(DoctorAvailabilityGenerateInputDto input)
{
    var allowedDayCount = (input.SelectedDays == null || input.SelectedDays.Count == 0)
        ? 7
        : input.SelectedDays.Distinct().Count();

    var totalDays = Enumerable.Range(0, (input.ToDate.Date - input.FromDate.Date).Days + 1)
        .Count(offset =>
        {
            var day = input.FromDate.Date.AddDays(offset);
            return (input.SelectedDays == null || input.SelectedDays.Count == 0)
                || input.SelectedDays.Contains((int)day.DayOfWeek);
        });

    var slotsPerDay = input.TimeRanges.Sum(range =>
    {
        var duration = range.AppointmentDurationMinutes ?? input.AppointmentDurationMinutes;
        if (duration <= 0) return 0;
        var minutes = (range.ToTime - range.FromTime).TotalMinutes;
        return (int)Math.Floor(minutes / duration);
    });

    return totalDays * slotsPerDay;
}
```

The check runs BEFORE the expansion loop so we never allocate
a 50,000-row list to discover it's too big.

**Risk:** medium -- if the helper miscounts (rounding error),
the guard fires false-positive or false-negative. Tests cover
this; one of the fact tests (#15 or new fact) should pin
"5 days * 4 slots = 20 expected".

### D5 -- `DoctorAvailabilityCreateRangeResultDto` mapper missing

**Plan body section 2** introduces the new DTO but does NOT
mention a Mapperly mapper for it. The DTO has only primitives
(`int InsertedCount`, `int SkippedConflictCount`) and a
`List<DoctorAvailabilitySlotPreviewDto> ConflictedSlots` -- the
nested type's mapper already exists via Phase 1.

**Fix:** no Mapperly mapper is needed because the AppService
constructs the DTO directly in code (verified by reading
section 3b's `result = new DoctorAvailabilityCreateRangeResultDto { ... }`
pattern). The DTO is a return-value-only object.

**No drift; surfaced as positive confirmation.**

### D6 -- Permission for `CreateRangeAsync`

**Plan body section 3b:**

```csharp
[Authorize(CaseEvaluationPermissions.DoctorAvailabilities.Create)]
public virtual async Task<DoctorAvailabilityCreateRangeResultDto> CreateRangeAsync(...)
```

**Reality:** the `CaseEvaluation.DoctorAvailabilities.Create`
permission already exists (per the `DoctorAvailabilities`
feature's permission tree: `.Default`, `.Create`, `.Edit`,
`.Delete`). Plan's decision to gate on `.Create` is
consistent with the existing single-slot `CreateAsync`.

**No drift; surfaced as positive confirmation.**

## Open questions (need decisions)

### Q1 -- Route name (overlap with D1)

See D1's three options. Recommendation **C**: keep `preview`,
add `create-range` as a new sibling.

### Q2 -- Generation cap (1,000 slots)

**Plan body Risk section:** caps generation at 1,000 slots.

**Effect:**

- An admin generating a full year of Monday/Wednesday slots
  with 6 ranges of 30-minute appointments = (52 weeks * 2
  days) * 6 ranges * (varies). With 30-min slots and 4-hour
  ranges = 8 slots per range = 48 slots per day = 4,992 over
  the year. Cap blocks this.
- The OLD app had no such cap.

**Options:**

- **A** -- 1,000 (plan's current text). Forces admins to
  generate in chunks.
- **B** -- 5,000. Accommodates a full year of dense
  scheduling.
- **C** -- Configurable per-tenant via SystemParameter. Adds
  ops surface; deferred.
- **D** -- No cap. Trust the admin; let the transaction
  succeed or time out naturally.

**Recommendation:** **B**. 5,000 covers all realistic
generation patterns; 50,000 is the danger zone. A SQL Server
transaction holding 5,000 inserts is well within OLTP norms.

### Q3 -- Validation localization keys

**Plan body section 3a:** error messages are hardcoded English
strings:

```csharp
throw new UserFriendlyException("Location is required.");
throw new UserFriendlyException("Appointment duration must be greater than zero.");
```

**Reality:** project convention (per CLAUDE.md) is to use
`L["Key"]` for localized messages with keys in
`Domain.Shared/Localization/CaseEvaluation/en.json`.

**Options:**

- **A** -- Use `L["Key", L["Location"]]` pattern matching the
  existing `CreateAsync` validators (see line 185 today).
- **B** -- Hardcoded English (plan's current text). Ship
  faster, fix later.

**Recommendation:** **A**. Matches the existing pattern; the
keys already exist (`The {0} field is required.`, etc.) so
this is a 5-minute swap.

### Q4 -- Multi-tenant ambient scope for `CreateRangeAsync`

**Plan body** asserts "Tenant scope stays ambient via ABP's
filter." Verify by tracing the flow:

1. Controller -> AppService: ambient tenant context via ABP.
2. AppService -> `DoctorAvailabilityManager.CreateAsync`:
   passes through (manager has no manual tenant set).
3. `CreateAsync` -> `_doctorAvailabilityRepository.InsertAsync`:
   the entity's `TenantId` is set by ABP via the
   `IMultiTenant` interface (auto-populated on insert).

**Sanity check:** ABP's `MultiTenancyDataFilter` sets the new
entity's TenantId from `CurrentTenant.Id` during the
`SaveChanges` callback. Plan section 3b's transaction wraps
this; the UoW commit captures the right TenantId.

**No decision needed**; flagged as sanity-check.

### Q5 -- `_appointmentChangeRequestRepository` field is unused in `GeneratePreviewAsync` rewrite

**Reality:** the constructor (line 35-51) injects
`_appointmentChangeRequestRepository`. The new `GeneratePreviewAsync`
+ `CreateRangeAsync` do not use it.

**No drift in this plan**; the field is used elsewhere in the
AppService (e.g., the existing delete paths). Surfaced as a
"don't accidentally drop the dependency" reminder.

## Decisions to lock before implementation

| # | Question | Recommendation |
|---|---|---|
| Q1 / D1 | Route URLs | C -- keep `preview`, add `create-range` |
| Q2 | Generation cap | B -- 5,000 (covers a year of dense scheduling) |
| Q3 | Localization | A -- use `L["Key"]` matching project convention |

Q4 and Q5 need no decisions; sanity checks only.

## Risk re-rating

**Plan body lists three risks** (interface change breaks
consumers, transaction lock under high-row counts, preview /
create-range race). All still apply.

**New risks surfaced by this check:**

- **D1 (low)**: route URL changes if the plan body's
  `generate-preview` form is applied verbatim. Mitigation:
  apply Q1=C (keep `preview`).
- **D2 (low)**: missing `IUnitOfWorkManager` injection breaks
  the build but the plan body is silent on this. Mitigation:
  add explicit injection to the plan body.
- **D4 (medium)**: `EstimateSlotCount` formula in the plan is
  incorrect. Mitigation: replace with the corrected helper in
  the plan body.

## Workflow

This readiness check informs (does not modify) the Phase 3 plan
body. Once decisions Q1, Q2, Q3 are locked and D1-D6 are
agreed, a follow-up edit pass updates
`2026-05-15-slot-rework-phase-3-generation-api.md`. Then the
plan is ready to execute as soon as Phase 2 ships.

## How to apply

1. Read this doc.
2. Decide Q1, Q2, Q3 (recommendations: C, B, A).
3. Edit the Phase 3 plan body to incorporate D1-D6 + locked
   decisions. Specifically:
   - D1: change route from `generate-preview` to `preview`
     (kept) + add `create-range` as new sibling.
   - D2: add `IUnitOfWorkManager` to constructor + field +
     `using Volo.Abp.Uow;` import.
   - D4: replace the `EstimateSlotCount` formula with the
     corrected version above.
4. Mark this readiness check `status: resolved`.
