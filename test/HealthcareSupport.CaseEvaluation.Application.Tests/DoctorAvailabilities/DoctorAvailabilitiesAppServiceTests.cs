using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Abstract base class for DoctorAvailabilitiesAppService integration tests.
/// The concrete EfCoreDoctorAvailabilitiesAppServiceTests subclass lives
/// under EntityFrameworkCore.Tests and supplies the TStartupModule that
/// wires in SQLite + full ABP module graph.
///
/// Phase B-6 Tier-1 PR-1B scope (Wave 1): validation-layer guards +
/// GeneratePreviewAsync slot-math coverage only. Seeded CRUD happy-path,
/// conflict-detection tests (require seeded existing slots), and
/// repository nav-property joins are deferred to Wave 2 once the
/// SQLite FK-enforcement posture for Location + AppointmentType seeds
/// is resolved.
///
/// This PR directly targets the 41-cognitive-complexity GeneratePreviewAsync
/// method that the P-11 refactor will later split. The slot-math tests
/// pin the behavioural contract the refactor must preserve.
/// </summary>
public abstract class DoctorAvailabilitiesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDoctorAvailabilitiesAppService _appService;

    protected DoctorAvailabilitiesAppServiceTests()
    {
        _appService = GetRequiredService<IDoctorAvailabilitiesAppService>();
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

    [Fact]
    public async Task GeneratePreviewAsync_WhenInputIsNull_ThrowsAbpValidation()
    {
        // NB: The AppService body contains `if (input == null || input.Count == 0) return empty;`
        // but ABP's method-invocation validation interceptor rejects null for non-nullable
        // list parameters BEFORE the method body runs, so the null branch is dead code.
        // This test pins the actual observable behaviour.
        await Should.ThrowAsync<AbpValidationException>(
            async () => await _appService.GeneratePreviewAsync(null!));
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenInputIsEmpty_ReturnsEmpty()
    {
        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto>());

        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    // =====================================================================
    // GeneratePreviewAsync -- per-item guard clauses (4 branches).
    // =====================================================================

    [Fact]
    public async Task GeneratePreviewAsync_WhenLocationIdIsEmpty_Throws()
    {
        var input = BuildValidGenerateInput();
        input.LocationId = Guid.Empty;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("Location");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenDurationIsZero_Throws()
    {
        var input = BuildValidGenerateInput();
        input.AppointmentDurationMinutes = 0;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("duration");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenDurationIsNegative_Throws()
    {
        var input = BuildValidGenerateInput();
        input.AppointmentDurationMinutes = -5;

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("duration");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenToDateBeforeFromDate_Throws()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 10);
        input.ToDate = new DateTime(2030, 1, 1);

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("date");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenToTimeEqualsFromTime_Throws()
    {
        var input = BuildValidGenerateInput();
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(9, 0);

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("time");
    }

    [Fact]
    public async Task GeneratePreviewAsync_WhenToTimeBeforeFromTime_Throws()
    {
        var input = BuildValidGenerateInput();
        input.FromTime = new TimeOnly(10, 0);
        input.ToTime = new TimeOnly(9, 0);

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input }));

        ex.Message.ShouldContain("time");
    }

    // =====================================================================
    // GeneratePreviewAsync -- slot-math happy path + boundaries.
    // Pins the behavioural contract that the P-11 refactor
    // (extract ValidateGenerateInput / GeneratePreviewSlots / DetectConflicts)
    // must preserve. No seeded slots => no conflict-detection branch.
    // =====================================================================

    [Fact]
    public async Task GeneratePreviewAsync_SingleDay_60MinuteSlot_InOneHourRange_Returns1Slot()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 1);
        input.ToDate = new DateTime(2030, 1, 1);
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(10, 0);
        input.AppointmentDurationMinutes = 60;

        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input });

        result.Count.ShouldBe(1);
        result[0].DoctorAvailabilities.Count.ShouldBe(1);
        result[0].DoctorAvailabilities[0].FromTime.ShouldBe(new TimeOnly(9, 0));
        result[0].DoctorAvailabilities[0].ToTime.ShouldBe(new TimeOnly(10, 0));
        result[0].DoctorAvailabilities[0].IsConflict.ShouldBeFalse();
    }

    [Fact]
    public async Task GeneratePreviewAsync_SingleDay_30MinuteSlots_InOneHourRange_Returns2Slots()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 1);
        input.ToDate = new DateTime(2030, 1, 1);
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(10, 0);
        input.AppointmentDurationMinutes = 30;

        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input });

        result.Count.ShouldBe(1);
        result[0].DoctorAvailabilities.Count.ShouldBe(2);
        result[0].DoctorAvailabilities[0].FromTime.ShouldBe(new TimeOnly(9, 0));
        result[0].DoctorAvailabilities[0].ToTime.ShouldBe(new TimeOnly(9, 30));
        result[0].DoctorAvailabilities[1].FromTime.ShouldBe(new TimeOnly(9, 30));
        result[0].DoctorAvailabilities[1].ToTime.ShouldBe(new TimeOnly(10, 0));
    }

    [Fact]
    public async Task GeneratePreviewAsync_MultiDay_ReturnsOnePreviewPerDay()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 1);
        input.ToDate = new DateTime(2030, 1, 3);
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(10, 0);
        input.AppointmentDurationMinutes = 60;

        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input });

        result.Count.ShouldBe(3);
        result.Select(r => r.Dates).ShouldBe(new[] { "01-01-2030", "01-02-2030", "01-03-2030" });
        result.ShouldAllBe(r => r.DoctorAvailabilities.Count == 1);
    }

    [Fact]
    public async Task GeneratePreviewAsync_DurationLongerThanRange_Returns0Slots()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 1);
        input.ToDate = new DateTime(2030, 1, 1);
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(9, 30);
        input.AppointmentDurationMinutes = 60;

        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input });

        // When no slots fit, the outer grouping produces zero preview entries
        // (not an entry with zero slots) because the generated-slots list is empty.
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GeneratePreviewAsync_Boundary_60MinuteInExactly60MinuteRange_Returns1Slot()
    {
        var input = BuildValidGenerateInput();
        input.FromDate = new DateTime(2030, 1, 1);
        input.ToDate = new DateTime(2030, 1, 1);
        input.FromTime = new TimeOnly(9, 0);
        input.ToTime = new TimeOnly(10, 0);
        input.AppointmentDurationMinutes = 60;

        var result = await _appService.GeneratePreviewAsync(new List<DoctorAvailabilityGenerateInputDto> { input });

        result.Count.ShouldBe(1);
        result[0].DoctorAvailabilities.Count.ShouldBe(1);
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
            AppointmentTypeId = DoctorAvailabilitiesTestData.NonExistentAppointmentTypeId,
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
            AppointmentTypeId = DoctorAvailabilitiesTestData.NonExistentAppointmentTypeId,
            ConcurrencyStamp = string.Empty,
        };
    }

    private static DoctorAvailabilityGenerateInputDto BuildValidGenerateInput()
    {
        return new DoctorAvailabilityGenerateInputDto
        {
            FromDate = new DateTime(2030, 1, 1),
            ToDate = new DateTime(2030, 1, 1),
            FromTime = new TimeOnly(9, 0),
            ToTime = new TimeOnly(10, 0),
            BookingStatusId = BookingStatus.Available,
            LocationId = DoctorAvailabilitiesTestData.NonExistentLocationId,
            AppointmentTypeId = DoctorAvailabilitiesTestData.NonExistentAppointmentTypeId,
            AppointmentDurationMinutes = 60,
        };
    }
}
