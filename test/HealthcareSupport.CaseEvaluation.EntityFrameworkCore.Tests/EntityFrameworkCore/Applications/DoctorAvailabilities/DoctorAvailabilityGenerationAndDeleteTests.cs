using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Slot-generation and slot-delete branches of <see cref="DoctorAvailabilitiesAppService"/> no
/// other test reaches: a preview that overlaps a CLOSED (Reserved) slot, a generation whose
/// weekday filter selects no date, the per-range duration and the 5,000-slot cap refusals, the
/// single delete of a referenced slot, and the by-date delete that skips in-flight slots.
///
/// <para>Two of these read or delete by location and date, which is office-scoped, so each
/// carries an OFFICE decoy: a slot at the same location and date in office B. The preview must
/// not report it as a conflict, and the by-date delete must not delete it.</para>
/// </summary>
public class DoctorAvailabilityGenerationAndDeleteTests : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IDoctorAvailabilitiesAppService _service;
    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly IAppointmentChangeRequestRepository _requests;
    private readonly ICurrentTenant _currentTenant;

    public DoctorAvailabilityGenerationAndDeleteTests()
    {
        _service = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _requests = GetRequiredService<IAppointmentChangeRequestRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static DateOnly FutureDate => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(400);

    [Fact]
    public async Task Preview_FlagsTheSlotOverlappingAClosedSlot_AndIgnoresAnotherOfficesSlot()
    {
        var day = FutureDate;
        await InsertSlotAsync(TenantsTestData.TenantARef, day, 9, BookingStatus.Reserved);
        // Office decoy: office B has a slot at the same location, overlapping the 10:00 hour.
        await InsertSlotAsync(TenantsTestData.TenantBRef, day, 10, BookingStatus.Available);

        var preview = await InOfficeAsync(TenantsTestData.TenantARef, () => _service.GeneratePreviewAsync(Input(day, 9, 11, durationMinutes: 60)));

        var date = preview.ShouldHaveSingleItem();
        date.DoctorAvailabilities.Single(s => s.FromTime == new TimeOnly(10, 0)).IsConflict.ShouldBeFalse();
        date.DoctorAvailabilities.Single(s => s.FromTime == new TimeOnly(9, 0)).IsConflict.ShouldBeTrue();
        date.SameTimeValidation.ShouldBe("Time slot overlaps a closed slot at this location.");
    }

    [Fact]
    public async Task PreviewAndCreateRange_WhenTheWeekdayFilterSelectsNoDate_ProduceNothing()
    {
        var day = FutureDate;
        var input = Input(day, 9, 11, durationMinutes: 60);
        input.SelectedDates = null;
        input.FromDate = input.ToDate = day.ToDateTime(TimeOnly.MinValue);
        input.SelectedDays = new List<int> { ((int)day.DayOfWeek + 1) % 7 }; // the next weekday only

        var preview = await InOfficeAsync(TenantsTestData.TenantARef, () => _service.GeneratePreviewAsync(input));
        var created = await InOfficeAsync(TenantsTestData.TenantARef, () => _service.CreateRangeAsync(input));

        preview.ShouldBeEmpty();
        created.InsertedCount.ShouldBe(0);
        created.SkippedConflictCount.ShouldBe(0);
        (await InOfficeAsync(TenantsTestData.TenantARef, () => _slots.CountAsync(s => s.LocationId == LocationsTestData.Location1Id && s.AvailableDate == input.FromDate)))
            .ShouldBe(0);
    }

    [Fact]
    public async Task Preview_RefusesARangeWhoseOwnDurationIsNotPositive()
    {
        var input = Input(FutureDate, 9, 11, durationMinutes: 30);
        input.TimeRanges[0].AppointmentDurationMinutes = 0; // overrides the valid input-level 30

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => InOfficeAsync(TenantsTestData.TenantARef, () => _service.GeneratePreviewAsync(input)));

        ex.Message.ShouldContain("duration must be > 0");
    }

    [Fact]
    public async Task Preview_RefusesAGenerationOverTheSlotCap()
    {
        var input = Input(FutureDate, 0, 23, durationMinutes: 5); // 276 slots a day
        input.SelectedDates = Enumerable.Range(0, 20).Select(i => FutureDate.AddDays(i)).ToList(); // 5,520 in all

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => InOfficeAsync(TenantsTestData.TenantARef, () => _service.GeneratePreviewAsync(input)));

        ex.Message.ShouldContain("more than 5000 slots");
    }

    [Fact]
    public async Task Delete_OfASlotAChangeRequestProposes_IsRefused_AndTheSlotStays()
    {
        var referenced = await InsertSlotAsync(TenantsTestData.TenantARef, FutureDate, 9, BookingStatus.Available);
        var unreferenced = await InsertSlotAsync(TenantsTestData.TenantARef, FutureDate, 11, BookingStatus.Available);
        await InOfficeAsync(TenantsTestData.TenantARef, () => _requests.InsertAsync(new AppointmentChangeRequest(
            Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, ChangeRequestType.Reschedule,
            cancellationReason: null, reScheduleReason: "TEST-reschedule reason", newDoctorAvailabilityId: referenced), autoSave: true));

        var ex = await Should.ThrowAsync<BusinessException>(() => InOfficeAsync(TenantsTestData.TenantARef, async () =>
        {
            await _service.DeleteAsync(referenced);
            return true;
        }));
        // Positive control: the same call deletes a slot nothing references.
        await InOfficeAsync(TenantsTestData.TenantARef, async () =>
        {
            await _service.DeleteAsync(unreferenced);
            return true;
        });

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorAvailabilityCannotDeleteReferenced);
        (await InOfficeAsync(TenantsTestData.TenantARef, () => _slots.FindAsync(referenced))).ShouldNotBeNull();
        (await InOfficeAsync(TenantsTestData.TenantARef, () => _slots.FindAsync(unreferenced))).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByDate_DeletesOnlyThisOfficesFreeSlots_AndSkipsInFlightOnes()
    {
        var day = FutureDate;
        var free = await InsertSlotAsync(TenantsTestData.TenantARef, day, 9, BookingStatus.Available);
        var held = await InsertSlotAsync(TenantsTestData.TenantARef, day, 10, BookingStatus.Reserved);
        var otherDay = await InsertSlotAsync(TenantsTestData.TenantARef, day.AddDays(1), 9, BookingStatus.Available);
        // Office decoy: a free slot at the same location and date in office B.
        var otherOffice = await InsertSlotAsync(TenantsTestData.TenantBRef, day, 11, BookingStatus.Available);

        var result = await InOfficeAsync(TenantsTestData.TenantARef, () => _service.DeleteByDateAsync(new DoctorAvailabilityDeleteByDateInputDto
        {
            LocationId = LocationsTestData.Location1Id,
            AvailableDate = day.ToDateTime(TimeOnly.MinValue),
        }));

        (await InOfficeAsync(TenantsTestData.TenantBRef, () => _slots.FindAsync(otherOffice))).ShouldNotBeNull();
        result.DeletedCount.ShouldBe(1);
        result.SkippedSlotIds.ShouldBe(new[] { held });
        (await InOfficeAsync(TenantsTestData.TenantARef, () => _slots.FindAsync(free))).ShouldBeNull();
        (await InOfficeAsync(TenantsTestData.TenantARef, () => _slots.FindAsync(otherDay))).ShouldNotBeNull();
    }

    private static DoctorAvailabilityGenerateInputDto Input(DateOnly day, int fromHour, int toHour, int durationMinutes) => new()
    {
        LocationId = LocationsTestData.Location1Id,
        SelectedDates = new List<DateOnly> { day },
        TimeRanges = new List<TimeRangeDto> { new() { FromTime = new TimeOnly(fromHour, 0), ToTime = new TimeOnly(toHour, 0) } },
        AppointmentDurationMinutes = durationMinutes,
        Capacity = 3,
    };

    /// <summary>A one-hour slot at location 1 on <paramref name="day"/> from <paramref name="fromHour"/>.</summary>
    private Task<Guid> InsertSlotAsync(Guid officeId, DateOnly day, int fromHour, BookingStatus status) =>
        InOfficeAsync(officeId, async () =>
        {
            var slot = new DoctorAvailability(Guid.NewGuid(), LocationsTestData.Location1Id, day.ToDateTime(TimeOnly.MinValue),
                new TimeOnly(fromHour, 0), new TimeOnly(fromHour + 1, 0), status);
            slot.TenantId = officeId;
            return (await _slots.InsertAsync(slot, autoSave: true)).Id;
        });

    private Task<T> InOfficeAsync<T>(Guid officeId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                return await action();
            }
        });
}
