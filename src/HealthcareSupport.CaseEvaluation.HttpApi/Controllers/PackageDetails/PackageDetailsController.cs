using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.PackageDetailsControllers;

/// <summary>
/// Manual HTTP surface for IT Admin per-AppointmentType package templates
/// + the link table to master Documents. Mirrors OLD
/// <c>PackageDetailController</c> + <c>DocumentPackageController</c> with
/// the linking endpoints unified onto the parent route per ABP convention.
///
/// Routes:
///   GET    /api/app/package-details
///   GET    /api/app/package-details/{id}
///   GET    /api/app/package-details/{id}/with-documents
///   POST   /api/app/package-details
///   PUT    /api/app/package-details/{id}
///   DELETE /api/app/package-details/{id}
///   PUT    /api/app/package-details/{id}/documents       (replace link set)
///   DELETE /api/app/package-details/{id}/documents/{documentId}
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("PackageDetails")]
[Route("api/app/package-details")]
public class PackageDetailsController : AbpController
{
    protected IPackageDetailsAppService PackageDetailsAppService { get; }

    public PackageDetailsController(IPackageDetailsAppService packageDetailsAppService)
    {
        PackageDetailsAppService = packageDetailsAppService;
    }

    [HttpGet("{id}")]
    public virtual Task<PackageDetailDto> GetAsync(Guid id)
    {
        return PackageDetailsAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<PackageDetailDto>> GetListAsync([FromQuery] GetPackageDetailsInput input)
    {
        return PackageDetailsAppService.GetListAsync(input);
    }

    [HttpGet("{id}/with-documents")]
    public virtual Task<PackageDetailWithDocumentsDto> GetWithDocumentsAsync(Guid id)
    {
        return PackageDetailsAppService.GetWithDocumentsAsync(id);
    }

    [HttpPost]
    public virtual Task<PackageDetailDto> CreateAsync(PackageDetailCreateDto input)
    {
        return PackageDetailsAppService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    public virtual Task<PackageDetailDto> UpdateAsync(Guid id, PackageDetailUpdateDto input)
    {
        return PackageDetailsAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return PackageDetailsAppService.DeleteAsync(id);
    }

    [HttpPut("{id}/documents")]
    public virtual Task<PackageDetailWithDocumentsDto> LinkDocumentsAsync(Guid id, [FromBody] LinkDocumentsRequest request)
    {
        return PackageDetailsAppService.LinkDocumentsAsync(id, request.DocumentIds);
    }

    [HttpDelete("{id}/documents/{documentId}")]
    public virtual Task UnlinkDocumentAsync(Guid id, Guid documentId)
    {
        return PackageDetailsAppService.UnlinkDocumentAsync(id, documentId);
    }
}

/// <summary>
/// Body shape for the bulk link replacement endpoint. Kept here (not in
/// Application.Contracts) because it is a controller-only convenience for
/// JSON deserialization; the AppService accepts the bare list.
/// </summary>
public class LinkDocumentsRequest
{
    public List<Guid> DocumentIds { get; set; } = new();
}
