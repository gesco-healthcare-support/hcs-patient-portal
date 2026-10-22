using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Branding;

/// <summary>
/// Manual controller forwarding to <see cref="IOfficeLetterheadAppService"/> (the app service
/// carries <c>[RemoteService(IsEnabled = false)]</c>, so this is its only route). Authorization
/// is enforced in the app service: Branding.Default to read, Branding.Edit to save. The
/// class-level <c>[Authorize]</c> additionally refuses anonymous callers at the HTTP layer.
/// </summary>
[Authorize]
[RemoteService]
[Area("app")]
[ControllerName("OfficeLetterhead")]
[Route("api/app/branding/letterhead")]
public class OfficeLetterheadController : AbpController, IOfficeLetterheadAppService
{
    private readonly IOfficeLetterheadAppService _service;

    public OfficeLetterheadController(IOfficeLetterheadAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<OfficeLetterheadDto> GetAsync()
    {
        return _service.GetAsync();
    }

    [HttpPut]
    public virtual Task<OfficeLetterheadDto> UpdateAsync([FromBody] UpdateOfficeLetterheadInput input)
    {
        return _service.UpdateAsync(input);
    }

    /// <summary>Host only: one office's letterhead, beside its display name and logo on the host branding page.</summary>
    [HttpGet("~/api/app/branding/offices/{officeId}/letterhead")]
    public virtual Task<OfficeLetterheadDto> GetForOfficeAsync(Guid officeId)
    {
        return _service.GetForOfficeAsync(officeId);
    }

    [HttpPut("~/api/app/branding/offices/{officeId}/letterhead")]
    public virtual Task<OfficeLetterheadDto> UpdateForOfficeAsync(Guid officeId, [FromBody] UpdateOfficeLetterheadInput input)
    {
        return _service.UpdateForOfficeAsync(officeId, input);
    }
}
