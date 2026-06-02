using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
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

        foreach (var (id, name, description, eval, maxTime) in Seeds)
        {
            var existing = await _repository.FindAsync(id);
            if (existing != null)
            {
                // Backfill the classification onto rows seeded before these
                // columns existed; skip when already correct so admin edits
                // (once an admin UI lands) are not clobbered on every run.
                if (existing.EvaluationType != eval || existing.MaxTimeCategory != maxTime)
                {
                    existing.EvaluationType = eval;
                    existing.MaxTimeCategory = maxTime;
                    await _repository.UpdateAsync(existing, autoSave: false);
                }
                continue;
            }

            await _repository.InsertAsync(
                new AppointmentType(id, name, description, eval, maxTime), autoSave: false);
        }
    }

    private static readonly (Guid Id, string Name, string? Description, EvaluationType Eval, AppointmentMaxTimeCategory MaxTime)[] Seeds =
    {
        (CaseEvaluationSeedIds.AppointmentTypes.Qme,
            "Qualified Medical Examination (QME)",
            "Single-physician medical-legal evaluation under California Labor Code Section 4060.",
            EvaluationType.Both, AppointmentMaxTimeCategory.Pqme),
        (CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
            "Panel QME",
            "Three-name panel-based QME evaluation per California Labor Code Section 4062.2.",
            EvaluationType.Both, AppointmentMaxTimeCategory.Pqme),
        (CaseEvaluationSeedIds.AppointmentTypes.Ame,
            "Agreed Medical Examination (AME)",
            "Mutually-agreed-upon medical evaluator selected by the parties.",
            EvaluationType.Both, AppointmentMaxTimeCategory.Ame),
        (CaseEvaluationSeedIds.AppointmentTypes.RecordReview,
            "Record Review",
            "Records-only review without an in-person examination.",
            EvaluationType.Normal, AppointmentMaxTimeCategory.Other),
        (CaseEvaluationSeedIds.AppointmentTypes.Deposition,
            "Deposition",
            "Sworn testimony of a medical evaluator outside the courtroom.",
            EvaluationType.Normal, AppointmentMaxTimeCategory.Other),
        (CaseEvaluationSeedIds.AppointmentTypes.SupplementalMedicalReport,
            "Supplemental Medical Report",
            "Follow-up report addressing additional records or questions after a prior evaluation.",
            EvaluationType.Re, AppointmentMaxTimeCategory.Other),
    };
}
