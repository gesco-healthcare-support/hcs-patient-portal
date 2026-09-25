using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Saas;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// Seeds the ONE synthetic clinic of the synthetic TEST office (<see cref="OfficeSeedData"/>), so
/// that office is walkable end to end. Every other office starts with no location: a real practice
/// adds its own on the Locations page, and a seeded clinic there would be a fake address in a real
/// office. Office-scoped; idempotent via a count guard.
/// </summary>
public class LocationDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Location, Guid> _repository;
    private readonly ITenantStore _tenantStore;

    public LocationDataSeedContributor(IRepository<Location, Guid> repository, ITenantStore tenantStore)
    {
        _repository = repository;
        _tenantStore = tenantStore;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Per-office (db-per-office): locations live in the office database; the host has none.
        if (context?.TenantId == null)
        {
            return;
        }

        var tenant = await _tenantStore.FindAsync(context.TenantId.Value);
        if (OfficeSeedData.FindByTenantName(tenant?.Name) == null)
        {
            return;
        }

        if (await _repository.GetCountAsync() > 0)
        {
            return;
        }

        var clinic = new Location(
            id: CaseEvaluationSeedIds.Locations.TestClinic,
            stateId: CaseEvaluationSeedIds.States.California,
            name: "TEST Clinic",
            parkingFee: 0m,
            isActive: true,
            address: "100 TEST Plaza",
            city: "Los Angeles",
            zipCode: "90001");
        clinic.AddAppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ame);
        await _repository.InsertAsync(clinic, autoSave: false);
    }
}
