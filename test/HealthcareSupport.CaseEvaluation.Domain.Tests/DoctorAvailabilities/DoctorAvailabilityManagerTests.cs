using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// Domain-service tests for <see cref="DoctorAvailabilityManager"/>. Mirrors
/// the abstract+concrete split used by Samples/SampleDomainTests + the
/// AppointmentManager test pair from Wave-2 PR-W2A.
///
/// Phase B-6 Wave-2 PR-W2B: greenfield manager-level coverage. The manager
/// is intentionally thin -- the only domain-level guards are NotNull checks
/// on locationId, availableDate, and bookingStatusId; overlap detection,
/// past-date guards, and booked-slot guards live in the AppService (and
/// only partially there). These tests pin the thin-manager intent so a
/// future refactor that adds domain-level checks shows up as test deltas.
/// </summary>
public abstract class DoctorAvailabilityManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly DoctorAvailabilityManager _slotManager;
    private readonly IDoctorAvailabilityRepository _slotRepository;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorAvailabilityManagerTests()
    {
        _slotManager = GetRequiredService<DoctorAvailabilityManager>();
        _slotRepository = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Manager_CreateAsync_PersistsSlotAndReturnsEntity()
    {
        // Manager.CreateAsync does Insert without autoSave, so the row is
        // not visible to a separate FindAsync inside the same UoW. Use the
        // Samples/SampleDomainTests pattern: mutate inside the UoW, read
        // back after it commits.
        Guid createdId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var created = await _slotManager.CreateAsync(
                    locationId: LocationsTestData.Location1Id,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    availableDate: new DateTime(2028, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available);

                created.ShouldNotBeNull();
                created.Id.ShouldNotBe(Guid.Empty);
                created.LocationId.ShouldBe(LocationsTestData.Location1Id);
                created.BookingStatusId.ShouldBe(BookingStatus.Available);
                createdId = created.Id;
            }
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var persisted = await _slotRepository.FindAsync(createdId);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Manager_UpdateAsync_OverwritesAllMutableFields()
    {
        // Thin-manager intent: UpdateAsync overwrites the 6 fields it accepts
        // (location, appt-type, date, from/to times, booking status). The
        // manager's internal GetAsync(id) won't find a freshly inserted row
        // inside the same UoW, so split Create + Update across two UoWs.
        Guid createdId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var created = await _slotManager.CreateAsync(
                    locationId: LocationsTestData.Location1Id,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    availableDate: new DateTime(2028, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available);
                createdId = created.Id;
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var updated = await _slotManager.UpdateAsync(
                    id: createdId,
                    locationId: LocationsTestData.Location2Id,
                    appointmentTypeId: null,
                    availableDate: new DateTime(2028, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                    fromTime: new TimeOnly(13, 0),
                    toTime: new TimeOnly(14, 0),
                    bookingStatusId: BookingStatus.Reserved);

                updated.LocationId.ShouldBe(LocationsTestData.Location2Id);
                updated.AppointmentTypeId.ShouldBeNull();
                updated.AvailableDate.Date.ShouldBe(new DateTime(2028, 3, 2));
                updated.FromTime.ShouldBe(new TimeOnly(13, 0));
                updated.ToTime.ShouldBe(new TimeOnly(14, 0));
                updated.BookingStatusId.ShouldBe(BookingStatus.Reserved);
            }
        });
    }

    [Fact]
    public async Task Manager_CreateAsync_DoesNotEnforceOverlapDetection()
    {
        // Pin thin-manager intent (Known Gotcha #2): the manager does NOT
        // check for time overlaps with existing slots. Two manager-created
        // slots in the same location/date with overlapping time windows
        // both succeed.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var date = new DateTime(2028, 4, 1, 0, 0, 0, DateTimeKind.Utc);

                var first = await _slotManager.CreateAsync(
                    locationId: LocationsTestData.Location1Id,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    availableDate: date,
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available);

                var overlapping = await _slotManager.CreateAsync(
                    locationId: LocationsTestData.Location1Id,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    availableDate: date,
                    fromTime: new TimeOnly(9, 30),
                    toTime: new TimeOnly(10, 30),
                    bookingStatusId: BookingStatus.Available);

                first.Id.ShouldNotBe(overlapping.Id);
                overlapping.Id.ShouldNotBe(Guid.Empty);
            }
        });
    }
}
