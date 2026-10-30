using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-10-30 -- staff change the capacity of existing slots, down to 0 (closed to new requests).
/// <see cref="DoctorAvailabilitiesAppService.SetCapacityAsync"/> is all or nothing: a refusal for any
/// one slot leaves every slot as it was. Each test seeds its own slots in office A; slots are never
/// shared between tests.
/// </summary>
public abstract class DoctorAvailabilityCapacityTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // Fixed and far ahead for the tests that never touch the booking picker (no lead time or horizon
    // applies to a capacity change). The picker tests use a near date, as their siblings do.
    private static readonly DateTime SlotDay = new(2031, 5, 5, 0, 0, 0, DateTimeKind.Utc);

    private readonly IDoctorAvailabilitiesAppService _appService;
    private readonly IDoctorAvailabilityRepository _slots;
    private readonly IAppointmentRepository _appointments;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected DoctorAvailabilityCapacityTests()
    {
        _appService = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _slots = GetRequiredService<IDoctorAvailabilityRepository>();
        _appointments = GetRequiredService<IAppointmentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task SetCapacity_ToZero_ClosesTheSlot_AndTheBookingPickerOffersItToNoOne()
    {
        var date = DateTime.Today.AddDays(15);
        var closing = await InsertSlotAsync(date, 9);
        var stayingOpen = await InsertSlotAsync(date, 10);

        var result = await SetCapacityAsync(0, LocationsTestData.Location1Id, (closing, (await GetSlotAsync(closing)).ConcurrencyStamp));

        result.UpdatedCount.ShouldBe(1);
        result.Capacity.ShouldBe(0);
        var closed = await GetSlotAsync(closing);
        closed.Capacity.ShouldBe(0);
        closed.BookingStatusId.ShouldBe(BookingStatus.Available);
        (await GetSlotAsync(stayingOpen)).Capacity.ShouldBe(3);

        var input = new GetDoctorAvailabilityLookupInput
        {
            LocationId = LocationsTestData.Location1Id,
            AvailableDateFrom = DateTime.Today.AddDays(-1),
            AvailableDateTo = date.AddDays(1),
        };
        foreach (var role in new[] { "Intake Staff", IdentityUsersTestData.PatientRoleName })
        {
            List<DoctorAvailabilityDto> offered;
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principal, Guid.NewGuid(), role))
            {
                offered = await _appService.GetDoctorAvailabilityLookupAsync(input);
            }

            offered.ShouldNotContain(x => x.Id == closing, $"a closed slot must not be offered to {role}");
            offered.ShouldContain(x => x.Id == stayingOpen, $"the open slot beside it is still offered to {role}");
        }
    }

    [Fact]
    public async Task SetCapacity_BelowASlotsActiveAppointments_IsRefused_AndChangesNoSlot()
    {
        // Open slots on BOTH sides of the blocked one, first and last by id and by insertion, so a
        // version that checked and wrote slot by slot would have written at least one of them before
        // reaching the refusal, whichever order the database returns them in. The rig runs without
        // transactions, so such a write would stay.
        var open = await InsertSlotAsync(SlotDay, 9, id: Guid.Parse("00000000-0000-0000-0000-00000000a001"));
        var blocked = await InsertSlotAsync(SlotDay, 11, id: Guid.Parse("88888888-8888-8888-8888-88888888b002"));
        var openLast = await InsertSlotAsync(SlotDay, 12, id: Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffc003"));
        await InsertActiveAppointmentsAsync(blocked, SlotDay.AddHours(11), 2);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            SetCapacityAsync(1, null, (open, null), (blocked, null), (openLast, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorAvailabilityCannotSetCapacityBelowActive);
        ex.Data["capacity"].ShouldBe(1);
        ex.Data["activeCount"].ShouldBe(2L);
        ex.Data["slotDate"].ShouldBe("05/05/2031");
        ex.Data["slotTime"].ShouldBe("11:00 AM");
        ex.Data["blockedCount"].ShouldBe(1);
        (await GetSlotAsync(open)).Capacity.ShouldBe(3, "nothing is written when any slot is refused");
        (await GetSlotAsync(blocked)).Capacity.ShouldBe(3);
        (await GetSlotAsync(openLast)).Capacity.ShouldBe(3, "nothing is written when any slot is refused");
    }

    [Fact]
    public async Task SetCapacity_DownToExactlyTheActiveCount_OrUpwards_IsNeverRefused()
    {
        var slot = await InsertSlotAsync(SlotDay, 13);
        await InsertActiveAppointmentsAsync(slot, SlotDay.AddHours(13), 2);

        (await SetCapacityAsync(2, null, (slot, null))).UpdatedCount.ShouldBe(1);
        (await GetSlotAsync(slot)).Capacity.ShouldBe(2);

        (await SetCapacityAsync(5, null, (slot, null))).UpdatedCount.ShouldBe(1);
        (await GetSlotAsync(slot)).Capacity.ShouldBe(5);
    }

    [Fact]
    public async Task SetCapacity_ForTheSelectedLocation_RefusesASlotAtAnotherLocation_AndChangesNoSlot()
    {
        // The per-day change names the location on screen; another location's slot on the same day
        // must not be reachable through it, even when its id is sent.
        var here = await InsertSlotAsync(SlotDay, 14);
        var elsewhere = await InsertSlotAsync(SlotDay, 14, LocationsTestData.Location2Id);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            SetCapacityAsync(0, LocationsTestData.Location1Id, (here, null), (elsewhere, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorAvailabilitySetCapacitySlotsNotFound);
        ex.Data["missingCount"].ShouldBe(1);
        (await GetSlotAsync(here)).Capacity.ShouldBe(3);
        (await GetSlotAsync(elsewhere)).Capacity.ShouldBe(3);
    }

    [Fact]
    public async Task SetCapacity_ForASlotThatNoLongerExists_IsRefused_AndChangesNoSlot()
    {
        var real = await InsertSlotAsync(SlotDay, 15);

        var ex = await Should.ThrowAsync<BusinessException>(() => SetCapacityAsync(0, null, (real, null), (Guid.NewGuid(), null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorAvailabilitySetCapacitySlotsNotFound);
        (await GetSlotAsync(real)).Capacity.ShouldBe(3);
    }

    [Fact]
    public async Task SetCapacity_OnASlotChangedSinceTheScreenLoadedIt_IsAConflict_AndChangesNoSlot()
    {
        var fresh = await InsertSlotAsync(SlotDay, 8);
        var stale = await InsertSlotAsync(SlotDay, 10);
        var freshStamp = (await GetSlotAsync(fresh)).ConcurrencyStamp;

        await Should.ThrowAsync<AbpDbConcurrencyException>(() =>
            SetCapacityAsync(0, null, (fresh, freshStamp), (stale, "stamp-from-an-older-load")));

        (await GetSlotAsync(fresh)).Capacity.ShouldBe(3);
        (await GetSlotAsync(stale)).Capacity.ShouldBe(3);
    }

    [Fact]
    public async Task SetCapacity_CountsARepeatedSlotOnce_AndLeavesALegacyReservedSlotsStatusAlone()
    {
        var available = await InsertSlotAsync(SlotDay, 16);
        var reserved = await InsertSlotAsync(SlotDay, 17, status: BookingStatus.Reserved);

        var result = await SetCapacityAsync(2, null, (available, null), (available, null), (reserved, null));

        result.UpdatedCount.ShouldBe(2);
        (await GetSlotAsync(available)).Capacity.ShouldBe(2);
        var stillReserved = await GetSlotAsync(reserved);
        stillReserved.Capacity.ShouldBe(2);
        stillReserved.BookingStatusId.ShouldBe(BookingStatus.Reserved);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(DoctorAvailabilityConsts.MaxCapacity + 1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, DoctorAvailabilityConsts.MaxSlotsPerCapacityChange + 1)]
    public async Task SetCapacity_OutsideTheAllowedRanges_IsRefusedByValidation(int capacity, int slotCount)
    {
        var input = new DoctorAvailabilitySetCapacityInputDto
        {
            Slots = Enumerable.Range(0, slotCount).Select(_ => new DoctorAvailabilitySlotStampDto { Id = Guid.NewGuid() }).ToList(),
            Capacity = capacity,
        };

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<AbpValidationException>(() => _appService.SetCapacityAsync(input));
        }
    }

    [Fact]
    public async Task Update_OfASingleSlot_MayAlsoSetCapacityToZero()
    {
        var id = await InsertSlotAsync(SlotDay, 7);
        var slot = await GetSlotAsync(id);

        DoctorAvailabilityDto updated;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            updated = await _appService.UpdateAsync(id, new DoctorAvailabilityUpdateDto
            {
                LocationId = slot.LocationId,
                AvailableDate = slot.AvailableDate,
                FromTime = slot.FromTime,
                ToTime = slot.ToTime,
                BookingStatusId = slot.BookingStatusId,
                AppointmentTypeIds = new List<Guid>(),
                Capacity = 0,
                ConcurrencyStamp = slot.ConcurrencyStamp,
            });
        }

        updated.Capacity.ShouldBe(0);
        (await GetSlotAsync(id)).Capacity.ShouldBe(0);
    }

    private async Task<DoctorAvailabilitySetCapacityResultDto> SetCapacityAsync(
        int capacity, Guid? locationId, params (Guid Id, string? Stamp)[] slots)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await _appService.SetCapacityAsync(new DoctorAvailabilitySetCapacityInputDto
            {
                Slots = slots.Select(s => new DoctorAvailabilitySlotStampDto { Id = s.Id, ConcurrencyStamp = s.Stamp }).ToList(),
                Capacity = capacity,
                LocationId = locationId,
            });
        }
    }

    private Task<Guid> InsertSlotAsync(
        DateTime day, int fromHour, Guid? locationId = null, BookingStatus status = BookingStatus.Available, Guid? id = null) =>
        InOfficeA(async () =>
        {
            var slot = new DoctorAvailability(id ?? Guid.NewGuid(), locationId ?? LocationsTestData.Location1Id, day,
                new TimeOnly(fromHour, 0), new TimeOnly(fromHour + 1, 0), status, capacity: 3);
            return (await _slots.InsertAsync(slot, autoSave: true)).Id;
        });

    private Task<bool> InsertActiveAppointmentsAsync(Guid slotId, DateTime at, int count) =>
        InOfficeA(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                await _appointments.InsertAsync(new Appointment(
                    id: Guid.NewGuid(),
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: slotId,
                    appointmentDate: at,
                    requestConfirmationNumber: "TEST-" + Guid.NewGuid().ToString("N")[..8],
                    appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
            }
            return true;
        });

    private Task<DoctorAvailability> GetSlotAsync(Guid id) => InOfficeA(() => _slots.GetAsync(id));

    private async Task<T> InOfficeA<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }
}
