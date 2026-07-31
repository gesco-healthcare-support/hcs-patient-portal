using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// IP4 (2026-06-05) domain-service tests for <see cref="LocationManager"/>'s integrity guards:
/// duplicate-name (case-insensitive, global since Location is host-scoped), non-negative
/// ParkingFee, ZipCode format, and the pre-delete reference count. The concrete
/// EfCoreLocationManagerTests subclass under EntityFrameworkCore.Tests supplies
/// CaseEvaluationEntityFrameworkCoreTestModule so the SQLite + repository wiring is in place
/// (mirrors AppointmentManagerTests). Each DB-touching manager call runs inside
/// WithUnitOfWorkAsync so the repository's DbContext stays alive for the duration of the call
/// (the manager queries the DB, unlike the validation-only AppointmentManager tests). The
/// reference-count guard is a COUNT query, so it is testable on SQLite without DB-level FK
/// enforcement (which the in-memory test connection ignores -- the reason the old AppService
/// FK-edge tests were skipped).
/// </summary>
public abstract class LocationManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly LocationManager _locationManager;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;

    protected LocationManagerTests()
    {
        _locationManager = GetRequiredService<LocationManager>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string UniqueName(string tag) => $"TEST-{tag}-{Guid.NewGuid():N}";

    // ------------------------------------------------------------------------
    // Duplicate-name guard (global; case-insensitive; excludes self on update)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Manager_CreateAsync_WithDuplicateName_ThrowsLocationDuplicateName()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: LocationsTestData.Location1Name, parkingFee: 1.00m, isActive: true)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationDuplicateName);
    }

    [Fact]
    public async Task Manager_CreateAsync_WithDuplicateName_IsCaseInsensitive()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: LocationsTestData.Location1Name.ToUpperInvariant(), parkingFee: 1.00m, isActive: true)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationDuplicateName);
    }

    [Fact]
    public async Task Manager_UpdateAsync_ToAnotherLocationsName_Throws()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _locationManager.UpdateAsync(
                id: LocationsTestData.Location2Id, stateId: null, appointmentTypeIds: new List<Guid>(),
                name: LocationsTestData.Location1Name, parkingFee: 1.00m, isActive: true)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationDuplicateName);
    }

    [Fact]
    public async Task Manager_UpdateAsync_KeepingOwnName_DoesNotThrowDuplicate()
    {
        // Create a throwaway location, then re-save it with its OWN name: the
        // exclude-self branch must keep the duplicate guard from firing.
        var id = Guid.Empty;
        var name = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var created = await _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: UniqueName("Self"), parkingFee: 1.00m, isActive: true);
            id = created.Id;
            name = created.Name;
        });

        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _locationManager.UpdateAsync(
                id: id, stateId: null, appointmentTypeIds: new List<Guid>(),
                name: name, parkingFee: 2.00m, isActive: true);
            result.Name.ShouldBe(name);
        });
    }

    // ------------------------------------------------------------------------
    // ParkingFee >= 0
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Manager_CreateAsync_WithNegativeParkingFee_Throws()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: UniqueName("NegFee"), parkingFee: -1.00m, isActive: true)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationParkingFeeNegative);
    }

    // ------------------------------------------------------------------------
    // ZipCode format (5 or ZIP+4; blank allowed)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Manager_CreateAsync_WithInvalidZipCode_Throws()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
            _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: UniqueName("BadZip"), parkingFee: 1.00m, isActive: true, zipCode: "ABCDE")));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationZipCodeInvalid);
    }

    [Fact]
    public async Task Manager_CreateAsync_WithValidZipPlus4_Succeeds()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: UniqueName("ZipOk"), parkingFee: 1.00m, isActive: true, zipCode: "90001-1234");

            result.ShouldNotBeNull();
            result.ZipCode.ShouldBe("90001-1234");
        });
    }

    // ------------------------------------------------------------------------
    // Pre-delete reference count (Appointment + DoctorAvailability, by LocationId)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Manager_EnsureCanDeleteAsync_WhenReferencedByAvailability_Throws()
    {
        var locationId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            // Database-per-office: a Location and its referencing rows live in the same
            // office database, so seed both inside one office context.
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var location = await _locationManager.CreateAsync(
                    stateId: null, appointmentTypeIds: new List<Guid>(),
                    name: UniqueName("RefByDa"), parkingFee: 1.00m, isActive: true);
                locationId = location.Id;

                await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                    id: Guid.NewGuid(),
                    locationId: location.Id,
                    availableDate: new DateTime(2026, 5, 1),
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available), autoSave: true);
            }
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _locationManager.EnsureCanDeleteAsync(locationId);
            }
        }));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationInUse);
    }

    [Fact]
    public async Task Manager_EnsureCanDeleteAsync_WhenReferencedByAppointment_Throws()
    {
        var locationId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            // Database-per-office: a Location and its referencing rows live in the same
            // office database, so seed both inside one office context.
            var availabilityId = Guid.NewGuid();
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var location = await _locationManager.CreateAsync(
                    stateId: null, appointmentTypeIds: new List<Guid>(),
                    name: UniqueName("RefByAppt"), parkingFee: 1.00m, isActive: true);
                locationId = location.Id;

                var slot = new DoctorAvailability(
                    id: availabilityId,
                    locationId: location.Id,
                    availableDate: new DateTime(2026, 5, 2),
                    fromTime: new TimeOnly(11, 0),
                    toTime: new TimeOnly(12, 0),
                    bookingStatusId: BookingStatus.Booked);
                slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
                await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);

                await _appointmentRepository.InsertAsync(new Appointment(
                    id: Guid.NewGuid(),
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: location.Id,
                    doctorAvailabilityId: availabilityId,
                    appointmentDate: new DateTime(2026, 5, 2),
                    requestConfirmationNumber: "A99997",
                    appointmentStatus: AppointmentStatusType.Pending), autoSave: true);
            }
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _locationManager.EnsureCanDeleteAsync(locationId);
            }
        }));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.LocationInUse);
    }

    [Fact]
    public async Task Manager_EnsureCanDeleteAsync_WhenUnreferenced_DoesNotThrow()
    {
        var locationId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            var location = await _locationManager.CreateAsync(
                stateId: null, appointmentTypeIds: new List<Guid>(),
                name: UniqueName("Unreferenced"), parkingFee: 1.00m, isActive: true);
            locationId = location.Id;
        });

        await Should.NotThrowAsync(() => WithUnitOfWorkAsync(() =>
            _locationManager.EnsureCanDeleteAsync(locationId)));
    }
}
