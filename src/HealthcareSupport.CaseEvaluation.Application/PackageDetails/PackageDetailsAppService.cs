using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// IT Admin per-AppointmentType package templates + the M:N link table to
/// master Documents. Mirrors OLD's
/// <c>PackageDetailController</c> / <c>DocumentPackageController</c> CRUD
/// surfaces with the linking endpoints unified onto the parent service.
/// Phase 5 (2026-05-03).
///
/// Strict-parity rule (OLD <c>PackageDetailDomain.cs</c>:48-53):
///   At most one active <c>PackageDetail</c> may exist per
///   <c>AppointmentTypeId</c>. Enforced in <see cref="EnsureNoActiveDuplicateAsync"/>
///   on Create and Update. The DB schema does not enforce this -- the rule
///   has lived in the application layer since OLD shipped.
///
/// OLD-bug-fix exception (Phase 5 audit, 2026-05-03):
///   OLD's <c>Delete</c> only soft-deletes the FIRST linked DocumentPackage
///   (PackageDetailDomain.cs:102-107) and orphans the rest. NEW cascades
///   the soft-delete across all link rows so no orphan rows remain. This
///   is treated as the OLD-bug-fix exception per the audit-doc lifecycle
///   convention.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.PackageDetails.Default)]
public class PackageDetailsAppService : CaseEvaluationAppService, IPackageDetailsAppService
{
    private readonly IPackageDetailRepository _packageRepository;
    private readonly IRepository<DocumentPackage> _linkRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IGuidGenerator _guidGenerator;

    public PackageDetailsAppService(
        IPackageDetailRepository packageRepository,
        IRepository<DocumentPackage> linkRepository,
        IDocumentRepository documentRepository,
        IGuidGenerator guidGenerator)
    {
        _packageRepository = packageRepository;
        _linkRepository = linkRepository;
        _documentRepository = documentRepository;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<PackageDetailDto> GetAsync(Guid id)
    {
        var entity = await _packageRepository.GetAsync(id);
        return ObjectMapper.Map<PackageDetail, PackageDetailDto>(entity);
    }

    public virtual async Task<PagedResultDto<PackageDetailDto>> GetListAsync(GetPackageDetailsInput input)
    {
        Check.NotNull(input, nameof(input));

        var queryable = await _packageRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            var needle = input.FilterText.Trim();
            queryable = queryable.Where(x => x.PackageName.Contains(needle));
        }

        if (input.AppointmentTypeId.HasValue)
        {
            queryable = queryable.Where(x => x.AppointmentTypeId == input.AppointmentTypeId.Value);
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(x => x.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sort = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(PackageDetail.PackageName) : input.Sorting;
        queryable = ApplySortingAndPaging(queryable, sort, input.SkipCount, input.MaxResultCount);

        var entities = await AsyncExecuter.ToListAsync(queryable);
        return new PagedResultDto<PackageDetailDto>(
            totalCount,
            entities.Select(ObjectMapper.Map<PackageDetail, PackageDetailDto>).ToList());
    }

    public virtual async Task<PackageDetailWithDocumentsDto> GetWithDocumentsAsync(Guid id)
    {
        var package = await _packageRepository.GetAsync(id);

        var linkedDocumentIds = (await _linkRepository.GetListAsync(x => x.PackageDetailId == id && x.IsActive))
            .Select(x => x.DocumentId)
            .ToList();

        var documents = linkedDocumentIds.Count == 0
            ? new List<Document>()
            : await _documentRepository.GetListAsync(x => linkedDocumentIds.Contains(x.Id));

        return new PackageDetailWithDocumentsDto
        {
            Package = ObjectMapper.Map<PackageDetail, PackageDetailDto>(package),
            LinkedDocuments = documents.Select(ObjectMapper.Map<Document, DocumentDto>).ToList(),
        };
    }

    [Authorize(CaseEvaluationPermissions.PackageDetails.Create)]
    public virtual async Task<PackageDetailDto> CreateAsync(PackageDetailCreateDto input)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(input.AppointmentTypeId, nameof(input.AppointmentTypeId));

        await EnsureNoActiveDuplicateAsync(input.AppointmentTypeId!.Value, excludingId: null);

        var entity = new PackageDetail(
            id: _guidGenerator.Create(),
            tenantId: CurrentTenant.Id,
            packageName: input.PackageName,
            appointmentTypeId: input.AppointmentTypeId,
            isActive: input.IsActive);

        await _packageRepository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<PackageDetail, PackageDetailDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.PackageDetails.Edit)]
    public virtual async Task<PackageDetailDto> UpdateAsync(Guid id, PackageDetailUpdateDto input)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNull(input.AppointmentTypeId, nameof(input.AppointmentTypeId));

        var entity = await _packageRepository.GetAsync(id);

        var appointmentTypeChanged = entity.AppointmentTypeId != input.AppointmentTypeId;
        var becomingActive = !entity.IsActive && input.IsActive;
        if ((appointmentTypeChanged && input.IsActive) || becomingActive)
        {
            await EnsureNoActiveDuplicateAsync(input.AppointmentTypeId!.Value, excludingId: id);
        }

        entity.PackageName = input.PackageName;
        entity.AppointmentTypeId = input.AppointmentTypeId;
        entity.IsActive = input.IsActive;
        await _packageRepository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<PackageDetail, PackageDetailDto>(entity);
    }

    [Authorize(CaseEvaluationPermissions.PackageDetails.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var links = await _linkRepository.GetListAsync(x => x.PackageDetailId == id);
        if (links.Count > 0)
        {
            await _linkRepository.DeleteManyAsync(links, autoSave: true);
        }

        await _packageRepository.DeleteAsync(id, autoSave: true);
    }

    [Authorize(CaseEvaluationPermissions.PackageDetails.ManageDocuments)]
    public virtual async Task<PackageDetailWithDocumentsDto> LinkDocumentsAsync(
        Guid packageDetailId,
        IReadOnlyList<Guid> documentIds)
    {
        Check.NotNull(documentIds, nameof(documentIds));

        // Touch the package row so we 404 if the package does not exist
        // before any link writes.
        _ = await _packageRepository.GetAsync(packageDetailId);

        var existing = await _linkRepository.GetListAsync(x => x.PackageDetailId == packageDetailId);
        var (toAdd, toRemove) = ComputeLinkSetDiff(packageDetailId, existing, documentIds);

        if (toRemove.Count > 0)
        {
            await _linkRepository.DeleteManyAsync(toRemove, autoSave: false);
        }
        if (toAdd.Count > 0)
        {
            await _linkRepository.InsertManyAsync(toAdd, autoSave: false);
        }

        // Single SaveChanges so callers see the post-link state atomically.
        await CurrentUnitOfWork!.SaveChangesAsync();

        return await GetWithDocumentsAsync(packageDetailId);
    }

    [Authorize(CaseEvaluationPermissions.PackageDetails.ManageDocuments)]
    public virtual async Task UnlinkDocumentAsync(Guid packageDetailId, Guid documentId)
    {
        var link = await _linkRepository.FindAsync(x => x.PackageDetailId == packageDetailId && x.DocumentId == documentId);
        if (link != null)
        {
            await _linkRepository.DeleteAsync(link, autoSave: true);
        }
    }

    /// <summary>
    /// Pure diff between the persisted set of document links and the desired
    /// set the caller supplied. Extracted as <c>internal static</c> so unit
    /// tests can exercise the diff without standing up the full ABP harness
    /// (which currently exhibits a pre-existing test-host crash unrelated
    /// to this work). Mirrors the same testable-helper pattern Phase 3 used
    /// for <c>SystemParametersAppService.ApplyUpdate</c>.
    ///
    /// Idempotency: passing the same set twice yields empty toAdd / toRemove.
    /// Duplicates in <paramref name="desiredDocumentIds"/> are deduplicated.
    /// </summary>
    internal static (List<DocumentPackage> ToAdd, List<DocumentPackage> ToRemove) ComputeLinkSetDiff(
        Guid packageDetailId,
        IReadOnlyCollection<DocumentPackage> existing,
        IReadOnlyList<Guid> desiredDocumentIds)
    {
        var existingIds = existing.Select(x => x.DocumentId).ToHashSet();
        var desiredIds = desiredDocumentIds.Distinct().ToHashSet();

        var toRemove = existing.Where(x => !desiredIds.Contains(x.DocumentId)).ToList();
        var toAdd = desiredIds
            .Where(id => !existingIds.Contains(id))
            .Select(documentId => new DocumentPackage(packageDetailId, documentId, isActive: true))
            .ToList();
        return (toAdd, toRemove);
    }

    private async Task EnsureNoActiveDuplicateAsync(Guid appointmentTypeId, Guid? excludingId)
    {
        var queryable = await _packageRepository.GetQueryableAsync();
        var conflict = queryable.Where(x =>
            x.AppointmentTypeId == appointmentTypeId &&
            x.IsActive &&
            (excludingId == null || x.Id != excludingId));

        if (await AsyncExecuter.AnyAsync(conflict))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.OneActivePackageDetailPerAppointmentType);
        }
    }

    private static System.Linq.IQueryable<PackageDetail> ApplySortingAndPaging(
        System.Linq.IQueryable<PackageDetail> queryable, string sorting, int skipCount, int maxResultCount)
    {
        var ordered = sorting.Trim().ToLowerInvariant() switch
        {
            "packagename desc" => queryable.OrderByDescending(x => x.PackageName),
            "packagename" or "packagename asc" => queryable.OrderBy(x => x.PackageName),
            "creationtime desc" => queryable.OrderByDescending(x => x.CreationTime),
            "creationtime" or "creationtime asc" => queryable.OrderBy(x => x.CreationTime),
            _ => queryable.OrderBy(x => x.PackageName),
        };
        return ordered.Skip(skipCount).Take(maxResultCount);
    }
}
