using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeManager : DomainService
{
    protected IAppointmentTypeRepository _appointmentTypeRepository;
    protected IRepository<Appointment, Guid> _appointmentRepository;

    public AppointmentTypeManager(
        IAppointmentTypeRepository appointmentTypeRepository,
        IRepository<Appointment, Guid> appointmentRepository)
    {
        _appointmentTypeRepository = appointmentTypeRepository;
        _appointmentRepository = appointmentRepository;
    }

    public virtual async Task<AppointmentType> CreateAsync(string name, string? description = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength);
        // Admin-created rows are never system rows.
        var appointmentType = new AppointmentType(GuidGenerator.Create(), name, description, isSystem: false);
        return await _appointmentTypeRepository.InsertAsync(appointmentType);
    }

    public virtual async Task<AppointmentType> UpdateAsync(Guid id, string name, string? description = null)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength);
        var appointmentType = await _appointmentTypeRepository.GetAsync(id);
        EnsureNotSystem(appointmentType);
        appointmentType.Name = name;
        appointmentType.Description = description;
        return await _appointmentTypeRepository.UpdateAsync(appointmentType);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _appointmentTypeRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        EnsureNotSystem(entity);
        await EnsureNotInUseAsync(id);
        await _appointmentTypeRepository.DeleteAsync(entity);
    }

    /// <summary>
    /// Block deleting a type still referenced by an Appointment (via
    /// <see cref="Appointment.AppointmentTypeId"/>). Mirrors the
    /// AppointmentDocumentType in-use guard.
    /// </summary>
    private async Task EnsureNotInUseAsync(Guid id)
    {
        if (await _appointmentRepository.AnyAsync(a => a.AppointmentTypeId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentTypeInUse);
        }
    }

    private static void EnsureNotSystem(AppointmentType entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentTypeSystemReadOnly);
        }
    }
}
