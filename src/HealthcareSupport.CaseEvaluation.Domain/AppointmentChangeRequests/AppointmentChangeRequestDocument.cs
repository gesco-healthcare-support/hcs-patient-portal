using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Supporting documents (e.g. doctor's note) attached to a cancel or
/// reschedule request. Mirrors OLD's <c>AppointmentChangeRequestDocument</c>
/// table (Phase 1.5, 2026-05-01). Stored in <c>IBlobStorage</c>.
/// </summary>
[Audited]
public class AppointmentChangeRequestDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentChangeRequestId { get; protected set; }

    [NotNull]
    public virtual string DocumentName { get; set; } = null!;

    [NotNull]
    public virtual string FileName { get; set; } = null!;

    [NotNull]
    public virtual string BlobName { get; set; } = null!;

    [CanBeNull]
    public virtual string? ContentType { get; set; }

    public virtual long FileSize { get; set; }

    public virtual Guid UploadedByUserId { get; set; }

    protected AppointmentChangeRequestDocument()
    {
    }

    public AppointmentChangeRequestDocument(
        Guid id,
        Guid? tenantId,
        Guid appointmentChangeRequestId,
        string documentName,
        string fileName,
        string blobName,
        string? contentType,
        long fileSize,
        Guid uploadedByUserId)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentChangeRequestId = appointmentChangeRequestId;
        Check.NotNullOrWhiteSpace(documentName, nameof(documentName));
        Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        DocumentName = documentName;
        FileName = fileName;
        BlobName = blobName;
        ContentType = contentType;
        FileSize = fileSize;
        UploadedByUserId = uploadedByUserId;
    }
}
