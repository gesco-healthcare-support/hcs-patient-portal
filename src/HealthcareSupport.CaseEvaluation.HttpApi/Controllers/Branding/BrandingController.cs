using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Branding;

/// <summary>
/// Manual controller forwarding to <see cref="IBrandingAppService"/>. The two GETs
/// are AllowAnonymous and resolve the office by subdomain so the login page + SPA
/// boot can read a brand pre-auth; the host-central list/preview + the write
/// endpoints are gated in the app service (Branding.Default / Branding.Edit).
/// Mirrors <c>UserSignatureController</c> for the multipart upload + File serve.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("Branding")]
[Route("api/app/branding")]
public class BrandingController : AbpController
{
    private readonly IBrandingAppService _service;

    public BrandingController(IBrandingAppService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public virtual Task<BrandingDto> GetBrandingAsync()
    {
        return _service.GetBrandingAsync();
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public virtual async Task<IActionResult> GetLogoAsync()
    {
        var result = await _service.DownloadLogoAsync();
        return result == null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("offices")]
    public virtual Task<ListResultDto<OfficeBrandingDto>> GetOfficeBrandingsAsync()
    {
        return _service.GetOfficeBrandingsAsync();
    }

    // 2026-06-30 (QA item B): paged + searchable office-branding list for the
    // reusable host-central Branding table. Distinct route from the non-paged
    // /offices above. Gated (Branding.Default) at the app service.
    [HttpGet("offices-paged")]
    public virtual Task<PagedResultDto<OfficeBrandingDto>> GetPagedOfficeBrandingsAsync(
        [FromQuery] GetOfficeBrandingInput input)
    {
        return _service.GetPagedOfficeBrandingsAsync(input);
    }

    [HttpGet("offices/{officeId}/logo")]
    public virtual async Task<IActionResult> GetOfficeLogoAsync(Guid officeId)
    {
        var result = await _service.DownloadLogoForOfficeAsync(officeId);
        return result == null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public virtual async Task<BrandingDto> UploadLogoAsync([FromForm] UploadBrandingLogoForm form, [FromQuery] Guid? officeId)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadLogoAsync(
            officeId,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream);
    }

    [HttpPut("display-name")]
    public virtual Task SetDisplayNameAsync([FromQuery] Guid? officeId, [FromBody] SetBrandingDisplayNameInput input)
    {
        return _service.SetDisplayNameAsync(officeId, input?.DisplayName);
    }

    [HttpDelete("logo")]
    public virtual Task RemoveLogoAsync([FromQuery] Guid? officeId)
    {
        return _service.RemoveLogoAsync(officeId);
    }
}

/// <summary>Form-bound wrapper so Swashbuckle describes the multipart upload as one schema.</summary>
public class UploadBrandingLogoForm
{
    public IFormFile File { get; set; } = null!;
}

/// <summary>Body for the set-display-name endpoint (null/blank clears the name).</summary>
public class SetBrandingDisplayNameInput
{
    public string? DisplayName { get; set; }
}
