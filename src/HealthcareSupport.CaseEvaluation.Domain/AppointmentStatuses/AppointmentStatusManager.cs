using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class AppointmentStatusManager : DomainService
{
    protected IAppointmentStatusRepository _appointmentStatusRepository;

    public AppointmentStatusManager(IAppointmentStatusRepository appointmentStatusRepository)
    {
        _appointmentStatusRepository = appointmentStatusRepository;
    }

    public virtual async Task<AppointmentStatus> CreateAsync(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentStatusConsts.NameMaxLength);
        // Admin-created rows are never system rows.
        var appointmentStatus = new AppointmentStatus(GuidGenerator.Create(), name, isSystem: false);
        return await _appointmentStatusRepository.InsertAsync(appointmentStatus);
    }

    public virtual async Task<AppointmentStatus> UpdateAsync(Guid id, string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentStatusConsts.NameMaxLength);
        var appointmentStatus = await _appointmentStatusRepository.GetAsync(id);
        EnsureNotSystem(appointmentStatus);
        appointmentStatus.Name = name;
        return await _appointmentStatusRepository.UpdateAsync(appointmentStatus);
    }

    // AppointmentStatus rows are not FK-referenced (appointments carry the
    // AppointmentStatusType enum), so there is no in-use guard -- only the
    // system-row guard applies.
    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _appointmentStatusRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        EnsureNotSystem(entity);
        await _appointmentStatusRepository.DeleteAsync(entity);
    }

    private static void EnsureNotSystem(AppointmentStatus entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentStatusSystemReadOnly);
        }
    }
}
