using Asp.Versioning;
using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Controllers.NotificationTemplates;

/// <summary>
/// Manual HTTP surface for the IT Admin notification-template editor.
/// Mirrors OLD's <c>SystemParametersController</c>'s GET-by-id and PUT
/// shape; OLD's POST / DELETE / SP-paged-list are not ported (Phase 4
/// audit confirmed the editor UI never used them and the seeded
/// 59-template invariant makes Create / Delete unsafe).
///
/// Authorization is enforced at the AppService layer per repo convention
/// (<c>HttpApi/CLAUDE.md</c>); this controller is a pure pass-through.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("NotificationTemplate")]
[Route("api/app/notification-templates")]
public class NotificationTemplatesController : AbpController, INotificationTemplatesAppService
{
    protected INotificationTemplatesAppService NotificationTemplatesAppService { get; }

    public NotificationTemplatesController(INotificationTemplatesAppService notificationTemplatesAppService)
    {
        NotificationTemplatesAppService = notificationTemplatesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<NotificationTemplateWithNavigationPropertiesDto>> GetListAsync(
        GetNotificationTemplatesInput input)
    {
        return NotificationTemplatesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<NotificationTemplateWithNavigationPropertiesDto> GetAsync(Guid id)
    {
        return NotificationTemplatesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-code/{templateCode}")]
    public virtual Task<NotificationTemplateWithNavigationPropertiesDto> GetByCodeAsync(string templateCode)
    {
        return NotificationTemplatesAppService.GetByCodeAsync(templateCode);
    }

    [HttpGet]
    [Route("template-type-lookup")]
    public virtual Task<ListResultDto<NotificationTemplateTypeDto>> GetTypeLookupAsync()
    {
        return NotificationTemplatesAppService.GetTypeLookupAsync();
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<NotificationTemplateDto> UpdateAsync(Guid id, NotificationTemplateUpdateDto input)
    {
        return NotificationTemplatesAppService.UpdateAsync(id, input);
    }
}
