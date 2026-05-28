using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Abstract base class for DoctorAvailabilitiesAppService integration tests.
/// The concrete EfCoreDoctorAvailabilitiesAppServiceTests subclass lives
/// under EntityFrameworkCore.Tests and supplies the TStartupModule that
/// wires in SQLite + full ABP module graph.
///
/// Phase B-6 Tier-1 PR-1B (Wave 1): validation-layer guards +
/// GeneratePreviewAsync slot-math coverage only. The slot-math tests
/// pin the behavioural contract the planned P-11 refactor of the
/// 41-cognitive-complexity GeneratePreviewAsync must preserve.
///
/// Phase B-6 Wave-2 PR-W2B: happy-path CRUD using seeded Slot1/2/3 +
/// nav-prop hydration + BookingStatus + LocationId filter coverage.
/// Tests that need a fresh Available slot create a scratch row via the
/// repository so the seeded baseline (Slot1 Booked, Slot2 Available,
/// Slot3 Booked) stays intact across test order.
/// </summary>
public abstract class DoctorAvailabilitiesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDoctorAvailabilitiesAppService _appService;
    private readonly IDoctorAvailabilityRepository _slotRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected DoctorAvailabilitiesAppServiceTests()
    {
        _appService = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _slotRepository = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // =====================================================================
    // CreateAsync / UpdateAsync / DeleteBySlotAsync / DeleteByDateAsync --
    // LocationId == Guid.Empty guard. Each service method checks
    // input.LocationId BEFORE hitting the repository / manager, so we
    // can use any placeholder id for the entity id parameter on update.
    // =====================================================================

    [Fact]
    public async Task CreateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidCreateDto();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.CreateAsync(input));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task UpdateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidUpdateDto();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.UpdateAsync(DoctorAvailabilitiesTestData.NonExistentSlotId, input));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task DeleteBySlotAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = new DoctorAvailabilityDeleteBySlotInputDto
        {
            LocationId = Guid.Empty,
            AvailableDate = new DateTime(2030, 1, 1),
            FromTime = new TimeOnly(9, 0),
            ToTime = new TimeOnly(10, 0),
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.DeleteBySlotAsync(input));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task DeleteByDateAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = new DoctorAvailabilityDeleteByDateInputDto
        {
            LocationId = Guid.Empty,
            AvailableDate = new DateTime(2030, 1, 1),
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.DeleteByDateAsync(input));

        ex.Message.ShouldContain("Location");
    }

    // =====================================================================
    // GeneratePreviewAsync -- null/empty input returns empty (does NOT throw).
    // =====================================================================

    // =====================================================================
    // 2026-05-15 (slot rework plan 4) -- multi-axis generation. Replaces
    // the pre-rework single-day / single-range / single-type test set.
    // The new shape is one DoctorAvailabilityGenerateInputDto (not a list)
    // with TimeRanges + SelectedDays + AppointmentTypeIds + Capacity.
    // =====================================================================

    [Fact]
    public async Task GeneratePreviewAsync_WhenLocationEmpty_Throws()
    {
        var input = BuildValidGenerateInput();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenDurationNonPositive_Throws()
    {
        var input = BuildValidGenerateInput();
        input.AppointmentDurationMinutes = 0;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("duration");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenCapacityZero_Throws()
    {
        var input = BuildValidGenerateInput();
        input.Capacity = 0;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("Capacity", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenFromDatePast_Throws()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = DateTime.Today.AddDays(-1);
        input.ToDate = DateTime.Today.AddDays(-1);

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("past", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenNoTimeRanges_Throws()
    {
        var input = BuildValidGenerateInput();
        input.TimeRanges = new List<TimeRangeDto>();

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("time range", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenRangeFromGtTo_Throws()
    {
        var input = BuildValidGenerateInput();
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(10, 0), ToTime = new TimeOnly(9, 0) }
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("FromTime", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenRangesOverlap_Throws()
    {
        var input = BuildValidGenerateInput();
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0) },
            new TimeRangeDto { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(11, 0) },
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("overlap", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenSelectedDayOutOfRange_Throws()
    {
        var input = BuildValidGenerateInput();
        input.SelectedDays = new List<int> { 9 };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("Sunday", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenSelectedDaysDuplicate_Throws()
    {
        var input = BuildValidGenerateInput();
        input.SelectedDays = new List<int> { 1, 1 };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(input));
        ex.Message.ShouldContain("duplicate", Case.Insensitive);
    }

    [Fact]
    public async Task GeneratePreviewAsync_AllWeekdays_3DayRange_OneRange_60mDuration_Returns9Slots()
    {
        // Pure expansion math test on ExpandToSlotPreviews: 3 days x 1 range
        // of 3 hours / 60-minute duration = 9 slots total.
        var input = BuildValidGenerateInput();
        // Pick a Monday so 3 consecutive days are Mon/Tue/Wed (all weekdays).
        var monday = NextWeekday(DateTime.Today.AddDays(1), DayOfWeek.Monday);
        input.FromDate = monday;
        input.ToDate = monday.AddDays(2);
        input.SelectedDays = null;
        input.AppointmentDurationMinutes = 60;
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(12, 0) },
        };

        var result = await _appService.GeneratePreviewAsync(input);

        result.Count.ShouldBe(3);
        result.Sum(d => d.DoctorAvailabilities.Count).ShouldBe(9);
        result.ShouldAllBe(d => d.DoctorAvailabilities.Count == 3);
    }

    [Fact]
    public async Task GeneratePreviewAsync_MondayAndWednesdayOnly_5DayRange_Returns2Days()
    {
        // Pin SelectedDays filtering: Mon=1, Wed=3 within a 5-day Mon-Fri
        // range yields exactly two preview days.
        var monday = NextWeekday(DateTime.Today.AddDays(1), DayOfWeek.Monday);
        var input = BuildValidGenerateInput();
        input.FromDate = monday;
        input.ToDate = monday.AddDays(4);
        input.SelectedDays = new List<int> { 1, 3 };
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(10, 0) },
        };
        input.AppointmentDurationMinutes = 60;

        var result = await _appService.GeneratePreviewAsync(input);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(d => d.DoctorAvailabilities.Count == 1);
    }

    [Fact]
    public async Task GeneratePreviewAsync_TwoNonOverlappingRanges_30mAm_60mPm_Sums()
    {
        // Per-range duration override -- AM 30m + PM 60m on one day = 4 + 2 = 6 slots.
        var input = BuildValidGenerateInput();
        input.FromDate = DateTime.Today.AddDays(2);
        input.ToDate = DateTime.Today.AddDays(2);
        input.SelectedDays = null;
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(10, 0), AppointmentDurationMinutes = 30 },
            new TimeRangeDto { FromTime = new TimeOnly(13, 0), ToTime = new TimeOnly(15, 0), AppointmentDurationMinutes = 60 },
        };

        var result = await _appService.GeneratePreviewAsync(input);

        result.Count.ShouldBe(1);
        result[0].DoctorAvailabilities.Count.ShouldBe(6);
    }

    [Fact]
    public async Task GeneratePreviewAsync_MultiTypeSet_AppliedToEverySlot()
    {
        var input = BuildValidGenerateInput();
        input.AppointmentTypeIds = new List<Guid>
        {
            LocationsTestData.AppointmentType1Id,
            AppointmentTypesTestData.AppointmentType2Id,
        };

        var result = await _appService.GeneratePreviewAsync(input);

        result.SelectMany(d => d.DoctorAvailabilities)
            .ShouldAllBe(s => s.AppointmentTypeIds.Count == 2);
    }

    [Fact]
    public async Task GeneratePreviewAsync_CapacityAppliedToEverySlot()
    {
        var input = BuildValidGenerateInput();
        input.Capacity = 3;

        var result = await _appService.GeneratePreviewAsync(input);

        result.SelectMany(d => d.DoctorAvailabilities)
            .ShouldAllBe(s => s.Capacity == 3);
    }

    [Fact]
    public async Task CreateRangeAsync_AllNonConflicting_InsertsAllAndReturnsCount()
    {
        // Fresh date range with no pre-existing slots in the tenant scope.
        var input = BuildValidGenerateInput();
        var startDate = NextWeekday(DateTime.Today.AddDays(40), DayOfWeek.Monday);
        input.FromDate = startDate;
        input.ToDate = startDate;
        input.SelectedDays = null;
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(13, 0) },
        };
        input.AppointmentDurationMinutes = 60;
        // 1 day x 5 slots = 5

        DoctorAvailabilityCreateRangeResultDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appService.CreateRangeAsync(input);
        }
        result.InsertedCount.ShouldBe(5);
        result.SkippedConflictCount.ShouldBe(0);
        result.ConflictedSlots.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateRangeAsync_HalfConflict_InsertsRest()
    {
        // Seed two pre-existing slots that overlap the first two new slots;
        // the remaining two should insert.
        var startDate = NextWeekday(DateTime.Today.AddDays(50), DayOfWeek.Monday);
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _slotRepository.InsertAsync(new DoctorAvailability(
                    id: Guid.NewGuid(),
                    locationId: LocationsTestData.Location1Id,
                    availableDate: startDate,
                    fromTime: new TimeOnly(8, 0),
                    toTime: new TimeOnly(9, 0),
                    bookingStatusId: BookingStatus.Available), autoSave: true);
                await _slotRepository.InsertAsync(new DoctorAvailability(
                    id: Guid.NewGuid(),
                    locationId: LocationsTestData.Location1Id,
                    availableDate: startDate,
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available), autoSave: true);
            }
        });

        var input = BuildValidGenerateInput();
        input.FromDate = startDate;
        input.ToDate = startDate;
        input.SelectedDays = null;
        input.TimeRanges = new List<TimeRangeDto>
        {
            new TimeRangeDto { FromTime = new TimeOnly(8, 0), ToTime = new TimeOnly(12, 0) },
        };
        input.AppointmentDurationMinutes = 60;
        // 4 slots; first two overlap pre-existing -> 2 conflicts + 2 inserts.

        DoctorAvailabilityCreateRangeResultDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appService.CreateRangeAsync(input);
        }
        result.InsertedCount.ShouldBe(2);
        result.SkippedConflictCount.ShouldBe(2);
        result.ConflictedSlots.Count.ShouldBe(2);
    }

    [Fact(Skip = "KNOWN GAP: forcing a mid-batch insert failure (to assert rollback) requires a DB constraint violation that the in-memory SQLite test rig does not honor reliably. The transactional UoW wrap (IUnitOfWorkManager.Begin(isTransactional:true)) is the ABP-provided guarantee; behavior will be exercised end-to-end against real SQL Server in plan 7 hardening.")]
    public Task CreateRangeAsync_AnyInsertFails_RollsBack()
    {
        return Task.CompletedTask;
    }

    private static DateTime NextWeekday(DateTime from, DayOfWeek target)
    {
        var diff = ((int)target - (int)from.DayOfWeek + 7) % 7;
        return from.AddDays(diff);
    }

    // =====================================================================
    // GetListAsync -- empty-state coverage (no slots seeded in Wave 1).
    // =====================================================================

    [Fact]
    public async Task GetListAsync_WhenNoSlotsSeeded_ReturnsZeroCount()
    {
        var result = await _appService.GetListAsync(new GetDoctorAvailabilitiesInput());

        result.ShouldNotBeNull();
        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    // =====================================================================
    // Wave-2 PR-W2B: happy-path CRUD using seeded Slot1/2/3 + nav-prop
    // hydration + filter coverage. Read tests use seeded rows directly.
    // Mutation tests insert their own scratch rows so seeded baselines
    // stay intact across test order.
    // =====================================================================

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsAllThreeSeededSlots()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _appService.GetListAsync(new GetDoctorAvailabilitiesInput { MaxResultCount = 100 });

            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot1Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot2Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot3Id).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsSlot1AndSlot2()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetListAsync(new GetDoctorAvailabilitiesInput { MaxResultCount = 100 });

            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot1Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot2Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot3Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantBContext_ReturnsOnlySlot3()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var result = await _appService.GetListAsync(new GetDoctorAvailabilitiesInput { MaxResultCount = 100 });

            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot3Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot1Id).ShouldBeFalse();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FilterByBookingStatusAvailable_ReturnsSlot2Only()
    {
        // Slot2 is the only seeded slot with BookingStatus.Available.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetListAsync(new GetDoctorAvailabilitiesInput
            {
                BookingStatusId = BookingStatus.Available,
                MaxResultCount = 100,
            });

            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot2Id).ShouldBeTrue();
            result.Items.Any(x => x.DoctorAvailability.Id == DoctorAvailabilitiesTestData.Slot1Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ResolvesLocationAndAppointmentType()
    {
        // 2026-05-15 slot rework: AppointmentTypes is a M2M collection.
        // Slot1 has Location1 and a single AppointmentType1 join row.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetWithNavigationPropertiesAsync(DoctorAvailabilitiesTestData.Slot1Id);

            result.ShouldNotBeNull();
            result.DoctorAvailability.Id.ShouldBe(DoctorAvailabilitiesTestData.Slot1Id);
            result.Location.ShouldNotBeNull();
            result.Location!.Id.ShouldBe(LocationsTestData.Location1Id);
            result.AppointmentTypes.Count.ShouldBe(1);
            result.AppointmentTypes.Single().Id.ShouldBe(LocationsTestData.AppointmentType1Id);
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_WhenAppointmentTypesEmpty_ReturnsEmptyList()
    {
        // 2026-05-15 slot rework: Slot2 has no join rows (loose mode).
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetWithNavigationPropertiesAsync(DoctorAvailabilitiesTestData.Slot2Id);

            result.ShouldNotBeNull();
            result.DoctorAvailability.Id.ShouldBe(DoctorAvailabilitiesTestData.Slot2Id);
            result.AppointmentTypes.ShouldBeEmpty();
            result.Location.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task CreateAsync_WhenInputValid_PersistsScratchSlot()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var input = new DoctorAvailabilityCreateDto
            {
                LocationId = LocationsTestData.Location1Id,
                AppointmentTypeIds = new List<Guid> { LocationsTestData.AppointmentType1Id },
                AvailableDate = new DateTime(2027, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                FromTime = new TimeOnly(11, 0),
                ToTime = new TimeOnly(12, 0),
                BookingStatusId = BookingStatus.Available,
                Capacity = 3,
            };

            var created = await _appService.CreateAsync(input);

            created.ShouldNotBeNull();
            created.LocationId.ShouldBe(input.LocationId);
            created.BookingStatusId.ShouldBe(BookingStatus.Available);

            var persisted = await _slotRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task UpdateAsync_FlipsAvailableScratchToReserved_Persists()
    {
        // Pin thin-AppService intent: UpdateAsync accepts BookingStatusId
        // and overwrites it directly (Business Rule #7). Use a scratch
        // slot so seeded data isn't mutated.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var scratch = new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: LocationsTestData.Location1Id,
                availableDate: new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                fromTime: new TimeOnly(13, 0),
                toTime: new TimeOnly(14, 0),
                bookingStatusId: BookingStatus.Available);
            scratch.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            var inserted = await _slotRepository.InsertAsync(scratch, autoSave: true);

            var existing = await _slotRepository.GetAsync(inserted.Id);
            var update = new DoctorAvailabilityUpdateDto
            {
                LocationId = existing.LocationId,
                AppointmentTypeIds = existing.AppointmentTypes.Select(x => x.AppointmentTypeId).ToList(),
                AvailableDate = existing.AvailableDate,
                FromTime = existing.FromTime,
                ToTime = existing.ToTime,
                BookingStatusId = BookingStatus.Reserved,
                Capacity = existing.Capacity,
                ConcurrencyStamp = existing.ConcurrencyStamp,
            };

            var result = await _appService.UpdateAsync(inserted.Id, update);
            result.BookingStatusId.ShouldBe(BookingStatus.Reserved);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesScratchSlot()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var scratch = new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: LocationsTestData.Location1Id,
                availableDate: new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                fromTime: new TimeOnly(8, 0),
                toTime: new TimeOnly(9, 0),
                bookingStatusId: BookingStatus.Available);
            scratch.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            var inserted = await _slotRepository.InsertAsync(scratch, autoSave: true);

            await _appService.DeleteAsync(inserted.Id);

            (await _slotRepository.FindAsync(inserted.Id)).ShouldBeNull();
        }
    }

    [Fact]
    public async Task DeleteByDateAsync_RemovesAllSlotsForLocationAndDate_PreservesSeed()
    {
        // Use a scratch date distinct from Slot1 (2026-06-01) / Slot2
        // (2026-06-02) / Slot3 (2026-06-03) so seeded baselines survive.
        var scratchDate = new DateTime(2027, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var scratchA = new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: LocationsTestData.Location1Id,
                availableDate: scratchDate,
                fromTime: new TimeOnly(8, 0),
                toTime: new TimeOnly(9, 0),
                bookingStatusId: BookingStatus.Available);
            scratchA.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            var scratchB = new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: LocationsTestData.Location1Id,
                availableDate: scratchDate,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available);
            scratchB.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            var insertedA = await _slotRepository.InsertAsync(scratchA, autoSave: true);
            var insertedB = await _slotRepository.InsertAsync(scratchB, autoSave: true);

            await _appService.DeleteByDateAsync(new DoctorAvailabilityDeleteByDateInputDto
            {
                LocationId = LocationsTestData.Location1Id,
                AvailableDate = scratchDate,
            });

            (await _slotRepository.FindAsync(insertedA.Id)).ShouldBeNull();
            (await _slotRepository.FindAsync(insertedB.Id)).ShouldBeNull();
            // Seed Slot1 (different date) untouched.
            (await _slotRepository.FindAsync(DoctorAvailabilitiesTestData.Slot1Id)).ShouldNotBeNull();
        }
    }

    // =====================================================================
    // Gap-encoding tests (Skip= with tracking references).
    // =====================================================================

    [Fact(Skip = "KNOWN GAP: GeneratePreviewAsync + CreateAsync are not atomic. Two concurrent callers can both see an Available slot, both generate non-conflicting previews, then both issue CreateAsync, resulting in duplicate overlapping slots. Tracked in src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md under 'Known Gotchas' #2.")]
    public Task GeneratePreviewAsync_ConcurrentCalls_PreventDuplicateSlotCreation()
    {
        // Expected behaviour (not yet implemented):
        // 1. Caller A invokes GeneratePreviewAsync(date=D, time=T) with no existing slots -> no conflict
        // 2. Caller B invokes GeneratePreviewAsync(date=D, time=T) in parallel -> also no conflict
        // 3. Both call CreateAsync with the previewed slot
        // 4. Assert: second CreateAsync either throws OR the manager enforces a uniqueness constraint
        // Current behaviour: both CreateAsync calls succeed, producing duplicate Available slots.
        return Task.CompletedTask;
    }

    [Fact(Skip = "KNOWN GAP: UpdateAsync allows flipping BookingStatus from Booked to Available without checking whether an Appointment.DoctorAvailabilityId still references this slot. The slot/appointment invariant can be silently broken. Tracked in src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md under 'Business Rules' #5.")]
    public Task UpdateAsync_ChangeBookedStatusBackToAvailable_WhenSlotStillBooked_ShouldThrow()
    {
        // Expected behaviour (not yet implemented):
        // 1. Seed DoctorAvailability slot with BookingStatusId=Booked
        // 2. Seed Appointment that references that slot
        // 3. Invoke UpdateAsync on the slot setting BookingStatusId=Available
        // 4. Assert: UserFriendlyException or domain-level exception is thrown
        // Current behaviour: UpdateAsync succeeds silently, orphaning the appointment's slot reference.
        return Task.CompletedTask;
    }

    // =====================================================================
    // Helpers.
    // =====================================================================

    private static DoctorAvailabilityCreateDto BuildValidCreateDto()
    {
        return new DoctorAvailabilityCreateDto
        {
            AvailableDate = new DateTime(2030, 1, 1),
            FromTime = new TimeOnly(9, 0),
            ToTime = new TimeOnly(10, 0),
            BookingStatusId = BookingStatus.Available,
            LocationId = DoctorAvailabilitiesTestData.NonExistentLocationId,
            AppointmentTypeIds = new List<Guid> { DoctorAvailabilitiesTestData.NonExistentAppointmentTypeId },
            Capacity = 3,
        };
    }

    private static DoctorAvailabilityUpdateDto BuildValidUpdateDto()
    {
        return new DoctorAvailabilityUpdateDto
        {
            AvailableDate = new DateTime(2030, 1, 1),
            FromTime = new TimeOnly(9, 0),
            ToTime = new TimeOnly(10, 0),
            BookingStatusId = BookingStatus.Available,
            LocationId = DoctorAvailabilitiesTestData.NonExistentLocationId,
            AppointmentTypeIds = new List<Guid> { DoctorAvailabilitiesTestData.NonExistentAppointmentTypeId },
            Capacity = 3,
            ConcurrencyStamp = string.Empty,
        };
    }

    private static DoctorAvailabilityGenerateInputDto BuildValidGenerateInput()
    {
        // 2026-05-15 (slot rework plan 4) -- multi-axis generation input.
        // Date is +1 day so the FromDatePast validator does not fire on the
        // default; SelectedDays=null means "all weekdays"; one TimeRange.
        return new DoctorAvailabilityGenerateInputDto
        {
            FromDate = DateTime.Today.AddDays(1),
            ToDate = DateTime.Today.AddDays(1),
            SelectedDays = null,
            TimeRanges = new List<TimeRangeDto>
            {
                new TimeRangeDto { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(10, 0) },
            },
            BookingStatusId = BookingStatus.Available,
            LocationId = LocationsTestData.Location1Id,
            AppointmentTypeIds = new List<Guid>(),
            AppointmentDurationMinutes = 60,
            Capacity = 3,
        };
    }

    // =====================================================================
    // 2026-05-15 -- slot rework plan 3: GetDoctorAvailabilityLookupAsync
    // computes RemainingCapacity = Capacity - activeAppointmentCount and
    // excludes full slots / Reserved slots from the picker.
    // =====================================================================

    [Fact]
    public async Task GetDoctorAvailabilityLookupAsync_RemainingCapacityComputed()
    {
        var date = DateTime.Today.AddDays(15);
        var slotId = Guid.NewGuid();
        var appointmentRepo = GetRequiredService<HealthcareSupport.CaseEvaluation.Appointments.IAppointmentRepository>();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var slot = new DoctorAvailability(
                    id: slotId,
                    locationId: LocationsTestData.Location1Id,
                    availableDate: date,
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available,
                    capacity: 3);
                slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
                await _slotRepository.InsertAsync(slot, autoSave: true);

                await appointmentRepo.InsertAsync(new HealthcareSupport.CaseEvaluation.Appointments.Appointment(
                    id: Guid.NewGuid(),
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: slotId,
                    appointmentDate: date.AddHours(9).AddMinutes(15),
                    requestConfirmationNumber: "A-RC-1",
                    appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
            }
        });

        List<DoctorAvailabilityDto> result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appService.GetDoctorAvailabilityLookupAsync(new GetDoctorAvailabilityLookupInput
            {
                LocationId = LocationsTestData.Location1Id,
                // AvailableDateFrom is today-1 so leadTime (default 3) + that
                // does not push minDate past the slot's date today+15..+18.
                AvailableDateFrom = DateTime.Today.AddDays(-1),
                AvailableDateTo = date.AddDays(1),
            });
        }

        var dto = result.SingleOrDefault(x => x.Id == slotId);
        dto.ShouldNotBeNull();
        dto.RemainingCapacity.ShouldBe(2);
    }

    [Fact]
    public async Task GetDoctorAvailabilityLookupAsync_FullSlotsExcluded()
    {
        var date = DateTime.Today.AddDays(16);
        var slotId = Guid.NewGuid();
        var appointmentRepo = GetRequiredService<HealthcareSupport.CaseEvaluation.Appointments.IAppointmentRepository>();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available,
                capacity: 1);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            await _slotRepository.InsertAsync(slot, autoSave: true);

            await appointmentRepo.InsertAsync(new HealthcareSupport.CaseEvaluation.Appointments.Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: slotId,
                appointmentDate: date.AddHours(9).AddMinutes(15),
                requestConfirmationNumber: "A-FULL-1",
                appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
        }

        List<DoctorAvailabilityDto> result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appService.GetDoctorAvailabilityLookupAsync(new GetDoctorAvailabilityLookupInput
            {
                LocationId = LocationsTestData.Location1Id,
                // AvailableDateFrom is today-1 so leadTime (default 3) + that
                // does not push minDate past the slot's date today+15..+18.
                AvailableDateFrom = DateTime.Today.AddDays(-1),
                AvailableDateTo = date.AddDays(1),
            });
        }

        result.Any(x => x.Id == slotId).ShouldBeFalse();
    }

    [Fact]
    public async Task GetDoctorAvailabilityLookupAsync_ReservedSlotsExcluded()
    {
        var date = DateTime.Today.AddDays(17);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Reserved,
                capacity: 10);
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            await _slotRepository.InsertAsync(slot, autoSave: true);
        }

        List<DoctorAvailabilityDto> result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _appService.GetDoctorAvailabilityLookupAsync(new GetDoctorAvailabilityLookupInput
            {
                LocationId = LocationsTestData.Location1Id,
                // AvailableDateFrom is today-1 so leadTime (default 3) + that
                // does not push minDate past the slot's date today+15..+18.
                AvailableDateFrom = DateTime.Today.AddDays(-1),
                AvailableDateTo = date.AddDays(1),
            });
        }

        result.Any(x => x.Id == slotId).ShouldBeFalse();
    }

    [Fact]
    public async Task GetDoctorAvailabilityLookupAsync_TypeFilterRespected()
    {
        var date = DateTime.Today.AddDays(18);
        var slotId = Guid.NewGuid();

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var slot = new DoctorAvailability(
                id: slotId,
                locationId: LocationsTestData.Location1Id,
                availableDate: date,
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available,
                capacity: 3);
            // Strict-mode: AppointmentType1 only.
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            await _slotRepository.InsertAsync(slot, autoSave: true);
        }

        List<DoctorAvailabilityDto> result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Caller requests AppointmentType2 -- should omit the strict slot.
            result = await _appService.GetDoctorAvailabilityLookupAsync(new GetDoctorAvailabilityLookupInput
            {
                LocationId = LocationsTestData.Location1Id,
                AppointmentTypeId = AppointmentTypesTestData.AppointmentType2Id,
                // AvailableDateFrom is today-1 so leadTime (default 3) + that
                // does not push minDate past the slot's date today+15..+18.
                AvailableDateFrom = DateTime.Today.AddDays(-1),
                AvailableDateTo = date.AddDays(1),
            });
        }

        result.Any(x => x.Id == slotId).ShouldBeFalse();
    }
}
