using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// <see cref="DoctorPreferredLocationsAppService"/>, which had no test at all: toggling a doctor's
/// preferred location on and off, reading a doctor's list, and the two required ids.
/// </summary>
/// <remarks>
/// The per-doctor read runs with ANOTHER doctor's row present, so a read that ignored the doctor
/// would fail. Runs in office A against the seeded doctor and locations; names are synthetic.
/// </remarks>
public abstract class DoctorPreferredLocationsAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDoctorPreferredLocationsAppService _service;
    private readonly IRepository<DoctorPreferredLocation> _repository;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorPreferredLocationsAppServiceTests()
    {
        _service = GetRequiredService<IDoctorPreferredLocationsAppService>();
        _repository = GetRequiredService<IRepository<DoctorPreferredLocation>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<T> InOfficeA<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task<DoctorPreferredLocationDto> ToggleAsync(Guid doctorId, Guid locationId, bool isActive) =>
        InOfficeA(() => _service.ToggleAsync(new ToggleDoctorPreferredLocationInput
        {
            DoctorId = doctorId,
            LocationId = locationId,
            IsActive = isActive,
        }));

    [Fact]
    public async Task Toggling_creates_the_preference_once_and_then_flips_it()
    {
        var created = await ToggleAsync(DoctorsTestData.Doctor1Id, LocationsTestData.Location1Id, isActive: true);
        var flipped = await ToggleAsync(DoctorsTestData.Doctor1Id, LocationsTestData.Location1Id, isActive: false);

        created.IsActive.ShouldBeTrue();
        created.TenantId.ShouldBe(TenantsTestData.TenantARef);
        flipped.IsActive.ShouldBeFalse();
        var rows = await InOfficeA(() => _repository.GetListAsync(x =>
            x.DoctorId == DoctorsTestData.Doctor1Id && x.LocationId == LocationsTestData.Location1Id));
        rows.ShouldHaveSingleItem().IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task A_doctors_list_holds_only_that_doctors_preferences_in_location_order()
    {
        var secondLocation = await InOfficeA(async () => (await GetRequiredService<IRepository<Location, Guid>>()
            .InsertAsync(new Location(Guid.NewGuid(), null, "Synthetic Preferred Location", 0m, isActive: true), autoSave: true)).Id);
        await ToggleAsync(DoctorsTestData.Doctor1Id, LocationsTestData.Location1Id, isActive: true);
        await ToggleAsync(DoctorsTestData.Doctor1Id, secondLocation, isActive: false);
        // LOAD-BEARING DECOY: another (seeded) doctor's preference. The table has a foreign key to Doctor.
        await InOfficeA(async () => await _repository.InsertAsync(
            new DoctorPreferredLocation(DoctorsTestData.Doctor2Id, LocationsTestData.Location1Id, TenantsTestData.TenantARef), autoSave: true));

        var list = await InOfficeA(() => _service.GetByDoctorAsync(DoctorsTestData.Doctor1Id));

        list.ShouldAllBe(x => x.DoctorId == DoctorsTestData.Doctor1Id);
        list.Select(x => x.LocationId).ShouldBe(new[] { LocationsTestData.Location1Id, secondLocation }.OrderBy(id => id));
    }

    [Fact]
    public async Task A_toggle_must_name_both_a_doctor_and_a_location()
    {
        await Should.ThrowAsync<UserFriendlyException>(() => ToggleAsync(Guid.Empty, LocationsTestData.Location1Id, true));
        await Should.ThrowAsync<UserFriendlyException>(() => ToggleAsync(DoctorsTestData.Doctor1Id, Guid.Empty, true));

        (await InOfficeA(() => _repository.GetListAsync(x => x.DoctorId == DoctorsTestData.Doctor1Id))).ShouldBeEmpty();
    }
}
