using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

/// <summary>
/// Seeds the 3 appointment types offered in California workers'-comp evaluations: AME, IME,
/// PQME (AF1, 2026-06-03). Host-scoped (no IMultiTenant); idempotent via per-row upsert-by-ID.
/// GUIDs match <see cref="CaseEvaluationSeedIds.AppointmentTypes"/> so other seeders (Locations)
/// and tests can reference them by name. The UI shows the full label; code keys off the seed
/// GUID, not the display name.
/// </summary>
public class AppointmentTypeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<AppointmentType, Guid> _repository;

    public AppointmentTypeDataSeedContributor(IRepository<AppointmentType, Guid> repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        foreach (var (id, name, description) in Seeds)
        {
            var existing = await _repository.FindAsync(id);
            if (existing != null)
            {
                continue;
            }

            await _repository.InsertAsync(new AppointmentType(id, name, description), autoSave: false);
        }
    }

    private static readonly (Guid Id, string Name, string? Description)[] Seeds =
    {
        (CaseEvaluationSeedIds.AppointmentTypes.Ame,
            "Agreed Medical Examination (AME)",
            "Mutually-agreed-upon medical evaluator selected by the parties."),
        (CaseEvaluationSeedIds.AppointmentTypes.Ime,
            "Independent Medical Examination (IME)",
            "Independent medical-legal evaluation by a neutral physician outside the panel process."),
        (CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
            "Panel Qualified Medical Examination (PQME)",
            "Three-name panel-based qualified medical evaluation per California Labor Code Section 4062.2."),
    };
}
