using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorManager : DomainService
{
    protected IAppointmentAccessorRepository _appointmentAccessorRepository;

    public AppointmentAccessorManager(IAppointmentAccessorRepository appointmentAccessorRepository)
    {
        _appointmentAccessorRepository = appointmentAccessorRepository;
    }

    public virtual async Task<AppointmentAccessor> CreateAsync(Guid identityUserId, Guid appointmentId, AccessType accessTypeId)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(accessTypeId, nameof(accessTypeId));
        var appointmentAccessor = new AppointmentAccessor(GuidGenerator.Create(), identityUserId, appointmentId, accessTypeId);
        return await _appointmentAccessorRepository.InsertAsync(appointmentAccessor);
    }

    public virtual async Task<AppointmentAccessor> UpdateAsync(Guid id, Guid identityUserId, Guid appointmentId, AccessType accessTypeId)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(accessTypeId, nameof(accessTypeId));
        var appointmentAccessor = await _appointmentAccessorRepository.GetAsync(id);
        appointmentAccessor.IdentityUserId = identityUserId;
        appointmentAccessor.AppointmentId = appointmentId;
        appointmentAccessor.AccessTypeId = accessTypeId;
        return await _appointmentAccessorRepository.UpdateAsync(appointmentAccessor);
    }
}