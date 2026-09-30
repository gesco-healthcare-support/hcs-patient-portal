using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Owns the invariants the legacy app lacked (its CRUD validated nothing):
/// name uniqueness per tenant, protection of the reserved
/// <see cref="AppointmentDocumentType.IsSystem"/> rows from edit/delete, and
/// (PR2) an in-use-before-delete guard now that
/// <see cref="AppointmentDocument.AppointmentDocumentTypeId"/> can reference a type.
///
/// <para>#4 (2026-06-19): a category is one record with a M2M set of appointment
/// types (or <see cref="AppointmentDocumentType.AppliesToAll"/>). Create/Update
/// reconcile that set, and uniqueness is now per-tenant (one "Medical Records"
/// per office) rather than per appointment type.</para>
/// </summary>
public class AppointmentDocumentTypeManager : DomainService
{
    protected IAppointmentDocumentTypeRepository _appointmentDocumentTypeRepository;
    protected IRepository<AppointmentDocument, Guid> _appointmentDocumentRepository;

    public AppointmentDocumentTypeManager(
        IAppointmentDocumentTypeRepository appointmentDocumentTypeRepository,
        IRepository<AppointmentDocument, Guid> appointmentDocumentRepository)
    {
        _appointmentDocumentTypeRepository = appointmentDocumentTypeRepository;
        _appointmentDocumentRepository = appointmentDocumentRepository;
    }

    public virtual async Task<AppointmentDocumentType> CreateAsync(
        string name,
        List<Guid> appointmentTypeIds,
        bool appliesToAll,
        bool isActive = true)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength);
        await EnsureNameIsUniqueAsync(name, excludeId: null);

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
            appliesToAll,
            isActive,
            isSystem: false,
            tenantId: CurrentTenant.Id);
        entity.SetAppointmentTypes(appointmentTypeIds ?? new List<Guid>());
        return await _appointmentDocumentTypeRepository.InsertAsync(entity);
    }

    public virtual async Task<AppointmentDocumentType> UpdateAsync(
        Guid id,
        string name,
        List<Guid> appointmentTypeIds,
        bool appliesToAll,
        bool isActive)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength);

        // Load WITH the join set so SetAppointmentTypes reconciles against the
        // current set instead of re-adding everything to an empty collection.
        var entity = await _appointmentDocumentTypeRepository.GetWithAppointmentTypesAsync(id);
        EnsureNotSystem(entity);
        await EnsureNameIsUniqueAsync(name, excludeId: id);

        entity.Name = name;
        entity.AppliesToAll = appliesToAll;
        entity.IsActive = isActive;
        entity.SetAppointmentTypes(appointmentTypeIds ?? new List<Guid>());
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
        await EnsureNotInUseAsync(id);
        await _appointmentDocumentTypeRepository.DeleteAsync(entity);
    }

    /// <summary>
    /// G-03-03 (PR2): block deleting a category still referenced by an uploaded
    /// or queued document (via <see cref="AppointmentDocument.AppointmentDocumentTypeId"/>).
    /// Staff retire the category (IsActive = false) instead, so the type label
    /// on historical documents is preserved. The IMultiTenant filter scopes the
    /// check to the current tenant.
    /// </summary>
    private async Task EnsureNotInUseAsync(Guid id)
    {
        if (await _appointmentDocumentRepository.AnyAsync(d => d.AppointmentDocumentTypeId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeInUse);
        }
    }

    private static void EnsureNotSystem(AppointmentDocumentType entity)
    {
        if (entity.IsSystem)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeSystemReadOnly);
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludeId)
    {
        if (await _appointmentDocumentTypeRepository.NameExistsAsync(name, excludeId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeNameAlreadyExists);
        }
    }
}
