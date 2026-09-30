using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// The parts of <see cref="DoctorAvailabilitiesAppService"/> its existing tests do not reach: the
/// location and appointment-type lookups, deleting by slot, the booked-slot update guard, and the
/// generation input guards.
/// </summary>
/// <remarks>
/// Dates are FIXED (far future for real slots, far past for the refused one), never derived from
/// today: the office windows are Pacific dates and the CI gate bans a UTC instant's date (#623).
/// Deleting by slot runs with a slot at another time on the same day present, which must survive.
/// </remarks>
public abstract class DoctorAvailabilitiesGuardTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly DateTime SlotDay = new(2031, 3, 3, 0, 0, 0, DateTimeKind.Utc);

    private readonly IDoctorAvailabilitiesAppService _availabilities;
    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorAvailabilitiesGuardTests()
    {
        _availabilities = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<T> InOffice<T>(Guid? officeId, Func<Task<T>> call)
    {
        using (_currentTenant.Change(officeId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task InOffice(Guid? officeId, Func<Task> call) =>
        InOffice(officeId, async () =>
        {
            await call();
            return true;
        });

    private Task<Guid> InsertSlotAsync(TimeOnly from, TimeOnly to) =>
        InOffice(TenantsTestData.TenantARef, async () =>
        {
            var slot = new DoctorAvailability(Guid.NewGuid(), LocationsTestData.Location1Id, SlotDay, from, to, BookingStatus.Available);
            slot.TenantId = TenantsTestData.TenantARef;
            return (await _slots.InsertAsync(slot, autoSave: true)).Id;
        });

    [Fact]
    public async Task The_location_and_appointment_type_lookups_return_only_what_matches()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await InOffice(null, async () =>
        {
            var locations = GetRequiredService<IRepository<Location, Guid>>();
            await locations.InsertAsync(new Location(Guid.NewGuid(), null, $"Synthetic Location {token}", 0m, isActive: true), autoSave: true);
            await locations.InsertAsync(new Location(Guid.NewGuid(), null, "Synthetic Unmatched Location", 0m, isActive: true), autoSave: true);
            var types = GetRequiredService<IRepository<AppointmentType, Guid>>();
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), $"Synthetic Type {token}"), autoSave: true);
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), "Synthetic Unmatched Type"), autoSave: true);
        });

        var locations = await InOffice(null, () => _availabilities.GetLocationLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var types = await InOffice(null, () => _availabilities.GetAppointmentTypeLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));

        locations.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Location {token}");
        types.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Type {token}");
    }

    [Fact]
    public async Task Deleting_by_slot_removes_that_time_only_and_needs_a_location()
    {
        var target = await InsertSlotAsync(new TimeOnly(9, 0), new TimeOnly(10, 0));
        // LOAD-BEARING SURVIVOR: same location and day, another time.
        var survivor = await InsertSlotAsync(new TimeOnly(10, 0), new TimeOnly(11, 0));

        await InOffice(TenantsTestData.TenantARef, () => _availabilities.DeleteBySlotAsync(new DoctorAvailabilityDeleteBySlotInputDto
        {
            LocationId = LocationsTestData.Location1Id,
            AvailableDate = SlotDay,
            FromTime = new TimeOnly(9, 0),
            ToTime = new TimeOnly(10, 0),
        }));

        (await InOffice(TenantsTestData.TenantARef, () => _slots.FindAsync(target))).ShouldBeNull();
        (await InOffice(TenantsTestData.TenantARef, () => _slots.FindAsync(survivor))).ShouldNotBeNull();
        await Should.ThrowAsync<UserFriendlyException>(() => InOffice(TenantsTestData.TenantARef, () =>
            _availabilities.DeleteBySlotAsync(new DoctorAvailabilityDeleteBySlotInputDto { LocationId = Guid.Empty, AvailableDate = SlotDay })));
    }

    [Fact]
    public async Task A_booked_slot_cannot_be_edited()
    {
        var before = await InOffice(TenantsTestData.TenantARef, () => _slots.GetAsync(DoctorAvailabilitiesTestData.Slot1Id));
        before.BookingStatusId.ShouldBe(BookingStatus.Booked, "the seeded Slot1 is booked by Appointment 1");

        var refused = await Should.ThrowAsync<BusinessException>(() => InOffice(TenantsTestData.TenantARef, () =>
            _availabilities.UpdateAsync(DoctorAvailabilitiesTestData.Slot1Id, new DoctorAvailabilityUpdateDto
            {
                AvailableDate = SlotDay,
                FromTime = new TimeOnly(13, 0),
                ToTime = new TimeOnly(14, 0),
                BookingStatusId = BookingStatus.Available,
                LocationId = LocationsTestData.Location1Id,
                ConcurrencyStamp = before.ConcurrencyStamp,
            })));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorAvailabilityCannotUpdateBookedOrReserved);
        (await InOffice(TenantsTestData.TenantARef, () => _slots.GetAsync(DoctorAvailabilitiesTestData.Slot1Id)))
            .FromTime.ShouldBe(before.FromTime);
    }

    [Fact]
    public async Task Generation_refuses_past_or_repeated_dates_and_an_inverted_range()
    {
        DoctorAvailabilityGenerateInputDto Input(Action<DoctorAvailabilityGenerateInputDto> shape)
        {
            var input = new DoctorAvailabilityGenerateInputDto
            {
                FromDate = SlotDay,
                ToDate = SlotDay,
                LocationId = LocationsTestData.Location1Id,
                TimeRanges = new List<TimeRangeDto> { new() { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(10, 0) } },
            };
            shape(input);
            return input;
        }

        var cases = new[]
        {
            Input(i => i.SelectedDates = new List<DateOnly> { new(2020, 1, 6) }),
            Input(i => i.SelectedDates = new List<DateOnly> { new(2031, 3, 3), new(2031, 3, 3) }),
            Input(i => { i.FromDate = SlotDay.AddDays(7); i.ToDate = SlotDay; }),
        };

        foreach (var input in cases)
        {
            await Should.ThrowAsync<UserFriendlyException>(() => InOffice(TenantsTestData.TenantARef, () => _availabilities.GeneratePreviewAsync(input)));
        }
    }
}
