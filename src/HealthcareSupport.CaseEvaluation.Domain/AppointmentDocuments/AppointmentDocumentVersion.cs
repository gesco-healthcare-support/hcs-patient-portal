using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// One file an <see cref="AppointmentDocument"/> used to point at, recorded at the moment an
/// upload replaced it. Append-only: <see cref="AppointmentDocumentManager.RecordReplacedFileAsync"/>
/// inserts these rows and nothing updates or deletes them.
///
/// <para>WHY IT EXISTS (2026-10-12). Documents are kept for seven years. Before this, replacing a
/// document's file either deleted the old object (the signed-in package upload) or kept it with
/// nothing pointing at it (the emailed-link upload), so in neither case could the earlier file be
/// produced on request. The old object now stays in storage and this row is how it is found: by
/// document, by appointment, or by storage key.</para>
///
/// <para>It copies the replaced file's metadata rather than referring to anything, because the
/// document row is overwritten in place on every upload. This is the only place those values
/// survive -- including the rejection reason, which the re-upload clears on the document.</para>
/// </summary>
public class AppointmentDocumentVersion : Entity<Guid>, IMultiTenant, IHasCreationTime
{
    public virtual Guid? TenantId { get; protected set; }

    public virtual Guid AppointmentDocumentId { get; protected set; }

    /// <summary>Copied from the document so every file ever filed on a case is one indexed query.</summary>
    public virtual Guid AppointmentId { get; protected set; }

    /// <summary>Storage key of the replaced file in the appointment-documents container.</summary>
    [NotNull]
    public virtual string BlobName { get; protected set; } = null!;

    [NotNull]
    public virtual string FileName { get; protected set; } = null!;

    public virtual string? ContentType { get; protected set; }

    public virtual long FileSize { get; protected set; }

    /// <summary>
    /// Who uploaded the replaced file. <see cref="Guid.Empty"/> when it arrived through the emailed
    /// link, matching what the document row itself records for such an upload.
    /// </summary>
    public virtual Guid UploadedByUserId { get; protected set; }

    /// <summary>The replaced file's review status at the moment it was replaced, for example Rejected.</summary>
    public virtual DocumentStatus Status { get; protected set; }

    /// <summary>Why staff rejected the replaced file, when they had.</summary>
    [CanBeNull]
    public virtual string? RejectionReason { get; protected set; }

    /// <summary>Who replaced it; null when the replacement came through the anonymous emailed link.</summary>
    public virtual Guid? ReplacedByUserId { get; protected set; }

    /// <summary>When it was replaced. Stamped by ABP on insert.</summary>
    public virtual DateTime CreationTime { get; protected set; }

    protected AppointmentDocumentVersion()
    {
    }

    /// <summary>
    /// Captures <paramref name="replaced"/>'s CURRENT file. Construct it before the upload overwrites
    /// the document's file fields, or it records the new file instead of the old one.
    /// </summary>
    public AppointmentDocumentVersion(Guid id, AppointmentDocument replaced, Guid? replacedByUserId)
        : base(id)
    {
        Check.NotNull(replaced, nameof(replaced));

        TenantId = replaced.TenantId;
        AppointmentDocumentId = replaced.Id;
        AppointmentId = replaced.AppointmentId;
        BlobName = Check.NotNullOrWhiteSpace(replaced.BlobName, nameof(replaced.BlobName));
        FileName = replaced.FileName;
        ContentType = replaced.ContentType;
        FileSize = replaced.FileSize;
        UploadedByUserId = replaced.UploadedByUserId;
        Status = replaced.Status;
        RejectionReason = replaced.RejectionReason;
        ReplacedByUserId = replacedByUserId;
    }
}
