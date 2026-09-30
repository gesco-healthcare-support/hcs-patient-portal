using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Many-to-many link between <c>PackageDetail</c> (per-AppointmentType
/// template) and <c>Document</c> (master template catalog). Mirrors OLD's
/// <c>DocumentPackage</c> table verbatim (Phase 1.2, 2026-05-01). Composite
/// key on (PackageDetailId, DocumentId) enforces the M:N uniqueness.
/// </summary>
public class DocumentPackage : Entity
{
    public virtual Guid PackageDetailId { get; protected set; }
    public virtual Guid DocumentId { get; protected set; }
    public virtual bool IsActive { get; set; }

    protected DocumentPackage()
    {
    }

    public DocumentPackage(Guid packageDetailId, Guid documentId, bool isActive = true)
    {
        PackageDetailId = packageDetailId;
        DocumentId = documentId;
        IsActive = isActive;
    }

    public override object[] GetKeys() => new object[] { PackageDetailId, DocumentId };
}
