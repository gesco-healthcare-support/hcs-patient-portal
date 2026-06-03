using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// G-03-01 (2026-06-03): tenant-scoped, per-appointment-type master list of
/// document categories. Restores (and extends) the legacy
/// <c>AppointmentDocumentType</c> lookup that was dropped at MVP. Internal staff
/// (IT Admin / Staff Supervisor) curate one list per appointment type; the list
/// drives the picker on document uploads (wired in a later slice).
///
/// <para>Tenant-scoped per Adrian decision (each office curates its own lists),
/// so this diverges from the host-only AppointmentStatuses lookup it is otherwise
/// modeled on. <see cref="AppointmentTypeId"/> is a loose reference (the
/// AppointmentType lookup is host-scoped); null means the row is not bound to a
/// specific type (used by the reserved <see cref="IsSystem"/> "Generated Packet"
/// category).</para>
/// </summary>
public class AppointmentDocumentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    /// <summary>Per-appointment-type scope; null for system/global rows.</summary>
    public virtual Guid? AppointmentTypeId { get; set; }

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
        Guid? appointmentTypeId,
        bool isActive = true,
        bool isSystem = false,
        Guid? tenantId = null)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentDocumentTypeConsts.NameMaxLength, 0);
        Name = name;
        AppointmentTypeId = appointmentTypeId;
        IsActive = isActive;
        IsSystem = isSystem;
        TenantId = tenantId;
    }
}
