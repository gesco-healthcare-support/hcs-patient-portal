using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Restores the catalog/navigation-resolution tests that were skipped because their
/// catalogs became IMultiTenant per office and the single-shared-SQLite rig could not
/// seed per-tenant catalogs (the same fixed GUID collides across tenants in one
/// database). On the multi-office harness each office owns its database, so its catalogs
/// resolve under its own tenant context -- and a different office never sees them.
///
/// Ports (and replaces the [Fact(Skip)] stubs of):
///   LocationsAppServiceTests.LocationsAreVisible_FromTenantContext
///   DoctorAvailabilitiesAppServiceTests.GetWithNavigationPropertiesAsync_ResolvesLocationAndAppointmentType
///   DoctorAvailabilitiesAppServiceTests.GetWithNavigationPropertiesAsync_WhenAppointmentTypesEmpty_ReturnsEmptyList
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeCatalogResolutionTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocationsAppService _locationsAppService;
    private readonly IDoctorAvailabilitiesAppService _doctorAvailabilitiesAppService;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IAppointmentEmployerDetailsAppService _employerDetailsAppService;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetailRepository;

    public MultiOfficeCatalogResolutionTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _locationsAppService = GetRequiredService<ILocationsAppService>();
        _doctorAvailabilitiesAppService = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _employerDetailsAppService = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        _employerDetailRepository = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
    }

    [Fact]
    public async Task Location_list_isVisibleInItsOwnOffice_andScopedToIt()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var result = await _locationsAppService.GetListAsync(
                    new GetLocationsInput { MaxResultCount = 1000 });
                var ids = result.Items.Select(x => x.Location.Id).ToList();

                ids.ShouldContain(officeA.LocationId);
                ids.ShouldNotContain(officeB.LocationId);
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task DoctorAvailability_nav_resolvesLocationAndAppointmentType()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var result = await _doctorAvailabilitiesAppService
                    .GetWithNavigationPropertiesAsync(officeA.DoctorAvailabilityId);

                result.ShouldNotBeNull();
                result.DoctorAvailability.Id.ShouldBe(officeA.DoctorAvailabilityId);
                result.Location.ShouldNotBeNull();
                result.Location!.Id.ShouldBe(officeA.LocationId);
                result.AppointmentTypes.Count.ShouldBe(1);
                result.AppointmentTypes.Single().Id.ShouldBe(officeA.AppointmentTypeId);
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task DoctorAvailability_nav_withNoAppointmentTypes_returnsEmptyList()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var looseSlotId = Guid.NewGuid();

        // A "loose" slot accepts any type -> no join rows. Seed it in office A's
        // database, reusing that office's already-seeded location.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var slot = new DoctorAvailability(
                    id: looseSlotId,
                    locationId: officeA.LocationId,
                    availableDate: new DateTime(2026, 4, 1),
                    fromTime: new TimeOnly(11, 0),
                    toTime: new TimeOnly(12, 0),
                    bookingStatusId: BookingStatus.Available);
                slot.TenantId = officeA.OfficeId;
                await _doctorAvailabilityRepository.InsertAsync(slot, autoSave: true);
            }
        }, requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var result = await _doctorAvailabilitiesAppService
                    .GetWithNavigationPropertiesAsync(looseSlotId);

                result.ShouldNotBeNull();
                result.DoctorAvailability.Id.ShouldBe(looseSlotId);
                result.AppointmentTypes.ShouldBeEmpty();
                result.Location.ShouldNotBeNull();
            }
        }, requiresNew: true);
    }

    // Ports AppointmentEmployerDetailsAppServiceTests.
    // GetWithNavigationPropertiesAsync_ReturnsDetailWithPopulatedState (State is now
    // IMultiTenant per office). Seeds a detail in office A referencing that office's
    // already-seeded appointment + state, then resolves its State navigation.
    [Fact]
    public async Task EmployerDetail_nav_resolvesOfficeScopedState()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var detailId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var detail = new AppointmentEmployerDetail(
                    id: detailId,
                    appointmentId: officeA.AppointmentId,
                    stateId: officeA.StateId,
                    employerName: "TEST-Employer-officeA",
                    occupation: "TEST-Occupation");
                await _employerDetailRepository.InsertAsync(detail, autoSave: true);
            }
        }, requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var result = await _employerDetailsAppService.GetWithNavigationPropertiesAsync(detailId);

                result.ShouldNotBeNull();
                result.AppointmentEmployerDetail.Id.ShouldBe(detailId);
                result.State.ShouldNotBeNull();
                result.State!.Id.ShouldBe(officeA.StateId);
            }
        }, requiresNew: true);
    }

    // Ports AppointmentEmployerDetailsAppServiceTests.GetStateLookupAsync_ReturnsSeededStates
    // -- the state lookup is scoped to the current office.
    [Fact]
    public async Task EmployerDetail_stateLookup_isScopedToTheCurrentOffice()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var result = await _employerDetailsAppService.GetStateLookupAsync(
                    new LookupRequestDto { MaxResultCount = 1000 });
                var ids = result.Items.Select(x => x.Id).ToList();

                ids.ShouldContain(officeA.StateId);
                ids.ShouldNotContain(officeB.StateId);
            }
        }, requiresNew: true);
    }
}
