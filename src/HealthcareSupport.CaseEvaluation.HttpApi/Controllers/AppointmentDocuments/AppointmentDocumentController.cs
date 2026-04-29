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
        [FromForm] UploadAppointmentDocumentForm form)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadStreamAsync(
            appointmentId,
            form.DocumentName ?? string.Empty,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
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

    [HttpPost("{id}/approve")]
    public virtual Task<AppointmentDocumentDto> ApproveAsync(Guid appointmentId, Guid id)
    {
        return _service.ApproveAsync(id);
    }

    [HttpPost("{id}/reject")]
    public virtual Task<AppointmentDocumentDto> RejectAsync(Guid appointmentId, Guid id, [FromBody] RejectDocumentInput input)
    {
        return _service.RejectAsync(id, input);
    }

    [HttpPost("/api/app/appointments/{appointmentId}/packet/regenerate")]
    public virtual Task RegeneratePacketAsync(Guid appointmentId)
    {
        return _service.RegeneratePacketAsync(appointmentId);
    }
}

/// <summary>
/// Form-bound wrapper so Swashbuckle can describe the multipart upload as a
/// single schema (it refuses to mix [FromForm] string + [FromForm] IFormFile
/// at the action signature level).
/// </summary>
public class UploadAppointmentDocumentForm
{
    public string? DocumentName { get; set; }
    public IFormFile File { get; set; } = null!;
}
