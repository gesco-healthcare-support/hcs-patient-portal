using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Per-AppointmentType package template -- defines which Documents must be
/// completed and uploaded for appointments of a given AppointmentType. IT
/// Admin manages these. Mirrors OLD's <c>PackageDetail</c> table verbatim
/// (Phase 1.2, 2026-05-01).
/// </summary>
[Audited]
public class PackageDetail : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string PackageName { get; set; } = null!;

    /// <summary>
    /// Appointment type this template belongs to. Nullable per OLD schema --
    /// in practice IT Admin always sets it; null means "applies to any type".
    /// </summary>
    public virtual Guid? AppointmentTypeId { get; set; }

    public virtual bool IsActive { get; set; }

    public virtual ICollection<DocumentPackage> DocumentPackages { get; protected set; } = new Collection<DocumentPackage>();

    protected PackageDetail()
    {
    }

    public PackageDetail(
        Guid id,
        Guid? tenantId,
        string packageName,
        Guid? appointmentTypeId,
        bool isActive = true)
    {
        Id = id;
        TenantId = tenantId;
        Check.NotNullOrWhiteSpace(packageName, nameof(packageName));
        Check.Length(packageName, nameof(packageName), PackageDetailConsts.PackageNameMaxLength);
        PackageName = packageName;
        AppointmentTypeId = appointmentTypeId;
        IsActive = isActive;
        DocumentPackages = new Collection<DocumentPackage>();
    }
}
