using Asp.Versioning;
using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- anonymous upload surface for the
/// per-document verification-code path. Lives at
/// <c>/api/public/appointment-documents/...</c> -- the
/// <c>api/public</c> prefix matches the convention established by
/// <c>ExternalSignupController</c> + <c>ExternalAccountController</c>
/// for public-by-design routes. Rate-limited at the host module
/// level via the same fixed-window limiter pattern as the password
/// reset endpoint (see Phase 10) when configured per the path
/// prefix; until that's wired explicitly here, ABP's global rate
/// limiter still partitions per IP per the
/// <c>CaseEvaluationHttpApiHostModule.ConfigurePasswordResetRateLimiter</c>
/// global-limiter pattern.
/// </summary>
[IgnoreAntiforgeryToken]
[Area("app")]
[ControllerName("PublicDocumentUpload")]
[Route("api/public/appointment-documents")]
public class PublicDocumentUploadController : AbpController
{
    private readonly IAppointmentDocumentsAppService _service;

    public PublicDocumentUploadController(IAppointmentDocumentsAppService service)
    {
        _service = service;
    }

    /// <summary>
    /// Anonymous upload via per-document verification code. The patient
    /// follows the email link, lands on a public-by-design Angular page
    /// (no login), and POSTs the file with the code from the URL. The
    /// AppService validates the code matches and the row is in
    /// Pending/Rejected status; otherwise throws "Un unauthorized user".
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id}/upload-by-code/{verificationCode}")]
    [Consumes("multipart/form-data")]
    public virtual async Task<AppointmentDocumentDto> UploadByVerificationCodeAsync(
        Guid id,
        Guid verificationCode,
        [FromForm] UploadAppointmentDocumentForm form)
    {
        if (form?.File == null || form.File.Length == 0)
        {
            throw new UserFriendlyException("File is required.");
        }
        await using var stream = form.File.OpenReadStream();
        return await _service.UploadByVerificationCodeAsync(
            id,
            verificationCode,
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream);
    }
}
