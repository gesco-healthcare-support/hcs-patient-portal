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
        var appointmentStatus = new AppointmentStatus(GuidGenerator.Create(), name);
        return await _appointmentStatusRepository.InsertAsync(appointmentStatus);
    }

    public virtual async Task<AppointmentStatus> UpdateAsync(Guid id, string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentStatusConsts.NameMaxLength);
        var appointmentStatus = await _appointmentStatusRepository.GetAsync(id);
        appointmentStatus.Name = name;
        return await _appointmentStatusRepository.UpdateAsync(appointmentStatus);
    }
}