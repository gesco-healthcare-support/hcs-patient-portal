using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
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
[Audited]
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

    /// <summary>W2-11: review state. Defaults to Uploaded; flipped by Approve/Reject AppService methods.</summary>
    public virtual DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

    /// <summary>W2-11: rejection reason captured when Status flips to Rejected. Max 500 chars.</summary>
    [CanBeNull]
    public virtual string? RejectionReason { get; set; }

    /// <summary>W2-11: user who approved or last actioned the document.</summary>
    public virtual Guid? ResponsibleUserId { get; set; }

    /// <summary>W2-11: user who rejected the document (null until rejected).</summary>
    public virtual Guid? RejectedByUserId { get; set; }

    /// <summary>
    /// True when this row was uploaded as an ad-hoc / general document
    /// (no status gate, no due-date gate, not part of a package). Mirrors
    /// OLD's <c>AppointmentNewDocument</c> sibling table; NEW unifies via
    /// this flag (Phase 1.6, 2026-05-01).
    /// </summary>
    public virtual bool IsAdHoc { get; set; }

    /// <summary>
    /// True when this row is the AME Joint Declaration Form. Mirrors OLD's
    /// <c>AppointmentJointDeclaration</c> sibling table; NEW unifies via
    /// this flag (Phase 1.6, 2026-05-01).
    /// </summary>
    public virtual bool IsJointDeclaration { get; set; }

    /// <summary>
    /// Per-document GUID emailed to the patient as part of the package-doc
    /// upload link, allowing unauthenticated upload of THIS document only.
    /// Null for internal-user uploads and ad-hoc rows where no email link
    /// is sent.
    /// </summary>
    public virtual Guid? VerificationCode { get; set; }

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

    /// <summary>
    /// Phase 14 (2026-05-04) -- queued-state factory for the package-doc
    /// auto-queue path
    /// (<c>PackageDocumentQueueHandler</c> on
    /// <c>AppointmentApprovedEto</c>). Creates a row in
    /// <see cref="DocumentStatus.Pending"/> with no file metadata yet
    /// (placeholders satisfy the constructor's
    /// <c>Check.NotNullOrWhiteSpace</c> guards). The
    /// <see cref="VerificationCode"/> lets the patient upload via the
    /// emailed link without an authenticated session. When the user
    /// uploads, <c>UploadPackageDocumentAsync</c> overwrites
    /// <see cref="BlobName"/>, <see cref="FileName"/>,
    /// <see cref="FileSize"/>, <see cref="ContentType"/>,
    /// <see cref="UploadedByUserId"/> and flips
    /// <see cref="Status"/> to <see cref="DocumentStatus.Uploaded"/>.
    ///
    /// <para>Mirrors OLD's <c>AppointmentDocumentDomain</c> queue path
    /// at <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:1102-1123
    /// where the row is inserted with <c>DocumentStatusId = Pending</c>
    /// + <c>VerificationCode = guid</c> and the file metadata
    /// (<c>DocumentFilePath</c>, <c>FileType</c>) is populated only on
    /// upload. NEW collapses OLD's separate <c>FileType</c> +
    /// <c>DocumentFilePath</c> into <see cref="ContentType"/> +
    /// <see cref="BlobName"/>.</para>
    /// </summary>
    public static AppointmentDocument CreateQueued(
        Guid id,
        Guid? tenantId,
        Guid appointmentId,
        string documentName,
        Guid verificationCode)
    {
        Check.NotNullOrWhiteSpace(documentName, nameof(documentName));
        Check.Length(documentName, nameof(documentName), AppointmentDocumentConsts.DocumentNameMaxLength);

        // Placeholder file metadata satisfies the public constructor's
        // non-null guards; real values are written by the upload path.
        const string PendingPlaceholder = "(pending-upload)";
        var entity = new AppointmentDocument(
            id: id,
            tenantId: tenantId,
            appointmentId: appointmentId,
            documentName: documentName,
            fileName: PendingPlaceholder,
            blobName: PendingPlaceholder,
            contentType: null,
            fileSize: 0,
            uploadedByUserId: Guid.Empty);
        entity.Status = DocumentStatus.Pending;
        entity.VerificationCode = verificationCode;
        return entity;
    }
}
