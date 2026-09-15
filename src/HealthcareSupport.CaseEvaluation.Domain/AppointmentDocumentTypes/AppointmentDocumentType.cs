using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// G-03-01 (2026-06-03): tenant-scoped master list of document categories.
/// Restores (and extends) the legacy <c>AppointmentDocumentType</c> lookup that
/// was dropped at MVP. Internal staff (IT Admin / Staff Supervisor) curate the
/// list; it drives the picker on document uploads.
///
/// <para>#4 (2026-06-19): a category is now ONE record, curated from the
/// document side. The single per-row <c>AppointmentTypeId</c> was replaced by a
/// many-to-many join (<see cref="AppointmentTypes"/>) plus an explicit
/// <see cref="AppliesToAll"/> flag. So "Medical Records" is a single row offered
/// to several appointment types instead of one duplicate row per type.</para>
///
/// <para>Tenant-scoped per Adrian decision (each office curates its own lists),
/// so this diverges from the host-only AppointmentStatuses lookup it is otherwise
/// modeled on. The join's <c>AppointmentTypeId</c> is a loose reference (the
/// AppointmentType lookup is host-scoped); <see cref="AppliesToAll"/> marks a
/// category offered for every type (used by the reserved <see cref="IsSystem"/>
/// "Generated Packet" category).</para>
/// </summary>
public class AppointmentDocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    /// <summary>True when the category is offered for EVERY appointment type
    /// (replaces the old null-AppointmentTypeId "applies to all" convention).
    /// Set on the reserved <see cref="IsSystem"/> rows.</summary>
    public virtual bool AppliesToAll { get; set; }

    /// <summary>The appointment types this category is offered for (M2M). Empty
    /// set with <see cref="AppliesToAll"/> false means the category is offered
    /// nowhere (effectively retired).</summary>
    public virtual ICollection<AppointmentDocumentTypeAppointmentType> AppointmentTypes { get; set; }
        = new Collection<AppointmentDocumentTypeAppointmentType>();

    /// <summary>Reserved system category (e.g. "Generated Packet"): hidden from
    /// the picker, not editable or deletable by admins.</summary>
    public virtual bool IsSystem { get; set; }

    /// <summary>Inactive types stay for historical rows but are not offered in
    /// the picker.</summary>
    public virtual bool IsActive { get; set; }

    protected AppointmentDocumentType()
    {
    }

    public AppointmentDocumentType(
        Guid id,
        string name,
        bool appliesToAll = false,
        bool isActive = true,
        bool isSystem = false,
        Guid? tenantId = null)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength, 0);
        Name = name;
        AppliesToAll = appliesToAll;
        IsActive = isActive;
        IsSystem = isSystem;
        TenantId = tenantId;
        AppointmentTypes = new Collection<AppointmentDocumentTypeAppointmentType>();
    }

    /// <summary>Adds an appointment type to the offered set (idempotent).</summary>
    public virtual void AddAppointmentType(Guid appointmentTypeId)
    {
        if (AppointmentTypes.Any(x => x.AppointmentTypeId == appointmentTypeId))
        {
            return;
        }
        AppointmentTypes.Add(new AppointmentDocumentTypeAppointmentType(Id, appointmentTypeId));
    }

    /// <summary>Reconciles the offered set to exactly the given ids (adds new,
    /// removes dropped). Mirrors Location.SetAppointmentTypes.</summary>
    public virtual void SetAppointmentTypes(List<Guid> appointmentTypeIds)
    {
        Check.NotNull(appointmentTypeIds, nameof(appointmentTypeIds));
        var distinct = appointmentTypeIds.Distinct().ToList();
        var toRemove = AppointmentTypes.Where(x => !distinct.Contains(x.AppointmentTypeId)).ToList();
        foreach (var item in toRemove)
        {
            AppointmentTypes.Remove(item);
        }
        foreach (var id in distinct)
        {
            AddAppointmentType(id);
        }
    }
}
