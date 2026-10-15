using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Notifications;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Notifications;

/// <summary>
/// The single HTTP surface for <see cref="IAppNotificationAppService"/>. The service is not auto-exposed;
/// these are the routes the generated Angular client already calls. Authorization lives on the app service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("AppNotification")]
[Route("api/app/app-notification")]
public class AppNotificationController : AbpController, IAppNotificationAppService
{
    private readonly IAppNotificationAppService _service;

    public AppNotificationController(IAppNotificationAppService service)
    {
        _service = service;
    }

    [HttpGet]
    [Route("my-notifications")]
    public virtual Task<PagedResultDto<AppNotificationDto>> GetMyNotificationsAsync(
        [FromQuery] PagedAndSortedResultRequestDto input) =>
        _service.GetMyNotificationsAsync(input);

    [HttpGet]
    [Route("my-unread-count")]
    public virtual Task<int> GetMyUnreadCountAsync() => _service.GetMyUnreadCountAsync();

    [HttpPost]
    [Route("{id}/mark-read")]
    public virtual Task MarkReadAsync(Guid id) => _service.MarkReadAsync(id);

    [HttpPost]
    [Route("mark-all-read")]
    public virtual Task MarkAllReadAsync() => _service.MarkAllReadAsync();
}
