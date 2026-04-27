using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

public abstract class LocationsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ILocationsAppService _locationsAppService;
    private readonly ILocationRepository _locationRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;

    protected LocationsAppServiceTests()
    {
        _locationsAppService = GetRequiredService<ILocationsAppService>();
        _locationRepository = GetRequiredService<ILocationRepository>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededLocation()
    {
        var result = await _locationsAppService.GetAsync(LocationsTestData.Location1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(LocationsTestData.Location1Id);
        result.Name.ShouldBe(LocationsTestData.Location1Name);
        result.ParkingFee.ShouldBe(LocationsTestData.Location1ParkingFee);
        result.IsActive.ShouldBe(LocationsTestData.Location1IsActive);
        result.StateId.ShouldBe(LocationsTestData.State1Id);
        result.AppointmentTypeId.ShouldBe(LocationsTestData.AppointmentType1Id);
    }

    [Fact]
    public async Task GetListAsync_WithNoFilter_ReturnsAllThreeSeededLocations()
    {
        var result = await _locationsAppService.GetListAsync(new GetLocationsInput());

        result.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        result.Items.Any(x => x.Location.Id == LocationsTestData.Location1Id).ShouldBeTrue();
        result.Items.Any(x => x.Location.Id == LocationsTestData.Location2Id).ShouldBeTrue();
        result.Items.Any(x => x.Location.Id == LocationsTestData.Location3Id).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewLocation()
    {
        var input = new LocationCreateDto
        {
            Name = $"TEST-CreateTarget-{Guid.NewGuid():N}",
            ParkingFee = 7.50m,
            IsActive = true,
            Address = "TEST-123 Synthetic St",
            City = "TEST-City",
            ZipCode = "90001"
        };

        var created = await _locationsAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);
        created.ParkingFee.ShouldBe(input.ParkingFee);
        created.IsActive.ShouldBeTrue();

        var persisted = await _locationRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
        persisted!.ParkingFee.ShouldBe(input.ParkingFee);
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        var existing = await _locationRepository.GetAsync(LocationsTestData.Location2Id);
        var update = new LocationUpdateDto
        {
            Name = "TEST-Location2-Updated",
            ParkingFee = 99.00m,
            IsActive = existing.IsActive,
            Address = existing.Address,
            City = existing.City,
            ZipCode = existing.ZipCode,
            StateId = existing.StateId,
            AppointmentTypeId = existing.AppointmentTypeId,
            ConcurrencyStamp = existing.ConcurrencyStamp
        };

        var result = await _locationsAppService.UpdateAsync(LocationsTestData.Location2Id, update);

        result.Name.ShouldBe("TEST-Location2-Updated");
        result.ParkingFee.ShouldBe(99.00m);

        // Restore seed values so sibling tests see the canonical Location2.
        var current = await _locationRepository.GetAsync(LocationsTestData.Location2Id);
        current.Name = LocationsTestData.Location2Name;
        current.ParkingFee = LocationsTestData.Location2ParkingFee;
        await _locationRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLocation()
    {
        var created = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"TEST-DeleteTarget-{Guid.NewGuid():N}",
            ParkingFee = 1.00m,
            IsActive = true
        });

        await _locationsAppService.DeleteAsync(created.Id);

        (await _locationRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Filter combinatorics (one [Fact] per distinct filter shape:
    // string partial-match, decimal range, bool, Guid).
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FilterByName_ReturnsMatchingLocation()
    {
        var result = await _locationsAppService.GetListAsync(new GetLocationsInput
        {
            Name = LocationsTestData.Location1Name
        });

        result.Items.Any(x => x.Location.Id == LocationsTestData.Location1Id).ShouldBeTrue();
        result.Items.All(x => x.Location.Name != null
                              && x.Location.Name.Contains(LocationsTestData.Location1Name))
              .ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FilterByParkingFeeRange_ReturnsOnlyLocationsInRange()
    {
        // Range 4..10 brackets Location2 (5.00), excludes Location1 (0) and Location3 (15).
        var result = await _locationsAppService.GetListAsync(new GetLocationsInput
        {
            ParkingFeeMin = 4.00m,
            ParkingFeeMax = 10.00m
        });

        result.Items.Any(x => x.Location.Id == LocationsTestData.Location2Id).ShouldBeTrue();
        result.Items.Any(x => x.Location.Id == LocationsTestData.Location1Id).ShouldBeFalse();
        result.Items.Any(x => x.Location.Id == LocationsTestData.Location3Id).ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_FilterByIsActive_ReturnsOnlyInactiveLocations()
    {
        var result = await _locationsAppService.GetListAsync(new GetLocationsInput
        {
            IsActive = false
        });

        result.Items.Any(x => x.Location.Id == LocationsTestData.Location3Id).ShouldBeTrue();
        result.Items.All(x => x.Location.IsActive == false).ShouldBeTrue();
    }

    // ------------------------------------------------------------------------
    // Nav-prop join (populated State + AppointmentType)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsLocationWithPopulatedNavProps()
    {
        var result = await _locationsAppService.GetWithNavigationPropertiesAsync(LocationsTestData.Location1Id);

        result.ShouldNotBeNull();
        result.Location.Id.ShouldBe(LocationsTestData.Location1Id);
        result.State.ShouldNotBeNull();
        result.State!.Id.ShouldBe(LocationsTestData.State1Id);
        result.State.Name.ShouldBe(LocationsTestData.State1Name);
        result.AppointmentType.ShouldNotBeNull();
        result.AppointmentType!.Id.ShouldBe(LocationsTestData.AppointmentType1Id);
        result.AppointmentType.Name.ShouldBe(LocationsTestData.AppointmentType1Name);
    }

    // ------------------------------------------------------------------------
    // Bulk delete
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteByIdsAsync_RemovesMultipleLocations()
    {
        var a = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"TEST-BulkDel-A-{Guid.NewGuid():N}",
            ParkingFee = 1.00m,
            IsActive = true
        });
        var b = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"TEST-BulkDel-B-{Guid.NewGuid():N}",
            ParkingFee = 2.00m,
            IsActive = true
        });

        await _locationsAppService.DeleteByIdsAsync(new List<Guid> { a.Id, b.Id });

        (await _locationRepository.FindAsync(a.Id)).ShouldBeNull();
        (await _locationRepository.FindAsync(b.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesOnlyLocationsMatchingFilter()
    {
        // Tag kept short -- combined with "-1"/"-2" suffix must stay under
        // LocationConsts.NameMaxLength (50) for [StringLength] on CreateDto.
        var tag = $"DA-{Guid.NewGuid():N}";
        var v1 = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"{tag}-1",
            ParkingFee = 1.00m,
            IsActive = true
        });
        var v2 = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"{tag}-2",
            ParkingFee = 1.00m,
            IsActive = true
        });

        await _locationsAppService.DeleteAllAsync(new GetLocationsInput { Name = tag });

        (await _locationRepository.FindAsync(v1.Id)).ShouldBeNull();
        (await _locationRepository.FindAsync(v2.Id)).ShouldBeNull();
        // Seeded Locations must survive the filtered delete.
        (await _locationRepository.FindAsync(LocationsTestData.Location1Id)).ShouldNotBeNull();
        (await _locationRepository.FindAsync(LocationsTestData.Location2Id)).ShouldNotBeNull();
        (await _locationRepository.FindAsync(LocationsTestData.Location3Id)).ShouldNotBeNull();
    }

    // ------------------------------------------------------------------------
    // Delete-constraint edges (inbound NoAction FKs on Location from
    // DoctorAvailability and Appointment). Currently skipped -- the shared
    // SQLite in-memory test connection ignores FK enforcement even after
    // "Foreign Keys=True" in the connection string AND an explicit
    // `PRAGMA foreign_keys = ON` on Open(); suspected cause is that ABP / EF
    // pool a wrapper around the manually-opened connection that bypasses the
    // opt-in. Test bodies below encode target behaviour and flip live when
    // FEAT-14 is closed.
    // ------------------------------------------------------------------------

    [Fact(Skip = "KNOWN GAP: SQLite in-memory test DB does not enforce foreign-key constraints "
              + "against the shared manually-opened connection, despite \"Foreign Keys=True\" + "
              + "explicit PRAGMA in CaseEvaluationEntityFrameworkCoreTestModule. Test body encodes "
              + "target behaviour and will flip live once test-infra FK enforcement is fixed. "
              + "Tracked: docs/issues/INCOMPLETE-FEATURES.md#test-fk-enforcement")]
    public async Task DeleteAsync_WhenLocationReferencedByDoctorAvailability_Throws()
    {
        var disposable = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"TEST-FkBlocked-Da-{Guid.NewGuid():N}",
            ParkingFee = 1.00m,
            IsActive = true
        });

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // autoSave forces the InsertAsync to persist before the outer
            // test method's ambient UoW commits. Without it the child row is
            // queued only, so the subsequent DeleteAsync on `disposable`
            // wouldn't trigger the FK violation we're asserting.
            await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                id: Guid.NewGuid(),
                locationId: disposable.Id,
                appointmentTypeId: null,
                availableDate: new DateTime(2026, 5, 1),
                fromTime: new TimeOnly(9, 0),
                toTime: new TimeOnly(10, 0),
                bookingStatusId: BookingStatus.Available), autoSave: true);
        }

        await Should.ThrowAsync<Exception>(async () =>
            await _locationsAppService.DeleteAsync(disposable.Id));
    }

    [Fact(Skip = "KNOWN GAP: SQLite in-memory test DB does not enforce foreign-key constraints "
              + "against the shared manually-opened connection, despite \"Foreign Keys=True\" + "
              + "explicit PRAGMA in CaseEvaluationEntityFrameworkCoreTestModule. Test body encodes "
              + "target behaviour and will flip live once test-infra FK enforcement is fixed. "
              + "Tracked: docs/issues/INCOMPLETE-FEATURES.md#test-fk-enforcement")]
    public async Task DeleteAsync_WhenLocationReferencedByAppointment_Throws()
    {
        var disposable = await _locationsAppService.CreateAsync(new LocationCreateDto
        {
            Name = $"FK-Appt-{Guid.NewGuid():N}",
            ParkingFee = 1.00m,
            IsActive = true
        });

        var availabilityId = Guid.NewGuid();
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // autoSave persists each child row immediately so the subsequent
            // DeleteAsync on `disposable` raises the NoAction FK violation we
            // are asserting.
            await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                id: availabilityId,
                locationId: disposable.Id,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                availableDate: new DateTime(2026, 5, 2),
                fromTime: new TimeOnly(11, 0),
                toTime: new TimeOnly(12, 0),
                bookingStatusId: BookingStatus.Booked), autoSave: true);

            await _appointmentRepository.InsertAsync(new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: IdentityUsersTestData.Patient1UserId,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: disposable.Id,
                doctorAvailabilityId: availabilityId,
                appointmentDate: new DateTime(2026, 5, 2),
                requestConfirmationNumber: "A99998",
                appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
        }

        await Should.ThrowAsync<Exception>(async () =>
            await _locationsAppService.DeleteAsync(disposable.Id));
    }

    // ------------------------------------------------------------------------
    // Host-only scoping (Location is NOT IMultiTenant; tenant context should
    // not filter it out).
    // ------------------------------------------------------------------------

    [Fact]
    public async Task LocationsAreVisible_FromTenantContext()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _locationsAppService.GetListAsync(new GetLocationsInput());

            result.Items.Any(x => x.Location.Id == LocationsTestData.Location1Id).ShouldBeTrue();
            result.Items.Any(x => x.Location.Id == LocationsTestData.Location2Id).ShouldBeTrue();
            result.Items.Any(x => x.Location.Id == LocationsTestData.Location3Id).ShouldBeTrue();
        }
    }
}
