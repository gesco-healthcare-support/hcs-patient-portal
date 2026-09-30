using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.DocumentsControllers;

/// <summary>
/// Manual HTTP surface for the IT Admin master-Document catalog. Mirrors
/// OLD <c>DocumentController</c> (POST / GET / PUT / DELETE) at
/// <c>P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Document\DocumentController.cs</c>.
/// File upload uses ASP.NET Core multipart per ABP convention; the
/// controller hands the stream + name to the AppService which owns the
/// blob-name + repository write.
///
/// Authorization is enforced at the AppService layer (Phase 5 plan + repo
/// convention); this controller is a pure pass-through.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("Documents")]
[Route("api/app/documents")]
public class DocumentsController : AbpController
{
    protected IDocumentsAppService DocumentsAppService { get; }

    public DocumentsController(IDocumentsAppService documentsAppService)
    {
        DocumentsAppService = documentsAppService;
    }

    [HttpGet("{id}")]
    public virtual Task<DocumentDto> GetAsync(Guid id)
    {
        return DocumentsAppService.GetAsync(id);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentDto>> GetListAsync([FromQuery] GetDocumentsInput input)
    {
        return DocumentsAppService.GetListAsync(input);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public virtual async Task<DocumentDto> CreateAsync(
        [FromForm] DocumentCreateDto input,
        [Required] IFormFile file)
    {
        Check.NotNull(file, nameof(file));
        await using var stream = file.OpenReadStream();
        return await DocumentsAppService.CreateAsync(input, stream, file.FileName);
    }

    [HttpPut("{id}")]
    public virtual Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input)
    {
        return DocumentsAppService.UpdateAsync(id, input);
    }

    [HttpPost("{id}/file")]
    [Consumes("multipart/form-data")]
    public virtual async Task<DocumentDto> ReplaceFileAsync(Guid id, [Required] IFormFile file)
    {
        Check.NotNull(file, nameof(file));
        await using var stream = file.OpenReadStream();
        return await DocumentsAppService.ReplaceFileAsync(id, stream, file.FileName, file.ContentType);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return DocumentsAppService.DeleteAsync(id);
    }
}
