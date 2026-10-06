using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Domains;

/// <summary>
/// Guards #764. The catalog (State, AppointmentType, Location, ...) is <c>IMultiTenant</c>
/// and production seeds it inside each office's own database, never in host scope. The
/// integration seed used to put it in host scope under a comment calling the entities
/// "!IMultiTenant", so every join from an office-owned row to its catalog row resolved to
/// nothing and assertions over those joins passed over empty data.
///
/// These tests pin the seed to the production shape: a catalog row seeded in an office is
/// visible under that office's filter, and invisible under the other office and under host.
/// </summary>
public class CatalogSeedScopeTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Location, Guid> _locations;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypes;
    private readonly IRepository<State, Guid> _states;

    public CatalogSeedScopeTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _locations = GetRequiredService<IRepository<Location, Guid>>();
        _appointmentTypes = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _states = GetRequiredService<IRepository<State, Guid>>();
    }

    private Task<bool> VisibleAsync<TEntity>(IRepository<TEntity, Guid> repo, Guid? tenantId, Guid id)
        where TEntity : class, Volo.Abp.Domain.Entities.IEntity<Guid> =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return (await repo.GetQueryableAsync()).Any(x => x.Id == id);
            }
        });

    [Fact]
    public async Task Location_seeded_in_office_A_is_visible_to_A_only()
    {
        (await VisibleAsync(_locations, TenantsTestData.TenantARef, LocationsTestData.Location1Id)).ShouldBeTrue();
        (await VisibleAsync(_locations, TenantsTestData.TenantBRef, LocationsTestData.Location1Id)).ShouldBeFalse();
        (await VisibleAsync(_locations, null, LocationsTestData.Location1Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Location_seeded_in_office_B_is_visible_to_B_only()
    {
        (await VisibleAsync(_locations, TenantsTestData.TenantBRef, LocationsTestData.Location1TenantBId)).ShouldBeTrue();
        (await VisibleAsync(_locations, TenantsTestData.TenantARef, LocationsTestData.Location1TenantBId)).ShouldBeFalse();
        (await VisibleAsync(_locations, null, LocationsTestData.Location1TenantBId)).ShouldBeFalse();
    }

    [Fact]
    public async Task AppointmentType_and_State_are_per_office_not_host()
    {
        (await VisibleAsync(_appointmentTypes, TenantsTestData.TenantARef, LocationsTestData.AppointmentType1Id)).ShouldBeTrue();
        (await VisibleAsync(_appointmentTypes, TenantsTestData.TenantBRef, LocationsTestData.AppointmentType1Id)).ShouldBeFalse();
        (await VisibleAsync(_appointmentTypes, null, LocationsTestData.AppointmentType1Id)).ShouldBeFalse();

        (await VisibleAsync(_states, TenantsTestData.TenantARef, StatesTestData.State1Id)).ShouldBeTrue();
        (await VisibleAsync(_states, TenantsTestData.TenantBRef, StatesTestData.State1Id)).ShouldBeFalse();
        (await VisibleAsync(_states, null, StatesTestData.State1Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Every_seeded_location_has_a_tenant_stamp()
    {
        var unstamped = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return (await _locations.GetQueryableAsync()).Count();
            }
        });

        // Host scope holds no seeded location; production's host database holds no catalog.
        unstamped.ShouldBe(0);
    }
}
