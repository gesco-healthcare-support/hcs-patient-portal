using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// Seeds 2 SYNTHETIC demo locations so a fresh dev DB is walkable. Real HCS clinic
/// addresses are deployment data (per-tenant operational), not host seed -- per the
/// Wave 0 plan, that import path is post-MVP. Both demo rows reference California
/// and the QME appointment type. Host-scoped; idempotent via simple count-guard
/// (this seed is finite and replaced by deployment data).
/// </summary>
public class LocationDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Location, Guid> _repository;

    public LocationDataSeedContributor(IRepository<Location, Guid> repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        if (await _repository.GetCountAsync() > 0)
        {
            return;
        }

        await _repository.InsertAsync(
            new Location(
                id: CaseEvaluationSeedIds.Locations.DemoClinicNorth,
                stateId: CaseEvaluationSeedIds.States.California,
                appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Qme,
                name: "Demo Clinic North",
                parkingFee: 0m,
                isActive: true,
                address: "100 Demo Plaza",
                city: "Los Angeles",
                zipCode: "90001"),
            autoSave: false);

        await _repository.InsertAsync(
            new Location(
                id: CaseEvaluationSeedIds.Locations.DemoClinicSouth,
                stateId: CaseEvaluationSeedIds.States.California,
                appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Qme,
                name: "Demo Clinic South",
                parkingFee: 0m,
                isActive: true,
                address: "200 Demo Way",
                city: "San Diego",
                zipCode: "92101"),
            autoSave: false);
    }
}
