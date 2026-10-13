using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Slim DomainService for <see cref="AppointmentDocument"/>. The AppService
/// orchestrates blob upload -> entity insert in a single UoW; this manager
/// centralizes the validation, the entity construction, and the record of
/// a file an upload replaces.
/// </summary>
public class AppointmentDocumentManager : DomainService
{
    protected IRepository<AppointmentDocument, Guid> _appointmentDocumentRepository;
    protected IRepository<AppointmentDocumentVersion, Guid> _versionRepository;

    public AppointmentDocumentManager(
        IRepository<AppointmentDocument, Guid> appointmentDocumentRepository,
        IRepository<AppointmentDocumentVersion, Guid> versionRepository)
    {
        _appointmentDocumentRepository = appointmentDocumentRepository;
        _versionRepository = versionRepository;
    }

    /// <summary>
    /// Records the file <paramref name="document"/> points at NOW, so it can still be found after an
    /// upload replaces it. The file itself is never deleted; see <see cref="AppointmentDocumentVersion"/>.
    ///
    /// <para>Call it after the new file is stored and BEFORE the document's file fields are
    /// overwritten, because it reads them. Records nothing, and returns null, for a queued row that
    /// has never had a file: its placeholder names no stored object.</para>
    /// </summary>
    public virtual async Task<AppointmentDocumentVersion?> RecordReplacedFileAsync(
        AppointmentDocument document,
        Guid? replacedByUserId)
    {
        Check.NotNull(document, nameof(document));

        if (string.IsNullOrWhiteSpace(document.BlobName)
            || string.Equals(document.BlobName, AppointmentDocumentConsts.PendingUploadPlaceholder, StringComparison.Ordinal))
        {
            return null;
        }

        var version = new AppointmentDocumentVersion(GuidGenerator.Create(), document, replacedByUserId);
        return await _versionRepository.InsertAsync(version);
    }

    public virtual async Task<AppointmentDocument> CreateAsync(
        Guid? tenantId,
        Guid appointmentId,
        string documentName,
        string fileName,
        string blobName,
        string? contentType,
        long fileSize,
        Guid uploadedByUserId,
        Guid? appointmentDocumentTypeId = null,
        string? otherDocumentTypeName = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException("AppointmentId is required.");
        }
        if (fileSize <= 0)
        {
            throw new UserFriendlyException("File is empty.");
        }
        if (fileSize > AppointmentDocumentConsts.MaxFileSizeBytes)
        {
            throw new UserFriendlyException($"File exceeds the {AppointmentDocumentConsts.MaxFileSizeBytes / (1024 * 1024)} MB upload cap.");
        }

        var entity = new AppointmentDocument(
            GuidGenerator.Create(),
            tenantId,
            appointmentId,
            documentName,
            fileName,
            blobName,
            contentType,
            fileSize,
            uploadedByUserId,
            appointmentDocumentTypeId,
            otherDocumentTypeName);

        return await _appointmentDocumentRepository.InsertAsync(entity);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- queued-state factory for the package-doc
    /// auto-queue path. Creates a row in
    /// <see cref="DocumentStatus.Pending"/> with placeholder file
    /// metadata + a fresh <see cref="AppointmentDocument.VerificationCode"/>.
    /// The patient uploads via the emailed link
    /// (<c>UploadByVerificationCodeAsync</c>) which overwrites the
    /// placeholders with real values and flips status to
    /// <see cref="DocumentStatus.Uploaded"/>.
    ///
    /// <para>Mirrors OLD <c>AppointmentDocumentDomain.cs</c>:1102-1123
    /// where the row is inserted on staff approval ahead of upload.
    /// Closes the Phase 12 deferred row insert.</para>
    /// </summary>
    public virtual async Task<AppointmentDocument> CreateQueuedAsync(
        Guid? tenantId,
        Guid appointmentId,
        string documentName,
        Guid? sourceDocumentId = null,
        Guid? appointmentDocumentTypeId = null)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException("AppointmentId is required.");
        }
        if (string.IsNullOrWhiteSpace(documentName))
        {
            throw new UserFriendlyException("Document name is required.");
        }

        var entity = AppointmentDocument.CreateQueued(
            id: GuidGenerator.Create(),
            tenantId: tenantId,
            appointmentId: appointmentId,
            documentName: documentName,
            verificationCode: Guid.NewGuid(),
            sourceDocumentId: sourceDocumentId,
            appointmentDocumentTypeId: appointmentDocumentTypeId);

        return await _appointmentDocumentRepository.InsertAsync(entity);
    }
}
