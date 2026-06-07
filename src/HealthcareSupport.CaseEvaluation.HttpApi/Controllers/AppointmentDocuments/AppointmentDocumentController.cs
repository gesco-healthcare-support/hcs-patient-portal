using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Shared;
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

    [HttpGet("document-type-options")]
    public virtual Task<List<LookupDto<Guid>>> GetDocumentTypeOptionsAsync(Guid appointmentId)
    {
        return _service.GetDocumentTypeOptionsAsync(appointmentId);
    }

    [HttpGet("missing-required")]
    public virtual Task<MissingRequiredDocumentsResultDto> GetMissingRequiredDocumentsAsync(Guid appointmentId)
    {
        return _service.GetMissingRequiredDocumentsAsync(appointmentId);
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
            stream,
            form.AppointmentDocumentTypeId,
            form.OtherDocumentTypeName,
            form.IsPanelStrikeList);
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

    [HttpGet("/api/app/appointments/{appointmentId}/documents/combined")]
    public virtual Task<List<PatientPortalDocumentDto>> GetCombinedForAppointmentAsync(Guid appointmentId)
    {
        return _service.GetCombinedForAppointmentAsync(appointmentId);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- package-document upload (authenticated).
    /// Updates an existing Pending row created by
    /// <c>PackageDocumentQueueHandler</c>.
    /// </summary>
    [HttpPost("{id}/upload-package")]
    [Consumes("multipart/form-data")]
    public virtual async Task<AppointmentDocumentDto> UploadPackageAsync(
        Guid appointmentId,
        Guid id,
        [FromForm] UploadAppointmentDocumentForm form)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadPackageDocumentAsync(
            id,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream);
    }

    /// <summary>
    /// Phase 14 (2026-05-04) -- AME Joint Declaration Form upload.
    /// Creates a new <c>IsJointDeclaration = true</c> row.
    /// </summary>
    [HttpPost("upload-jdf")]
    [Consumes("multipart/form-data")]
    public virtual async Task<AppointmentDocumentDto> UploadJointDeclarationAsync(
        Guid appointmentId,
        [FromForm] UploadAppointmentDocumentForm form)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadJointDeclarationAsync(
            appointmentId,
            form.DocumentName ?? "Joint Declaration Form",
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream);
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

    /// <summary>G-03-03 (PR2): chosen document category id (omit for "Other" or untyped).</summary>
    public Guid? AppointmentDocumentTypeId { get; set; }

    /// <summary>G-03-03 (PR2): free-text label when the uploader picks "Other".</summary>
    public string? OtherDocumentTypeName { get; set; }

    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// AF6 (2026-06-05): true when the booker marked this document as the PQME
    /// panel strike list on the booking form. Defaults false; tags the row via
    /// <c>AppointmentDocument.IsPanelStrikeList</c> (AF5) for staff venue
    /// verification.
    /// </summary>
    public bool IsPanelStrikeList { get; set; }
}
