using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Volo.Abp.Validation;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentDocumentsAppService : CaseEvaluationAppService, IAppointmentDocumentsAppService
{
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly AppointmentDocumentManager _documentManager;
    private readonly IBlobContainer<AppointmentDocumentsContainer> _blobContainer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILocalEventBus _localEventBus;
    private readonly IdentityUserManager _userManager;

    public AppointmentDocumentsAppService(
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        AppointmentDocumentManager documentManager,
        IBlobContainer<AppointmentDocumentsContainer> blobContainer,
        ICurrentTenant currentTenant,
        IBackgroundJobManager backgroundJobManager,
        IAuthorizationService authorizationService,
        ILocalEventBus localEventBus,
        IdentityUserManager userManager)
    {
        _documentRepository = documentRepository;
        _appointmentRepository = appointmentRepository;
        _documentManager = documentManager;
        _packetRepository = packetRepository;
        _blobContainer = blobContainer;
        _currentTenant = currentTenant;
        _backgroundJobManager = backgroundJobManager;
        _authorizationService = authorizationService;
        _localEventBus = localEventBus;
        _userManager = userManager;
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Default)]
    public virtual async Task<List<AppointmentDocumentDto>> GetListByAppointmentAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        var queryable = await _documentRepository.GetQueryableAsync();
        var rows = queryable
            .Where(x => x.AppointmentId == appointmentId)
            .OrderByDescending(x => x.CreationTime)
            .ToList();
        return ObjectMapper.Map<List<AppointmentDocument>, List<AppointmentDocumentDto>>(rows);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Create)]
    [UnitOfWork]
    [DisableValidation]
    public virtual async Task<AppointmentDocumentDto> UploadStreamAsync(
        Guid appointmentId,
        string documentName,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        if (string.IsNullOrWhiteSpace(documentName))
        {
            documentName = string.IsNullOrWhiteSpace(fileName) ? "Unnamed document" : fileName;
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new UserFriendlyException("File name is required.");
        }
        if (content == null || fileSize <= 0)
        {
            throw new UserFriendlyException("File is empty.");
        }

        // W2-11: magic-byte validation BEFORE any blob save. Browser-supplied
        // ContentType + extension are trivially spoofable; the file header
        // is part of the file itself. Limited to PDF/JPG/PNG to match the
        // packet merge path's supported formats.
        EnsureValidFileFormat(content, fileName);

        var tenantSegment = _currentTenant.Id?.ToString("N") ?? "host";
        var blobName = $"{tenantSegment}/{appointmentId:N}/{Guid.NewGuid():N}";
        await _blobContainer.SaveAsync(blobName, content, overrideExisting: false);

        // W2-11: internal staff uploads land directly as Approved (matches
        // OLD's vInternalUser pre-set behaviour); external user uploads land
        // as Uploaded pending office review.
        var initialStatus = await IsInternalActorAsync()
            ? DocumentStatus.Accepted
            : DocumentStatus.Uploaded;

        var entity = await _documentManager.CreateAsync(
            tenantId: _currentTenant.Id,
            appointmentId: appointmentId,
            documentName: documentName.Trim(),
            fileName: fileName.Trim(),
            blobName: blobName,
            contentType: contentType,
            fileSize: fileSize,
            uploadedByUserId: CurrentUser.Id ?? Guid.Empty);

        // Phase 14: this generic upload path is the ad-hoc / general
        // document path (OLD's AppointmentNewDocuments table). NEW
        // unifies via the IsAdHoc flag (Phase 1.6). Package + JDF
        // uploads use the dedicated methods below.
        entity.IsAdHoc = true;
        entity.Status = initialStatus;
        if (initialStatus == DocumentStatus.Accepted)
        {
            entity.ResponsibleUserId = CurrentUser.Id;
        }
        await _documentRepository.UpdateAsync(entity);

        await _localEventBus.PublishAsync(new AppointmentDocumentUploadedEto
        {
            AppointmentId = appointmentId,
            AppointmentDocumentId = entity.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = true,
            IsJointDeclaration = false,
            UploadedByUserId = CurrentUser.Id,
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- package-document upload. Updates an
    /// existing Pending row (created by
    /// <c>PackageDocumentQueueHandler</c>) with the user-supplied
    /// file. Mirrors OLD <c>AppointmentDocumentDomain.Update</c> at
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:109-182.
    /// Gates via <see cref="DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate"/>
    /// + <see cref="DocumentUploadGate.EnsureNotImmutable"/>.
    /// Internal users (any caller with the
    /// <c>AppointmentDocuments.Approve</c> permission) auto-Accept;
    /// external users land as Uploaded.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Create)]
    [UnitOfWork]
    [DisableValidation]
    public virtual async Task<AppointmentDocumentDto> UploadPackageDocumentAsync(
        Guid documentId,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        var document = await _documentRepository.GetAsync(documentId);
        var appointment = await _appointmentRepository.GetAsync(document.AppointmentId);

        var isInternal = await IsInternalActorAsync();
        DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment);
        DocumentUploadGate.EnsureNotImmutable(document, isInternal);

        await OverwriteUploadedFileAsync(document, fileName, contentType, fileSize, content, isInternal);

        await _localEventBus.PublishAsync(new AppointmentDocumentUploadedEto
        {
            AppointmentId = appointment.Id,
            AppointmentDocumentId = document.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = false,
            IsJointDeclaration = false,
            UploadedByUserId = CurrentUser.Id,
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(document);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- AME Joint Declaration Form upload.
    /// Creates a new <c>AppointmentDocument</c> row with
    /// <c>IsJointDeclaration = true</c>. Gates: appointment Approved +
    /// not past DueDate; AppointmentType is AME (or future AME-REVAL);
    /// caller is the booking attorney. Mirrors OLD
    /// <c>AppointmentJointDeclarationDomain</c>.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Create)]
    [UnitOfWork]
    [DisableValidation]
    public virtual async Task<AppointmentDocumentDto> UploadJointDeclarationAsync(
        Guid appointmentId,
        string documentName,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new UserFriendlyException("File name is required.");
        }
        if (content == null || fileSize <= 0)
        {
            throw new UserFriendlyException("File is empty.");
        }

        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment);
        DocumentUploadGate.EnsureAme(appointment.AppointmentTypeId);

        var roleNames = await ResolveCurrentUserRoleNamesAsync();
        DocumentUploadGate.EnsureCreatorIsAttorney(appointment, CurrentUser.Id, roleNames);

        EnsureValidFileFormat(content, fileName);

        var tenantSegment = _currentTenant.Id?.ToString("N") ?? "host";
        var blobName = $"{tenantSegment}/{appointmentId:N}/{Guid.NewGuid():N}";
        await _blobContainer.SaveAsync(blobName, content, overrideExisting: false);

        var entity = await _documentManager.CreateAsync(
            tenantId: _currentTenant.Id,
            appointmentId: appointmentId,
            documentName: string.IsNullOrWhiteSpace(documentName)
                ? "Joint Declaration Form"
                : documentName.Trim(),
            fileName: fileName.Trim(),
            blobName: blobName,
            contentType: contentType,
            fileSize: fileSize,
            uploadedByUserId: CurrentUser.Id ?? Guid.Empty);

        entity.IsJointDeclaration = true;
        entity.Status = DocumentStatus.Uploaded; // attorney uploader; never internal here
        await _documentRepository.UpdateAsync(entity);

        await _localEventBus.PublishAsync(new AppointmentDocumentUploadedEto
        {
            AppointmentId = appointmentId,
            AppointmentDocumentId = entity.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = false,
            IsJointDeclaration = true,
            UploadedByUserId = CurrentUser.Id,
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- anonymous upload via per-document
    /// verification code (the link emailed to the patient at staff
    /// approval time). Mirrors OLD <c>AppointmentDocumentDomain.GetValidation</c>
    /// at <c>AppointmentDocumentDomain.cs</c>:64-75. Same upload-gate
    /// semantics as <c>UploadPackageDocumentAsync</c> but with no
    /// authenticated <c>CurrentUser</c>; the verification-code match
    /// IS the authorization. Rate-limited at the HTTP layer (Phase 10
    /// fixed-window limiter, partitioned by code).
    /// </summary>
    [AllowAnonymous]
    [UnitOfWork]
    [DisableValidation]
    public virtual async Task<AppointmentDocumentDto> UploadByVerificationCodeAsync(
        Guid documentId,
        Guid verificationCode,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        var document = await _documentRepository.FindAsync(documentId);
        DocumentUploadGate.EnsureVerificationCodeMatches(document!, verificationCode);
        var appointment = await _appointmentRepository.GetAsync(document!.AppointmentId);
        DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment);

        // Anonymous = external by definition; never internal.
        await OverwriteUploadedFileAsync(document, fileName, contentType, fileSize, content, isInternalUser: false);

        await _localEventBus.PublishAsync(new AppointmentDocumentUploadedEto
        {
            AppointmentId = appointment.Id,
            AppointmentDocumentId = document.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = false,
            IsJointDeclaration = document.IsJointDeclaration,
            UploadedByUserId = null, // anonymous
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(document);
    }

    /// <summary>
    /// Shared file-upload + entity-update path for package-doc and
    /// verification-code upload paths. Saves the blob, overwrites the
    /// document's file metadata, sets status (Uploaded for external,
    /// Accepted for internal), clears <see cref="AppointmentDocument.RejectionReason"/>
    /// (re-upload after rejection wipes the prior reason).
    /// </summary>
    private async Task OverwriteUploadedFileAsync(
        AppointmentDocument document,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content,
        bool isInternalUser)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new UserFriendlyException("File name is required.");
        }
        if (content == null || fileSize <= 0)
        {
            throw new UserFriendlyException("File is empty.");
        }

        EnsureValidFileFormat(content, fileName);

        var tenantSegment = _currentTenant.Id?.ToString("N") ?? "host";
        var newBlobName = $"{tenantSegment}/{document.AppointmentId:N}/{Guid.NewGuid():N}";
        await _blobContainer.SaveAsync(newBlobName, content, overrideExisting: false);

        // Try to delete the placeholder/old blob if it was a real one
        // (queued rows have a "(pending-upload)" placeholder; skip that).
        if (!string.Equals(document.BlobName, "(pending-upload)", StringComparison.Ordinal))
        {
            try
            {
                await _blobContainer.DeleteAsync(document.BlobName);
            }
            catch
            {
                // entity row is the source of truth; orphan blob is
                // a cleanup-job concern.
            }
        }

        document.BlobName = newBlobName;
        document.FileName = fileName.Trim();
        document.ContentType = contentType;
        document.FileSize = fileSize;
        document.UploadedByUserId = CurrentUser.Id ?? Guid.Empty;
        document.RejectionReason = null;
        document.RejectedByUserId = null;
        document.Status = isInternalUser ? DocumentStatus.Accepted : DocumentStatus.Uploaded;
        if (isInternalUser)
        {
            document.ResponsibleUserId = CurrentUser.Id;
        }
        await _documentRepository.UpdateAsync(document);
    }

    private async Task<IReadOnlyCollection<string>> ResolveCurrentUserRoleNamesAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return Array.Empty<string>();
        }
        var user = await _userManager.GetByIdAsync(CurrentUser.Id.Value);
        if (user == null)
        {
            return Array.Empty<string>();
        }
        var names = await _userManager.GetRolesAsync(user);
        return names.ToArray();
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Default)]
    public virtual async Task<DownloadResult> DownloadAsync(Guid id)
    {
        var entity = await _documentRepository.FindAsync(id);
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(AppointmentDocument), id);
        }
        var stream = await _blobContainer.GetAsync(entity.BlobName);
        if (stream == null)
        {
            throw new UserFriendlyException("Document file is missing from storage.");
        }
        return new DownloadResult
        {
            Content = stream,
            FileName = entity.FileName,
            ContentType = entity.ContentType ?? "application/octet-stream",
        };
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Delete)]
    [UnitOfWork]
    public virtual async Task DeleteAsync(Guid id)
    {
        var entity = await _documentRepository.FindAsync(id);
        if (entity == null)
        {
            return;
        }
        try
        {
            await _blobContainer.DeleteAsync(entity.BlobName);
        }
        catch
        {
            // swallowed -- entity row is the source of truth.
        }
        await _documentRepository.DeleteAsync(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Approve)]
    [UnitOfWork]
    public virtual async Task<AppointmentDocumentDto> ApproveAsync(Guid id)
    {
        var entity = await _documentRepository.GetAsync(id);
        entity.Status = DocumentStatus.Accepted;
        entity.RejectionReason = null;
        entity.RejectedByUserId = null;
        entity.ResponsibleUserId = CurrentUser.Id;
        await _documentRepository.UpdateAsync(entity);

        // Phase 14: publish Eto for the per-feature email handler
        // (Phase 14b) so the uploader gets the OLD-parity
        // PatientDocumentAccepted email.
        await _localEventBus.PublishAsync(new AppointmentDocumentAcceptedEto
        {
            AppointmentId = entity.AppointmentId,
            AppointmentDocumentId = entity.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = entity.IsAdHoc,
            IsJointDeclaration = entity.IsJointDeclaration,
            AcceptedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentDocuments.Approve)]
    [UnitOfWork]
    public virtual async Task<AppointmentDocumentDto> RejectAsync(Guid id, RejectDocumentInput input)
    {
        if (string.IsNullOrWhiteSpace(input?.Reason))
        {
            throw new UserFriendlyException("A rejection reason is required.");
        }
        var entity = await _documentRepository.GetAsync(id);
        entity.Status = DocumentStatus.Rejected;
        entity.RejectionReason = input.Reason.Trim();
        entity.RejectedByUserId = CurrentUser.Id;
        entity.ResponsibleUserId = CurrentUser.Id;
        await _documentRepository.UpdateAsync(entity);

        await _localEventBus.PublishAsync(new AppointmentDocumentRejectedEto
        {
            AppointmentId = entity.AppointmentId,
            AppointmentDocumentId = entity.Id,
            TenantId = _currentTenant.Id,
            IsAdHoc = entity.IsAdHoc,
            IsJointDeclaration = entity.IsJointDeclaration,
            RejectionNotes = entity.RejectionReason ?? string.Empty,
            RejectedByUserId = CurrentUser.Id ?? Guid.Empty,
            OccurredAt = DateTime.UtcNow,
        });

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
    }

    [Authorize]
    public virtual async Task<List<PatientPortalDocumentDto>> GetCombinedForAppointmentAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }

        // Uploaded documents.
        var docQ = await _documentRepository.GetQueryableAsync();
        var uploads = docQ
            .Where(x => x.AppointmentId == appointmentId)
            .ToList();

        // Generated Patient packet (only when reached the Generated state).
        var packetQ = await _packetRepository.GetQueryableAsync();
        var packets = packetQ
            .Where(x => x.AppointmentId == appointmentId
                        && x.Kind == PacketKind.Patient
                        && x.Status == PacketGenerationStatus.Generated)
            .ToList();

        var combined = new List<PatientPortalDocumentDto>(uploads.Count + packets.Count);

        foreach (var u in uploads)
        {
            combined.Add(new PatientPortalDocumentDto
            {
                Id = u.Id,
                Source = PatientPortalDocumentSource.Uploaded,
                FileName = u.FileName ?? string.Empty,
                ContentType = u.ContentType ?? string.Empty,
                CreatedAt = u.CreationTime,
                PacketKind = null,
                UploadStatus = u.Status,
            });
        }

        foreach (var p in packets)
        {
            combined.Add(new PatientPortalDocumentDto
            {
                Id = p.Id,
                Source = PatientPortalDocumentSource.GeneratedPacket,
                FileName = p.BlobName.Split('/').Last(),
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                CreatedAt = p.GeneratedAt,
                PacketKind = p.Kind,
                UploadStatus = null,
            });
        }

        return combined.OrderByDescending(x => x.CreatedAt).ToList();
    }

    [Authorize(CaseEvaluationPermissions.AppointmentPackets.Regenerate)]
    public virtual async Task RegeneratePacketAsync(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "AppointmentId"]);
        }
        await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs
        {
            AppointmentId = appointmentId,
            TenantId = _currentTenant.Id,
        });
    }

    private async Task<bool> IsInternalActorAsync()
    {
        return await _authorizationService.IsGrantedAsync(CaseEvaluationPermissions.AppointmentDocuments.Approve);
    }

    private static void EnsureValidFileFormat(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(extension))
        {
            throw new UserFriendlyException("Only PDF and image formats (JPG, PNG) are accepted.");
        }

        if (!stream.CanSeek)
        {
            return;
        }

        var magic = new byte[8];
        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.Read(magic, 0, magic.Length);
        stream.Seek(0, SeekOrigin.Begin);

        if (read < 4)
        {
            throw new UserFriendlyException("File is empty or corrupted.");
        }

        bool isPdf = magic[0] == 0x25 && magic[1] == 0x50 && magic[2] == 0x44 && magic[3] == 0x46;
        bool isJpeg = magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF;
        bool isPng = magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47;

        if (!(isPdf || isJpeg || isPng))
        {
            throw new UserFriendlyException("File format is not supported. Please upload a valid PDF or image file.");
        }
    }
}
