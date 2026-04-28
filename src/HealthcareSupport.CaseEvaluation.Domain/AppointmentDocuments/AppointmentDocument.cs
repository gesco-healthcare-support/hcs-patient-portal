using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W1-3 cut: minimum viable appointment-document row. Files live in
/// <c>IBlobContainer&lt;AppointmentDocumentsContainer&gt;</c> (DB BLOB at MVP);
/// this entity carries metadata plus the BlobName pointer.
///
/// Cuts deferred to ledger:
///   - AppointmentDocumentType lookup (free-text DocumentName at MVP).
///   - AppointmentNewDocument post-approval sibling (single entity at MVP).
///   - Approve / Reject document workflow + status enum.
///   - Verification-code anonymous download.
///   - AV scan, retention job.
///
/// Tenant-scoped via <see cref="IMultiTenant"/>; FK to Appointment matches
/// the existing FK style (NoAction). The blob save and entity insert happen
/// inside a single AppService UoW so a partial failure rolls back both.
/// </summary>
public class AppointmentDocument : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; set; }

    /// <summary>Free-text document name supplied by the uploader (e.g. "Medical records 2026-Q1").</summary>
    [NotNull]
    public virtual string DocumentName { get; set; } = null!;

    /// <summary>Original file name as uploaded (e.g. "scan.pdf").</summary>
    [NotNull]
    public virtual string FileName { get; set; } = null!;

    /// <summary>Random GUID-derived identifier used as the blob's storage key.</summary>
    [NotNull]
    public virtual string BlobName { get; set; } = null!;

    /// <summary>MIME type from the upload (e.g. "application/pdf"). Null if the client did not provide one.</summary>
    public virtual string? ContentType { get; set; }

    public virtual long FileSize { get; set; }

    public virtual Guid UploadedByUserId { get; set; }

    protected AppointmentDocument()
    {
    }

    public AppointmentDocument(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        string documentName,
        string fileName,
        string blobName,
        string? contentType,
        long fileSize,
        Guid uploadedByUserId)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        DocumentName = documentName;
        FileName = fileName;
        BlobName = blobName;
        ContentType = contentType;
        FileSize = fileSize;
        UploadedByUserId = uploadedByUserId;

        Check.NotNullOrWhiteSpace(DocumentName, nameof(documentName));
        Check.Length(DocumentName, nameof(documentName), AppointmentDocumentConsts.DocumentNameMaxLength);
        Check.NotNullOrWhiteSpace(FileName, nameof(fileName));
        Check.Length(FileName, nameof(fileName), AppointmentDocumentConsts.FileNameMaxLength);
        Check.NotNullOrWhiteSpace(BlobName, nameof(blobName));
        Check.Length(BlobName, nameof(blobName), AppointmentDocumentConsts.BlobNameMaxLength);
        Check.Length(ContentType, nameof(contentType), AppointmentDocumentConsts.ContentTypeMaxLength);
    }
}
