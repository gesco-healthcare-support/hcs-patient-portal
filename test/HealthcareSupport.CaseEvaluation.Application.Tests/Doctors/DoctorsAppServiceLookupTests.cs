using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// The reads of <see cref="DoctorsAppService"/> that <c>DoctorApplicationTests</c> does not reach:
/// the doctor with its navigation properties, and the office, appointment-type and location
/// lookups. Each lookup runs with a non-matching row present, so a filter that matched everything
/// would fail. All names are synthetic.
/// </summary>
public abstract class DoctorsAppServiceLookupTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDoctorsAppService _doctors;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorsAppServiceLookupTests()
    {
        _doctors = GetRequiredService<IDoctorsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<T> InTenant<T>(Guid? tenantId, Func<Task<T>> call)
    {
        using (_currentTenant.Change(tenantId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    [Fact]
    public async Task An_office_reads_its_doctor_with_navigation_properties()
    {
        var result = await InTenant(TenantsTestData.TenantARef,
            () => _doctors.GetWithNavigationPropertiesAsync(DoctorsTestData.Doctor1Id));

        result.Doctor.Id.ShouldBe(DoctorsTestData.Doctor1Id);
    }

    [Fact]
    public async Task The_lookups_return_only_what_matches_the_filter()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await InTenant(null, async () =>
        {
            var types = GetRequiredService<IRepository<AppointmentType, Guid>>();
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), $"Synthetic Type {token}"), autoSave: true);
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), "Synthetic Unmatched Type"), autoSave: true);
            var locations = GetRequiredService<IRepository<Location, Guid>>();
            await locations.InsertAsync(new Location(Guid.NewGuid(), null, $"Synthetic Location {token}", 0m, isActive: true), autoSave: true);
            await locations.InsertAsync(new Location(Guid.NewGuid(), null, "Synthetic Unmatched Location", 0m, isActive: true), autoSave: true);
            return true;
        });

        var offices = await InTenant(null, () => _doctors.GetTenantLookupAsync(
            new LookupRequestDto { Filter = TenantsTestData.TenantAName, MaxResultCount = 10 }));
        var types = await InTenant(null, () => _doctors.GetAppointmentTypeLookupAsync(
            new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var locations = await InTenant(null, () => _doctors.GetLocationLookupAsync(
            new LookupRequestDto { Filter = token, MaxResultCount = 10 }));

        offices.Items.ShouldHaveSingleItem().Id.ShouldBe(TenantsTestData.TenantARef);
        types.TotalCount.ShouldBe(1);
        types.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Type {token}");
        locations.TotalCount.ShouldBe(1);
        locations.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Location {token}");
    }
}
