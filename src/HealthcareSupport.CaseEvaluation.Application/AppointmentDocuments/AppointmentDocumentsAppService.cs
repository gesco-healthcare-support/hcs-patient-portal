using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentDocumentsAppService : CaseEvaluationAppService, IAppointmentDocumentsAppService
{
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly AppointmentDocumentManager _documentManager;
    private readonly IBlobContainer<AppointmentDocumentsContainer> _blobContainer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IAuthorizationService _authorizationService;

    public AppointmentDocumentsAppService(
        IRepository<AppointmentDocument, Guid> documentRepository,
        AppointmentDocumentManager documentManager,
        IBlobContainer<AppointmentDocumentsContainer> blobContainer,
        ICurrentTenant currentTenant,
        IBackgroundJobManager backgroundJobManager,
        IAuthorizationService authorizationService)
    {
        _documentRepository = documentRepository;
        _documentManager = documentManager;
        _blobContainer = blobContainer;
        _currentTenant = currentTenant;
        _backgroundJobManager = backgroundJobManager;
        _authorizationService = authorizationService;
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

        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        var blobName = $"{tenantSegment}/{appointmentId}/{Guid.NewGuid():N}";
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

        entity.Status = initialStatus;
        if (initialStatus == DocumentStatus.Accepted)
        {
            entity.ResponsibleUserId = CurrentUser.Id;
        }
        await _documentRepository.UpdateAsync(entity);

        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
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
        return ObjectMapper.Map<AppointmentDocument, AppointmentDocumentDto>(entity);
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
