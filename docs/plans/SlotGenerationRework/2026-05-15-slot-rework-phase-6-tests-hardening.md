---
status: draft
issue: slot-rework-phase-6-tests-hardening
owner: AdrianG
created: 2026-05-15
approach: tdd (HARDENING-TEST-SUITE additions are written
  TEST-FIRST against the now-live behavior; the unit tests are
  small and pure)
sequence: 7 of 7 (slot-generation + doctor-invariant series)
depends-on: 2026-05-15-slot-rework-phase-5-picker-ui.md
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`. After this merges, kick off the
  merge wave for plans 1-7 back into `main`.
---

# Slot rework Phase 6: tests + HARDENING-TEST-SUITE additions

## Goal

Lock the behavior shipped in plans 1-6 with explicit tests:

1. Add the HARDENING test scenarios required by the slot-rework
   plan: `HRD-R1.12.{1..6}` (admin generation) and
   `HRD-R2.10.{1..3}` (booking-time concurrency / capacity).
2. Add unit tests for the new pure helpers introduced in plan
   1 (entity invariants), plan 2 (active-count exclusions), and
   plan 3 (generation expansion math + per-range overlap).
3. Add integration tests that exercise the end-to-end booking
   path through the new capacity gate (parallel callers,
   concurrent reschedule, manual close).
4. Add Playwright MCP scenarios (browser-level) for the picker
   UX changes shipped in plan 6.
5. Document the new tests in a single hardening-suite reference
   doc so future contributors can find them.

## Why

Plans 1-6 ship behavior; this plan ships the proof that the
behavior is locked. The slot-rework plan
(`W:\patient-portal\main\docs\plans\2026-05-15-slot-generation-rework.md`)
section "HARDENING-TEST-SUITE additions" explicitly enumerates
the test IDs:

> - `HRD-R1.12.1` -- generate 14 days, MWF, two ranges, capacity 2
> - `HRD-R1.12.2` -- generate with empty SelectedDays -> all 7
> - `HRD-R1.12.3` -- overlapping ranges rejected
> - `HRD-R1.12.4` -- per-range duration override
> - `HRD-R1.12.5` -- capacity 0 rejected
> - `HRD-R1.12.6` -- > 1000 slot estimate rejected
> - `HRD-R2.10.1` -- two patients race to last seat -> one full
> - `HRD-R2.10.2` -- type-mismatch refresh cascade
> - `HRD-R2.10.3` -- manual-close mid-flight rejects booking

This plan ships those nine scenarios PLUS the unit / integration
tests called out in plans 1-5.

## Non-goals

- No new product behavior. Pure test wiring.
- No new permission gates or schema delta.
- No expansion of HRD coverage beyond what plans 1-5 explicitly
  added; broader admin/permission scenarios stay in their own
  hardening waves.

## Decisions locked

1. **HRD scenarios go in a SINGLE markdown reference doc** at
   `docs/parity/wave-1-parity/_hrd-scenarios-slot-rework.md`
   mirroring the existing
   `_hrd-scenarios-terms-and-conditions.md` shape (created in
   PR #204).

2. **xUnit `[Trait("HRD", "R1.12.1")]` on each test** so we
   can filter via `dotnet test --filter Trait=HRD` and produce
   the per-scenario log.

3. **Each scenario is a single `[Fact]`** (TDD-style, atomic).
   No `[Theory]` with data-table inputs -- the scenarios are
   semantically distinct.

4. **Browser tests run via Playwright MCP** invoked from a
   manual session (the playwright MCP plugin is configured per
   the user's setup). Each scenario logs success or failure
   to a per-run note in `docs/parity/wave-1-parity/_hrd-runs/`.

5. **Concurrency tests use the actual SQL Server container**,
   not the in-process SQLite test DB. The plan 2 row-lock hint
   requires the real engine. Add a marker
   `[Trait("Backend", "SqlServer")]` to gate.

## Files touched

### 1. NEW FILE `docs/parity/wave-1-parity/_hrd-scenarios-slot-rework.md`

Reference doc enumerating each HRD scenario with: ID, goal,
fixtures, observable assertion, status (pending / passing /
failing).

```markdown
# HRD scenarios: slot-generation rework (2026-05-15)

This doc enumerates the nine HARDENING-TEST-SUITE scenarios
introduced by the slot-rework series (plans 1-7 dated
2026-05-15). Each scenario is encoded as a test via the
`[Trait("HRD", "<id>")]` xUnit trait (back-end) or a
browser-driven assertion (front-end). Failures must be
investigated against the originating plan, not silently
re-baselined.

## Admin generation (HRD-R1.12.x)

### HRD-R1.12.1 -- generate 14 days, MWF, two ranges, capacity 2

Goal: end-to-end happy path for the multi-axis form.
Fixtures: tenant with 1 location, 2 appointment types seeded.
Test:
  `DoctorAvailabilitiesAppServiceTests.HrdR1_12_1_MultiAxisHappyPath`
Assertions:
- Input: `selectedDays = [1, 3, 5]`, `timeRanges = [08:00-10:00 @ 60m, 13:00-15:00 @ 30m]`, `capacity = 2`, `appointmentTypeIds = [t1, t2]`.
- Date range: 14 calendar days starting Monday 2026-06-01.
- Expected slot count: 6 days * (2 + 4) = 36 slots.
- All 36 slots have `Capacity = 2` and `AppointmentTypeIds.Count = 2`.

### HRD-R1.12.2 -- empty SelectedDays defaults to all 7 weekdays

Goal: empty list is the "all weekdays" sentinel.
Test: `DoctorAvailabilitiesAppServiceTests.HrdR1_12_2_EmptySelectedDaysExpandsToAll`
Assertions:
- Input: `selectedDays = []`, 7-day range starting Sunday.
- Expected slot count = 7 (one per day, one slot per day for a
  single 60m range).

### HRD-R1.12.3 -- overlapping time ranges rejected at validate

Goal: the cross-range overlap check fires before expansion.
Test: `DoctorAvailabilitiesAppServiceTests.HrdR1_12_3_OverlappingRangesRejected`
Assertions:
- Input: `timeRanges = [08:00-10:00, 09:00-11:00]`.
- `UserFriendlyException` with message containing
  "Time ranges overlap".

### HRD-R1.12.4 -- per-range duration override honored

Goal: range-level duration trumps the input-level default.
Test: `DoctorAvailabilitiesAppServiceTests.HrdR1_12_4_PerRangeDurationOverride`
Assertions:
- Input: input-level `appointmentDurationMinutes = 60`,
  one range `08:00-10:00, appointmentDurationMinutes = 30`.
- Expected slot count for that day = 4 (30m chunks), not 2
  (60m chunks).

### HRD-R1.12.5 -- capacity 0 rejected

Goal: capacity minimum enforced in DTO validation AND in
domain-level Check.Range.
Test: `DoctorAvailabilitiesAppServiceTests.HrdR1_12_5_CapacityZeroRejected`
Assertions:
- Input: `capacity = 0`.
- AbpValidationException OR UserFriendlyException with
  "Capacity must be at least 1".

### HRD-R1.12.6 -- > 1000 estimated slots rejected

Goal: large-batch guard fires before any DB work.
Test: `DoctorAvailabilitiesAppServiceTests.HrdR1_12_6_LargeBatchRejected`
Assertions:
- Input: 31-day range, all 7 weekdays, single 24-hour range
  with 1-minute duration. Estimated count = 31 * 1440 > 1000.
- `UserFriendlyException` with message containing
  "more than 1,000 slots".

## Booking-time concurrency / capacity (HRD-R2.10.x)

### HRD-R2.10.1 -- two patients race to last seat

Goal: row-lock hint prevents double-booking under concurrency.
Test:
  `AppointmentsAppServiceTests.HrdR2_10_1_TwoPatientsRaceLastSeat`
Trait: `[Trait("Backend", "SqlServer")]` -- skip on SQLite.
Assertions:
- Fixture: 1 slot, `Capacity = 1`, no existing appointments.
- Two `Task.Run(() => CreateAsync(...))` against two
  separate `IServiceScope` instances (so the UoW is per-scope).
- Exactly one completes with success; the other throws
  `BusinessException` with code
  `Appointment.BookingSlotFull`.

### HRD-R2.10.2 -- type-mismatch causes UI refresh cascade

Goal: server returns `BookingSlotTypeMismatch`; SPA recovers.
Test (browser, via Playwright MCP):
  `hrd-r2-10-2-type-mismatch-refresh.spec.ts`
Assertions:
- Pre-seed slot with `AppointmentTypes = [t1]`.
- In the SPA: pick that slot, then change AppointmentType to
  t2 in the form, submit.
- Assert: backend returns 400 with code
  `CaseEvaluation:Appointment.BookingSlotTypeMismatch`.
- Assert: SPA shows warn toast with the localized message.
- Assert: SPA clears the `appointmentTime` form control.
- Assert: SPA refetches the picker; slot is gone from the
  dropdown (filtered out for t2).

### HRD-R2.10.3 -- manual-close mid-flight rejects booking

Goal: a `BookingStatusId = Reserved` flip mid-form blocks the
booking with the manual-close code.
Test:
  `AppointmentsAppServiceTests.HrdR2_10_3_ManualCloseRejects`
Assertions:
- Fixture: slot `BookingStatusId = Available`, `Capacity = 1`.
- Step 1: load slot via `GetDoctorAvailabilityLookupAsync`.
- Step 2: update slot to `BookingStatusId = Reserved` (admin
  action).
- Step 3: call `CreateAsync` with the slot id from step 1.
- Assert: throws with code `Appointment.BookingSlotClosed`.

## Run procedure

```bash
# Back-end HRD tests:
dotnet test --filter Trait=HRD --logger "console;verbosity=detailed"

# SqlServer-only tests (must have docker stack up):
dotnet test --filter "Trait=HRD&Trait=Backend=SqlServer"

# Browser-driven HRD tests run via the playwright MCP session;
# log results in docs/parity/wave-1-parity/_hrd-runs/.
```
```

### 2. NEW FILE `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/HrdSlotGenerationTests.cs`

Holds `HRD-R1.12.{1..6}`. The class is a partial of the
existing `DoctorAvailabilitiesAppServiceTests` infrastructure
to inherit the seed + tenant scope:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public abstract partial class DoctorAvailabilitiesAppServiceTests
{
    [Fact]
    [Trait("HRD", "R1.12.1")]
    public async Task HrdR1_12_1_MultiAxisHappyPath()
    {
        var location = await SeedLocationAsync();
        var t1 = await SeedAppointmentTypeAsync("PQME");
        var t2 = await SeedAppointmentTypeAsync("AME");

        var input = new DoctorAvailabilityGenerateInputDto
        {
            LocationId = location.Id,
            FromDate = NextMonday(),
            ToDate = NextMonday().AddDays(13),
            SelectedDays = new List<int> { 1, 3, 5 },
            TimeRanges = new List<TimeRangeDto>
            {
                new() { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0) },
                new() { FromTime = new TimeOnly(13, 0), ToTime = new TimeOnly(15, 0), AppointmentDurationMinutes = 30 },
            },
            AppointmentDurationMinutes = 60,
            AppointmentTypeIds = new() { t1.Id, t2.Id },
            BookingStatusId = BookingStatus.Available,
            Capacity = 2,
        };

        var result = await DoctorAvailabilitiesAppService.CreateRangeAsync(input);

        result.InsertedCount.ShouldBe(6 * (2 + 4));
        result.SkippedConflictCount.ShouldBe(0);

        var fetched = await DoctorAvailabilitiesAppService.GetListAsync(new GetDoctorAvailabilitiesInput
        {
            LocationId = location.Id,
            MaxResultCount = 100,
        });
        fetched.Items.Count.ShouldBe(36);
        fetched.Items.ShouldAllBe(x => x.DoctorAvailability.Capacity == 2);
        fetched.Items.ShouldAllBe(x => x.AppointmentTypes.Count == 2);
    }

    [Fact]
    [Trait("HRD", "R1.12.2")]
    public async Task HrdR1_12_2_EmptySelectedDaysExpandsToAll()
    {
        var location = await SeedLocationAsync();
        var sunday = NextSunday();

        var result = await DoctorAvailabilitiesAppService.CreateRangeAsync(new DoctorAvailabilityGenerateInputDto
        {
            LocationId = location.Id,
            FromDate = sunday,
            ToDate = sunday.AddDays(6),
            SelectedDays = new List<int>(),  // empty = all 7
            TimeRanges = new List<TimeRangeDto>
            {
                new() { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(10, 0) },
            },
            AppointmentDurationMinutes = 60,
            BookingStatusId = BookingStatus.Available,
            Capacity = 1,
        });

        result.InsertedCount.ShouldBe(7);
    }

    [Fact]
    [Trait("HRD", "R1.12.3")]
    public async Task HrdR1_12_3_OverlappingRangesRejected()
    {
        var location = await SeedLocationAsync();

        var ex = await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await DoctorAvailabilitiesAppService.GeneratePreviewAsync(
                new DoctorAvailabilityGenerateInputDto
                {
                    LocationId = location.Id,
                    FromDate = NextMonday(),
                    ToDate = NextMonday(),
                    TimeRanges = new List<TimeRangeDto>
                    {
                        new() { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0) },
                        new() { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(11, 0) },
                    },
                    AppointmentDurationMinutes = 60,
                    BookingStatusId = BookingStatus.Available,
                    Capacity = 1,
                });
        });
        ex.Message.ShouldContain("overlap");
    }

    [Fact]
    [Trait("HRD", "R1.12.4")]
    public async Task HrdR1_12_4_PerRangeDurationOverride()
    {
        var location = await SeedLocationAsync();
        var input = new DoctorAvailabilityGenerateInputDto
        {
            LocationId = location.Id,
            FromDate = NextMonday(),
            ToDate = NextMonday(),
            TimeRanges = new List<TimeRangeDto>
            {
                new() { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0), AppointmentDurationMinutes = 30 },
            },
            AppointmentDurationMinutes = 60,  // input-level default; range overrides
            BookingStatusId = BookingStatus.Available,
            Capacity = 1,
        };
        var preview = await DoctorAvailabilitiesAppService.GeneratePreviewAsync(input);
        preview.Sum(d => d.DoctorAvailabilities.Count).ShouldBe(4);
    }

    [Fact]
    [Trait("HRD", "R1.12.5")]
    public async Task HrdR1_12_5_CapacityZeroRejected()
    {
        var location = await SeedLocationAsync();
        var ex = await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await DoctorAvailabilitiesAppService.GeneratePreviewAsync(
                new DoctorAvailabilityGenerateInputDto
                {
                    LocationId = location.Id,
                    FromDate = NextMonday(),
                    ToDate = NextMonday(),
                    TimeRanges = new List<TimeRangeDto>
                    {
                        new() { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0) },
                    },
                    AppointmentDurationMinutes = 60,
                    BookingStatusId = BookingStatus.Available,
                    Capacity = 0,
                });
        });
        ex.Message.ShouldContain("Capacity must be at least 1");
    }

    [Fact]
    [Trait("HRD", "R1.12.6")]
    public async Task HrdR1_12_6_LargeBatchRejected()
    {
        var location = await SeedLocationAsync();
        var ex = await Should.ThrowAsync<UserFriendlyException>(async () =>
        {
            await DoctorAvailabilitiesAppService.GeneratePreviewAsync(
                new DoctorAvailabilityGenerateInputDto
                {
                    LocationId = location.Id,
                    FromDate = NextMonday(),
                    ToDate = NextMonday().AddDays(30),
                    SelectedDays = new List<int>(),  // all 7
                    TimeRanges = new List<TimeRangeDto>
                    {
                        new() { FromTime = new TimeOnly(0, 0), ToTime = new TimeOnly(23, 59), AppointmentDurationMinutes = 1 },
                    },
                    AppointmentDurationMinutes = 1,
                    BookingStatusId = BookingStatus.Available,
                    Capacity = 1,
                });
        });
        ex.Message.ShouldContain("more than 1,000 slots");
    }

    // Helpers below: NextMonday(), NextSunday(), SeedLocationAsync,
    // SeedAppointmentTypeAsync. Implement in the existing tests base
    // class or in a new helper trait. Match the pattern in
    // AppointmentsAppServiceTests.cs.
}
```

### 3. NEW FILE `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/HrdSlotBookingTests.cs`

Holds `HRD-R2.10.{1,3}`. R2.10.2 is browser-driven and lives in
the playwright spec below.

```csharp
[Fact]
[Trait("HRD", "R2.10.1")]
[Trait("Backend", "SqlServer")]
public async Task HrdR2_10_1_TwoPatientsRaceLastSeat()
{
    var slot = await SeedSlotAsync(capacity: 1);
    var patientA = await SeedPatientAsync();
    var patientB = await SeedPatientAsync();
    var typeId = slot.AppointmentTypes.First().AppointmentTypeId;

    // Two booking attempts in parallel, each in its own DI scope so
    // the UoW boundary is separate (matches the real HTTP request
    // model).
    var aTask = RunInScope(scope =>
        scope.ServiceProvider.GetRequiredService<IAppointmentsAppService>()
            .CreateAsync(BuildCreate(patientA.Id, slot.Id, typeId)));
    var bTask = RunInScope(scope =>
        scope.ServiceProvider.GetRequiredService<IAppointmentsAppService>()
            .CreateAsync(BuildCreate(patientB.Id, slot.Id, typeId)));

    var results = await Task.WhenAll(WrapAsync(aTask), WrapAsync(bTask));
    var successes = results.Count(r => r.Success);
    var failures = results.Count(r => !r.Success);

    successes.ShouldBe(1);
    failures.ShouldBe(1);

    var failed = results.First(r => !r.Success);
    failed.Exception.ShouldBeOfType<BusinessException>();
    ((BusinessException)failed.Exception!).Code.ShouldBe(
        CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull);
}

[Fact]
[Trait("HRD", "R2.10.3")]
public async Task HrdR2_10_3_ManualCloseRejects()
{
    var slot = await SeedSlotAsync(capacity: 1);
    var patient = await SeedPatientAsync();
    var typeId = slot.AppointmentTypes.First().AppointmentTypeId;

    // Admin closes the slot after the patient loaded it.
    await DoctorAvailabilitiesAppService.UpdateAsync(slot.Id,
        new DoctorAvailabilityUpdateDto
        {
            AvailableDate = slot.AvailableDate,
            FromTime = slot.FromTime,
            ToTime = slot.ToTime,
            BookingStatusId = BookingStatus.Reserved,
            LocationId = slot.LocationId,
            AppointmentTypeIds = new List<Guid> { typeId },
            Capacity = slot.Capacity,
            ConcurrencyStamp = slot.ConcurrencyStamp,
        });

    var ex = await Should.ThrowAsync<BusinessException>(async () =>
    {
        await AppointmentsAppService.CreateAsync(BuildCreate(patient.Id, slot.Id, typeId));
    });
    ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed);
}
```

The `WrapAsync` helper turns a `Task<T>` into a
`Task<{ Success: bool, Exception: Exception? }>` so both lanes
in the race can be observed without one cancelling the other.
Plan 2 introduced the row-lock hint that makes the race
deterministic.

### 4. NEW FILE `angular/e2e/hrd-r2-10-2-type-mismatch-refresh.spec.ts`

A Playwright spec that drives the SPA through the
type-mismatch refresh cascade. The MCP setup already exists.

```typescript
import { test, expect } from '@playwright/test';

test('HRD-R2.10.2 type-mismatch refresh', async ({ page }) => {
  // Pre-seed: this test assumes the dev seeder has run with the
  // following fixtures: tenant 'falkinstein' with 1 location, 1
  // slot at <date> 09:00-10:00 with AppointmentTypes=[PQME] and
  // Capacity=1. Adjust the test if the seed differs.
  await page.goto('https://falkinstein.localhost:4200/login');
  // ...login as Patient...

  await page.goto('https://falkinstein.localhost:4200/appointments/add');
  // Pick Location.
  await page.click('[data-cid="appointment-location-id"]');
  await page.click('text=Main Office');
  // Pick AppointmentType = PQME.
  await page.click('[data-cid="appointment-appointment-type-id"]');
  await page.click('text=PQME');
  // Pick the seeded slot.
  await page.click('input[formcontrolname="appointmentDate"]');
  await page.click('.available-day:not(.outside-month)');  // first available
  await page.selectOption('select[formcontrolname="appointmentTime"]', { index: 1 });

  // NOW change AppointmentType to AME (not permitted for this slot).
  await page.click('[data-cid="appointment-appointment-type-id"]');
  await page.click('text=AME');

  // Fill the rest of the form with synthetic patient data, then submit.
  // ...
  await page.click('button:has-text("Submit")');

  // Assert: warning toast with the localized message.
  await expect(page.locator('.toast-message')).toContainText(
    'no longer compatible',
    { timeout: 5000 },
  );

  // Assert: appointmentTime field is cleared.
  await expect(page.locator('select[formcontrolname="appointmentTime"]'))
    .toHaveValue('');

  // Assert: refetched picker no longer shows the slot.
  const slotOptions = await page.locator('select[formcontrolname="appointmentTime"] option').allTextContents();
  expect(slotOptions).not.toContain('09:00 - 10:00');
});
```

If the project does not yet have an `e2e/` folder, scaffold it
following the existing Playwright MCP convention used in
`.github/pr-media/` adjacent flow.

### 5. NEW FILE `test/HealthcareSupport.CaseEvaluation.Domain.Tests/DoctorAvailabilities/DoctorAvailabilityCapacityTests.cs`

Pure entity invariants (TDD; some of these are already covered
by plan 1's unit tests -- keep them deduplicated by moving them
here if plan 1 wasn't merged with the tests):

| # | Test | Acceptance |
|---|------|------------|
| 1 | `Ctor_RejectsCapacityZero` | Throws on `capacity = 0`. |
| 2 | `Ctor_RejectsNegativeCapacity` | Throws on `-1`. |
| 3 | `AddAppointmentType_Idempotent` | Adding same id twice = 1 row. |
| 4 | `AddAppointmentType_MirrorsTenantId` | Slot TenantId X -> join row TenantId X. |
| 5 | `RemoveAllAppointmentTypesExceptGivenIds_KeepsListed` | Seed 3; keep [1, 3] = 2 remain. |
| 6 | `Capacity_DefaultsTo1WhenCtorOmitted` | Constructor without capacity argument -> 1. |

### 6. NEW FILE `test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/Appointments/EfCoreAppointmentRepositoryActiveCountTests.cs`

| # | Test | Acceptance |
|---|------|------------|
| 1 | `ActiveCount_ExcludesRejected` | Seed 1 Appointment with status Rejected. Count = 0. |
| 2 | `ActiveCount_ExcludesCancelledNoBill` | ditto. |
| 3 | `ActiveCount_ExcludesCancelledLate` | ditto. |
| 4 | `ActiveCount_ExcludesRescheduledNoBill` | ditto. |
| 5 | `ActiveCount_ExcludesRescheduledLate` | ditto. |
| 6 | `ActiveCount_IncludesApproved` | Count = 1. |
| 7 | `ActiveCount_IncludesNoShow` | Count = 1. |
| 8 | `ActiveCount_IncludesBilled` | Count = 1. |
| 9 | `ActiveCounts_BulkFetch_GroupsCorrectly` | Seed 2 slots; each with 1 active + 1 Rejected; bulk fetch returns `{slot1: 1, slot2: 1}`. |

### 7. Documentation cross-link

In `docs/parity/wave-1-parity/staff-supervisor-doctor-management.md`:

Update the "Invariant" section's status from `[NEEDS
IMPLEMENTATION 2026-05-15]` to `[IMPLEMENTED in plan 1
(2026-05-15-doctor-invariant-enforcement); tests in plan 7
(2026-05-15-slot-rework-phase-6-tests-hardening)]`.

In `docs/parity/wave-1-parity/_parity-flags.md`:

Add a closing note under PARITY-FLAG-NEW-006:
"2026-05-15 -- enforcement landed in plans 1-7 of the
slot-generation rework series. Tests under
`test/.../HrdSlotGenerationTests.cs` and the docs reference
`docs/parity/wave-1-parity/_hrd-scenarios-slot-rework.md`."

## Test plan (running the tests this plan ships)

```bash
# Phase 6 unit tests (entity + repo + AppService): run together.
dotnet test --filter "FullyQualifiedName~DoctorAvailabilityCapacityTests" \
            --filter "FullyQualifiedName~EfCoreAppointmentRepositoryActiveCountTests" \
            --filter "FullyQualifiedName~HrdSlotGenerationTests" \
            --filter "FullyQualifiedName~HrdSlotBookingTests"

# HRD-only filter:
dotnet test --filter Trait=HRD --logger "console;verbosity=detailed"

# SqlServer-gated (R2.10.1):
docker compose up -d
dotnet test --filter "Trait=HRD&Trait=Backend=SqlServer"

# Browser (R2.10.2): run via Playwright MCP from the IDE session.
```

## Risk and rollback

**Blast radius:**
- Test code only. No product behavior touched.
- One small markdown reference doc; two cross-link edits.

**Rollback:**
- Revert the commit. Tests disappear; product code unchanged.

**Risk: HRD-R2.10.1's race test is flaky on SQLite.** Mitigated
by the `[Trait("Backend", "SqlServer")]` gate. CI must run the
SQL Server container for HRD-Backend=SqlServer; in-process
SQLite test runs skip it by default.

**Risk: helper methods (`SeedLocationAsync`, `SeedPatientAsync`,
`RunInScope`) do not yet exist in the test base.** Mitigated:
implement them in a new partial class
`DoctorAvailabilitiesAppServiceTests.Hrd.cs` (or
`AppointmentsAppServiceTests.Hrd.cs`) that lives alongside the
HRD tests; the base class stays untouched.

## Verification

After ship:

1. `dotnet test --filter Trait=HRD` -- all 9 HRD scenarios pass.
2. `dotnet test --filter "FullyQualifiedName~DoctorAvailability"` --
   all unit tests pass.
3. Run the Playwright spec via the MCP browser session; record
   the screenshot of the warning toast under
   `.github/pr-media/hrd-r2-10-2-toast.png`.
4. Update `docs/parity/wave-1-parity/_hrd-runs/2026-05-15.md`
   with the pass/fail matrix per scenario.

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- After merge, kick off the wave merge of plans 1-7 to `main`:
  ```
  git checkout main
  git pull
  git merge --no-ff feat/replicate-old-app
  git push
  ```
  (Per the project convention -- Adrian decides the timing.)
