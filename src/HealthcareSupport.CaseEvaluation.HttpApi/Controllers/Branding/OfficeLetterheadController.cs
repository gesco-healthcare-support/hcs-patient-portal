using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Branding;

/// <summary>
/// Manual controller forwarding to <see cref="IOfficeLetterheadAppService"/> (the app service
/// carries <c>[RemoteService(IsEnabled = false)]</c>, so this is its only route). Authorization
/// is enforced in the app service: Branding.Default to read, Branding.Edit to save.
/// </summary>
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
}
