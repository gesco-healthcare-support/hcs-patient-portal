using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentType : FullAuditedEntity<Guid>, IMultiTenant
{
    // Office-owned list (db-per-office): each office has its own copy, seeded
    // with the AME/IME/PQME defaults, and may add or disable types.
    public virtual Guid? TenantId { get; protected set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    [CanBeNull]
    public virtual string? Description { get; set; }

    /// <summary>
    /// Booking-dropdown classification (Normal / Re / Both). Drives which
    /// types appear on the initial vs re-evaluation booking path. Null is
    /// treated as "show in all contexts".
    /// </summary>
    public virtual EvaluationType? EvaluationType { get; set; }

    /// <summary>
    /// Selects the per-tenant max-time horizon for booking this type. Null
    /// falls back to the OTHER horizon.
    /// </summary>
    public virtual AppointmentMaxTimeCategory? MaxTimeCategory { get; set; }

    /// <summary>Reserved system type: not editable or deletable by admins.
    /// Mirrors <c>AppointmentDocumentType.IsSystem</c>.</summary>
    public virtual bool IsSystem { get; set; }

    public virtual ICollection<DoctorAppointmentType> DoctorAppointmentTypes { get; set; } = new Collection<DoctorAppointmentType>();

    protected AppointmentType()
    {
    }

    public AppointmentType(
        Guid id,
        string name,
        string? description = null,
        EvaluationType? evaluationType = null,
        AppointmentMaxTimeCategory? maxTimeCategory = null,
        bool isSystem = false)
    {
        Id = id;
        Check.NotNull(name, nameof(name));
        Check.Length(name, nameof(name), AppointmentTypeConsts.NameMaxLength, 0);
        Check.Length(description, nameof(description), AppointmentTypeConsts.DescriptionMaxLength, 0);
        Name = name;
        Description = description;
        EvaluationType = evaluationType;
        MaxTimeCategory = maxTimeCategory;
        IsSystem = isSystem;
    }
}