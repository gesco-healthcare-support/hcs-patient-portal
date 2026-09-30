using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

/// <summary>
/// Prompt 15 (2026-06-15): seeds the redesign's six canonical appointment-status
/// pills into the otherwise-empty AppointmentStatus LOOKUP table so the
/// Configuration > Statuses section has rows to show. Runtime status logic uses
/// the <see cref="HealthcareSupport.CaseEvaluation.Enums.AppointmentStatusType"/>
/// enum, NOT this table -- these rows are display/reference only and are all
/// system-locked (admins may rename but never delete them). Host-scoped (no
/// IMultiTenant); idempotent via per-row upsert-by-ID.
/// </summary>
public class AppointmentStatusDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<AppointmentStatus, Guid> _repository;

    public AppointmentStatusDataSeedContributor(IRepository<AppointmentStatus, Guid> repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Per-office (db-per-office): seed under the active tenant; skip host scope
        // (no catalogs there). Per-office seed execution + ordering is Phase B (B4).
        if (context?.TenantId == null)
        {
            return;
        }

        foreach (var (id, name) in Seeds)
        {
            var existing = await _repository.FindAsync(id);
            if (existing != null)
            {
                continue;
            }

            await _repository.InsertAsync(new AppointmentStatus(id, name, isSystem: true), autoSave: false);
        }
    }

    // The six redesign status pills; AppointmentStatusType's legacy values all
    // bucket into these. All system-locked -- the lookup mirrors the enum for
    // display, it does not drive status logic.
    private static readonly (Guid Id, string Name)[] Seeds =
    {
        (CaseEvaluationSeedIds.AppointmentStatuses.Pending, "Pending"),
        (CaseEvaluationSeedIds.AppointmentStatuses.InfoRequested, "Info Requested"),
        (CaseEvaluationSeedIds.AppointmentStatuses.Approved, "Approved"),
        (CaseEvaluationSeedIds.AppointmentStatuses.Rejected, "Rejected"),
        (CaseEvaluationSeedIds.AppointmentStatuses.Cancelled, "Cancelled"),
        (CaseEvaluationSeedIds.AppointmentStatuses.Rescheduled, "Rescheduled"),
    };
}
