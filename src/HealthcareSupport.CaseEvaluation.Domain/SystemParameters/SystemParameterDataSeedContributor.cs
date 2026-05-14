using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Per-tenant singleton seeder for <c>SystemParameter</c>. Defaults mirror
/// OLD's seed values verbatim for strict parity (Phase 1.1, 2026-05-01).
/// Idempotent: skips if a row already exists in the current tenant scope.
/// </summary>
public class SystemParameterDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ISystemParameterRepository _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public SystemParameterDataSeedContributor(
        ISystemParameterRepository repository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId == null)
        {
            // Host scope: skip; the singleton is per-tenant.
            return;
        }

        using (_currentTenant.Change(context.TenantId))
        {
            var existing = await _repository.GetCurrentTenantAsync();
            if (existing != null)
            {
                return;
            }

            var entity = new SystemParameter(
                id: _guidGenerator.Create(),
                tenantId: context.TenantId,
                appointmentLeadTime: SystemParameterConsts.DefaultAppointmentLeadTime,
                appointmentMaxTimePQME: SystemParameterConsts.DefaultAppointmentMaxTimePQME,
                appointmentMaxTimeAME: SystemParameterConsts.DefaultAppointmentMaxTimeAME,
                appointmentMaxTimeOTHER: SystemParameterConsts.DefaultAppointmentMaxTimeOTHER,
                appointmentCancelTime: SystemParameterConsts.DefaultAppointmentCancelTime,
                appointmentDueDays: SystemParameterConsts.DefaultAppointmentDueDays,
                appointmentDurationTime: SystemParameterConsts.DefaultAppointmentDurationTime,
                autoCancelCutoffTime: SystemParameterConsts.DefaultAutoCancelCutoffTime,
                jointDeclarationUploadCutoffDays: SystemParameterConsts.DefaultJointDeclarationUploadCutoffDays,
                pendingAppointmentOverDueNotificationDays: SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays,
                reminderCutoffTime: SystemParameterConsts.DefaultReminderCutoffTime,
                isCustomField: SystemParameterConsts.DefaultIsCustomField,
                ccEmailIds: null);

            await _repository.InsertAsync(entity, autoSave: true);
        }
    }
}
