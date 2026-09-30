using Asp.Versioning;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.UserProfile;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.UserProfile;

/// <summary>
/// Manual controller forwarding to <see cref="IUserSignatureAppService"/>.
/// All operations scope to the current user; there is no admin /
/// per-user-target surface here. The internal
/// <c>GetBytesByUserIdAsync</c> stays in-process for the packet resolver
/// and is intentionally NOT exposed.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("UserSignature")]
[Route("api/app/user-signatures/me")]
public class UserSignatureController : AbpController
{
    private readonly IUserSignatureAppService _service;

    public UserSignatureController(IUserSignatureAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<UserSignatureInfoDto> GetInfoAsync()
    {
        return _service.GetInfoAsync();
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public virtual async Task<UserSignatureInfoDto> UploadAsync([FromForm] UploadUserSignatureForm form)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadAsync(
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream);
    }

    [HttpGet("download")]
    public virtual async Task<IActionResult> DownloadAsync()
    {
        var result = await _service.DownloadAsync();
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpDelete]
    public virtual Task DeleteAsync()
    {
        return _service.DeleteAsync();
    }
}

/// <summary>
/// Form-bound wrapper so Swashbuckle describes the multipart upload as
/// a single schema. Mirrors the pattern from
/// <c>AppointmentDocumentController.UploadAppointmentDocumentForm</c>.
/// </summary>
public class UploadUserSignatureForm
{
    public IFormFile File { get; set; } = null!;
}
