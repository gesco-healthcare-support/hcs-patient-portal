using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// IT Admin manages per-AppointmentType package templates plus the M:N
/// links between packages and master Documents. Mirrors OLD's REST surface
/// at <c>/api/PackageDetails</c> + <c>/api/packagedetails/{id}/DocumentPackages</c>
/// (POST/GET/PUT/PATCH/DELETE on each), unifying the linking endpoints
/// onto the parent service so the controller stays ABP-conventional.
///
/// Strict-parity rule encoded in <c>CreateAsync</c> + <c>UpdateAsync</c>:
/// at most one active package per AppointmentTypeId. If a deleted row
/// exists with the same name + AppointmentTypeId, OLD reuses its primary
/// key (a soft-undelete pattern); we replicate via ABP's
/// <c>IRepository.RestoreAsync</c> path.
/// </summary>
public interface IPackageDetailsAppService : IApplicationService
{
    Task<PackageDetailDto> GetAsync(Guid id);

    Task<PagedResultDto<PackageDetailDto>> GetListAsync(GetPackageDetailsInput input);

    Task<PackageDetailWithDocumentsDto> GetWithDocumentsAsync(Guid id);

    Task<PackageDetailDto> CreateAsync(PackageDetailCreateDto input);

    Task<PackageDetailDto> UpdateAsync(Guid id, PackageDetailUpdateDto input);

    Task DeleteAsync(Guid id);

    /// <summary>
    /// Replaces the package's linked-Documents set with the supplied IDs.
    /// Existing links not in the new set are removed; new links are inserted;
    /// already-linked rows are left unchanged. Returns the post-link state.
    /// </summary>
    Task<PackageDetailWithDocumentsDto> LinkDocumentsAsync(Guid packageDetailId, IReadOnlyList<Guid> documentIds);

    /// <summary>
    /// Removes a single Document from a package. No-op if the link does
    /// not exist (idempotent per OLD UI contract).
    /// </summary>
    Task UnlinkDocumentAsync(Guid packageDetailId, Guid documentId);
}
