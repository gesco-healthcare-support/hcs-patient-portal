using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentDocumentsAppService : CaseEvaluationAppService, IAppointmentDocumentsAppService
{
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly AppointmentDocumentManager _documentManager;
    private readonly IBlobContainer<AppointmentDocumentsContainer> _blobContainer;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentDocumentsAppService(
        IRepository<AppointmentDocument, Guid> documentRepository,
        AppointmentDocumentManager documentManager,
        IBlobContainer<AppointmentDocumentsContainer> blobContainer,
        ICurrentTenant currentTenant)
    {
        _documentRepository = documentRepository;
        _documentManager = documentManager;
        _blobContainer = blobContainer;
        _currentTenant = currentTenant;
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

        // Blob name is a tenant-prefixed GUID; SaveAsync writes inside the
        // current UoW so a manager-side validation failure rolls the blob
        // write back along with the entity insert.
        var tenantSegment = _currentTenant.Id?.ToString() ?? "host";
        var blobName = $"{tenantSegment}/{appointmentId}/{Guid.NewGuid():N}";
        await _blobContainer.SaveAsync(blobName, content, overrideExisting: false);

        var entity = await _documentManager.CreateAsync(
            tenantId: _currentTenant.Id,
            appointmentId: appointmentId,
            documentName: documentName.Trim(),
            fileName: fileName.Trim(),
            blobName: blobName,
            contentType: contentType,
            fileSize: fileSize,
            uploadedByUserId: CurrentUser.Id ?? Guid.Empty);

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
        // Best-effort blob delete; if the file is already gone the entity
        // soft-delete still proceeds.
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
}
