using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

/// <summary>
/// Seeds the canonical 6 IME appointment types used in California workers'-comp evaluations.
/// Host-scoped (no IMultiTenant); idempotent via per-row upsert-by-ID. GUIDs match
/// <see cref="CaseEvaluationSeedIds.AppointmentTypes"/> so other seeders (Locations) and
/// tests can reference them by name.
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
        (CaseEvaluationSeedIds.AppointmentTypes.Qme,
            "Qualified Medical Examination (QME)",
            "Single-physician medical-legal evaluation under California Labor Code Section 4060."),
        (CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
            "Panel QME",
            "Three-name panel-based QME evaluation per California Labor Code Section 4062.2."),
        (CaseEvaluationSeedIds.AppointmentTypes.Ame,
            "Agreed Medical Examination (AME)",
            "Mutually-agreed-upon medical evaluator selected by the parties."),
        (CaseEvaluationSeedIds.AppointmentTypes.RecordReview,
            "Record Review",
            "Records-only review without an in-person examination."),
        (CaseEvaluationSeedIds.AppointmentTypes.Deposition,
            "Deposition",
            "Sworn testimony of a medical evaluator outside the courtroom."),
        (CaseEvaluationSeedIds.AppointmentTypes.SupplementalMedicalReport,
            "Supplemental Medical Report",
            "Follow-up report addressing additional records or questions after a prior evaluation."),
    };
}
