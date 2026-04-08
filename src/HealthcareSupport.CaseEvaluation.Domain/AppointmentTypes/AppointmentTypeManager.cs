using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeManager : DomainService
{
    protected IAppointmentTypeRepository _appointmentTypeRepository;

    public AppointmentTypeManager(IAppointmentTypeRepository appointmentTypeRepository)
    {
        _appointmentTypeRepository = appointmentTypeRepository;
    }

    public virtual async Task<AppointmentType> CreateAsync(string name, string? description = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength);
        var appointmentType = new AppointmentType(GuidGenerator.Create(), name, description);
        return await _appointmentTypeRepository.InsertAsync(appointmentType);
    }

    public virtual async Task<AppointmentType> UpdateAsync(Guid id, string name, string? description = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength);
        var appointmentType = await _appointmentTypeRepository.GetAsync(id);
        appointmentType.Name = name;
        appointmentType.Description = description;
        return await _appointmentTypeRepository.UpdateAsync(appointmentType);
    }
}