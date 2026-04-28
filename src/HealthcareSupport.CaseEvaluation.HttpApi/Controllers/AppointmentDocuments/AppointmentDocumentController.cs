using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocuments;

[RemoteService]
[Area("app")]
[ControllerName("AppointmentDocument")]
[Route("api/app/appointments/{appointmentId}/documents")]
public class AppointmentDocumentController : AbpController
{
    private readonly IAppointmentDocumentsAppService _service;

    public AppointmentDocumentController(IAppointmentDocumentsAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<List<AppointmentDocumentDto>> GetListAsync(Guid appointmentId)
    {
        return _service.GetListByAppointmentAsync(appointmentId);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public virtual async Task<AppointmentDocumentDto> UploadAsync(
        Guid appointmentId,
        [FromForm] string? documentName,
        [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = file.OpenReadStream();
        return await _service.UploadStreamAsync(
            appointmentId,
            documentName ?? string.Empty,
            file.FileName,
            file.ContentType,
            file.Length,
            stream);
    }

    [HttpGet("{id}/download")]
    public virtual async Task<IActionResult> DownloadAsync(Guid appointmentId, Guid id)
    {
        var result = await _service.DownloadAsync(id);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid appointmentId, Guid id)
    {
        return _service.DeleteAsync(id);
    }
}
