using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// IT Admin master Document catalog. Mirrors OLD's <c>DocumentController</c>
/// (P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Document\DocumentController.cs)
/// CRUD surface, replacing OLD's <c>spm.Documents.DocumentFilePath</c>
/// (local file system path) with an ABP <see cref="IBlobContainer{TContainer}"/>
/// reference. Phase 5 (2026-05-03).
///
/// Authorization (Phase 2.5 + Phase 5):
///   - Class-level <c>[Authorize(Documents.Default)]</c> gates Get for all
///     internal roles plus IT Admin.
///   - <c>CreateAsync / UpdateAsync / ReplaceFileAsync / DeleteAsync</c>
///     override with <c>.Create / .Edit / .Delete</c> so non-IT-Admin
///     callers see 403.
///
/// Strict-parity rule: <c>DeleteAsync</c> rejects when a
/// <see cref="DocumentPackage"/> still references the row -- IT Admin must
/// unlink first. OLD's <c>PackageDetailDomain.Delete</c> only soft-deletes
/// one linked DocumentPackage at a time, but its
/// <c>DocumentDomain.DeleteValidation</c> is empty; replicating the empty
/// validation here would orphan link rows. The reference check is the
/// minimum safe replication of the documented intent.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Documents.Default)]
public class DocumentsAppService : CaseEvaluationAppService, IDocumentsAppService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IRepository<DocumentPackage> _documentPackageRepository;
    private readonly IBlobContainer<MasterDocumentsContainer> _blobContainer;
    private readonly IGuidGenerator _guidGenerator;

    public DocumentsAppService(
        IDocumentRepository documentRepository,
        IRepository<DocumentPackage> documentPackageRepository,
        IBlobContainer<MasterDocumentsContainer> blobContainer,
        IGuidGenerator guidGenerator)
    {
        _documentRepository = documentRepository;
        _documentPackageRepository = documentPackageRepository;
        _blobContainer = blobContainer;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<DocumentDto> GetAsync(Guid id)
    {
        var entity = await _documentRepository.GetAsync(id);
        return ObjectMapper.Map<Document, DocumentDto>(entity);
    }

    public virtual async Task<PagedResultDto<DocumentDto>> GetListAsync(GetDocumentsInput input)
    {
        Check.NotNull(input, nameof(input));

        var queryable = await _documentRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            var needle = input.FilterText.Trim();
            queryable = queryable.Where(x => x.Name.Contains(needle));
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sort = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(Document.Name) : input.Sorting;
        queryable = ApplySortingAndPaging(queryable, sort, input.SkipCount, input.MaxResultCount);

        var entities = await AsyncExecuter.ToListAsync(queryable);
        return new PagedResultDto<DocumentDto>(
            totalCount,
            entities.Select(ObjectMapper.Map<Document, DocumentDto>).ToList());
    }

    [Authorize(CaseEvaluationPermissions.Documents.Create)]
    public virtual async Task<DocumentDto> CreateAsync(DocumentCreateDto input, Stream fileStream, string fileName)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(fileStream, nameof(fileStream));
        Check.NotNullOrWhiteSpace(fileName, nameof(fileName));

        var blobName = ComposeBlobName(fileName);
        await _blobContainer.SaveAsync(blobName, fileStream, overrideExisting: false);

        var entity = new Document(
            id: _guidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            name: input.Name,
            blobName: blobName,
            contentType: input.ContentType,
            isActive: input.IsActive);

        await _documentRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<Document, DocumentDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.Documents.Edit)]
    public virtual async Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input)
    {
        Check.NotNull(input, nameof(input));

        var entity = await _documentRepository.GetAsync(id);
        entity.Name = input.Name;
        entity.ContentType = input.ContentType;
        entity.IsActive = input.IsActive;
        await _documentRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<Document, DocumentDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.Documents.Edit)]
    public virtual async Task<DocumentDto> ReplaceFileAsync(Guid id, Stream fileStream, string fileName, string? contentType)
    {
        Check.NotNull(fileStream, nameof(fileStream));
        Check.NotNullOrWhiteSpace(fileName, nameof(fileName));

        var entity = await _documentRepository.GetAsync(id);

        var newBlobName = ComposeBlobName(fileName);
        await _blobContainer.SaveAsync(newBlobName, fileStream, overrideExisting: false);

        entity.BlobName = newBlobName;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            entity.ContentType = contentType;
        }
        await _documentRepository.UpdateAsync(entity, autoSave: true);

        return ObjectMapper.Map<Document, DocumentDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.Documents.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var stillLinked = await _documentPackageRepository.AnyAsync(x => x.DocumentId == id && x.IsActive);
        if (stillLinked)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.DocumentInUse);
        }

        await _documentRepository.DeleteAsync(id, autoSave: true);
    }

    /// <summary>
    /// Tenant-scoped, collision-safe blob name. The leading GUID makes the
    /// blob path independent of the user-supplied filename so two users
    /// uploading "intake.pdf" never overwrite each other; the trailing
    /// extension keeps blob-storage browsing tools (Azure portal, etc.)
    /// readable.
    /// </summary>
    private string ComposeBlobName(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        var tenant = CurrentTenant.Id?.ToString("N") ?? "host";
        return $"{tenant}/{_guidGenerator.Create():N}{ext}";
    }

    private static System.Linq.IQueryable<Document> ApplySortingAndPaging(
        System.Linq.IQueryable<Document> queryable, string sorting, int skipCount, int maxResultCount)
    {
        var ordered = sorting.Trim().ToLowerInvariant() switch
        {
            "name desc" => queryable.OrderByDescending(x => x.Name),
            "name" or "name asc" => queryable.OrderBy(x => x.Name),
            "creationtime desc" => queryable.OrderByDescending(x => x.CreationTime),
            "creationtime" or "creationtime asc" => queryable.OrderBy(x => x.CreationTime),
            _ => queryable.OrderBy(x => x.Name),
        };
        return ordered.Skip(skipCount).Take(maxResultCount);
    }
}
