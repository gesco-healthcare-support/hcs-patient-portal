using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Owns the invariants the legacy app lacked (its CRUD validated nothing):
/// name uniqueness per appointment type, and protection of the reserved
/// <see cref="AppointmentDocumentType.IsSystem"/> rows from edit/delete. The
/// in-use-before-delete guard is added once documents can reference a type
/// (the AppointmentDocument FK ships in the next slice).
/// </summary>
public class AppointmentDocumentTypeManager : DomainService
{
    protected IAppointmentDocumentTypeRepository _appointmentDocumentTypeRepository;

    public AppointmentDocumentTypeManager(IAppointmentDocumentTypeRepository appointmentDocumentTypeRepository)
    {
        _appointmentDocumentTypeRepository = appointmentDocumentTypeRepository;
    }

    public virtual async Task<AppointmentDocumentType> CreateAsync(string name, Guid? appointmentTypeId, bool isActive = true)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength);
        await EnsureNameIsUniqueAsync(name, appointmentTypeId, excludeId: null);

        // Stamp the owning tenant explicitly from the resolved request tenant.
        // ABP auto-populates IMultiTenant.TenantId on insert only for entities
        // whose constructor leaves it unset; this entity's constructor assigns
        // TenantId (defaulting to null when omitted), so a plain create persisted
        // a null-tenant row -- invisible to the tenant-scoped list, and the null
        // tenant also tripped ABP's cross-tenant audit guard, leaving CreatorId
        // null. The data seeder stamps its tenant the same way.
        var entity = new AppointmentDocumentType(
            GuidGenerator.Create(),
            name,
            appointmentTypeId,
            isActive,
            isSystem: false,
            tenantId: CurrentTenant.Id);
        return await _appointmentDocumentTypeRepository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentDocumentType> UpdateAsync(Guid id, string name, Guid? appointmentTypeId, bool isActive)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength);

        var entity = await _appointmentDocumentTypeRepository.GetAsync(id);
        EnsureNotSystem(entity);
        await EnsureNameIsUniqueAsync(name, appointmentTypeId, excludeId: id);

        entity.Name = name;
        entity.AppointmentTypeId = appointmentTypeId;
        entity.IsActive = isActive;
        return await _appointmentDocumentTypeRepository.UpdateAsync(entity);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _appointmentDocumentTypeRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        EnsureNotSystem(entity);
        await _appointmentDocumentTypeRepository.DeleteAsync(entity);
    }

    private static void EnsureNotSystem(AppointmentDocumentType entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeSystemReadOnly);
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? appointmentTypeId, Guid? excludeId)
    {
        if (await _appointmentDocumentTypeRepository.NameExistsAsync(name, appointmentTypeId, excludeId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeNameAlreadyExists);
        }
    }
}
