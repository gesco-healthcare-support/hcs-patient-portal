using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// IT Admin master-Document catalog. CRUD over the Document entity plus a
/// blob-aware upload endpoint that wraps the file write to IBlobStorage and
/// the row write to the repository in a single transactional operation.
/// Mirrors OLD's <c>spm.Documents</c> CRUD (POST/GET/PUT/PATCH/DELETE) at
/// <c>P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Document\DocumentController.cs</c>
/// while replacing OLD's local <c>DocumentFilePath</c> with an ABP blob.
/// </summary>
public interface IDocumentsAppService : IApplicationService
{
    Task<DocumentDto> GetAsync(Guid id);

    Task<PagedResultDto<DocumentDto>> GetListAsync(GetDocumentsInput input);

    /// <summary>
    /// Uploads the blank template file to blob storage and creates the
    /// catalog row. The controller hands the AppService the file stream and
    /// metadata; the AppService chooses the BlobName so callers cannot
    /// collide tenant scopes by guessing names.
    /// </summary>
    Task<DocumentDto> CreateAsync(DocumentCreateDto input, Stream fileStream, string fileName);

    Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input);

    /// <summary>
    /// Replaces the blank template file for an existing Document. Generates
    /// a new BlobName, stores the new file, and updates the row. The
    /// previous blob is left in place so any in-flight notifications still
    /// resolve until tenant-side blob retention sweeps it.
    /// </summary>
    Task<DocumentDto> ReplaceFileAsync(Guid id, Stream fileStream, string fileName, string? contentType);

    /// <summary>
    /// Soft-delete via ABP <c>ISoftDelete</c>. Linked DocumentPackages are
    /// not cascaded -- IT Admin must manually unlink before deletion (matches
    /// OLD's manual unlink flow); attempting to delete a referenced Document
    /// throws <c>BusinessException</c>.
    /// </summary>
    Task DeleteAsync(Guid id);
}
