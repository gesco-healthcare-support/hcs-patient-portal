using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: the signed-in staff user's own in-app notifications. Every method
/// is scoped to <c>CurrentUser.Id</c> within the current office -- no method takes
/// a recipient id, so reaching another user's notifications is structurally
/// impossible. Backs the bell (unread badge + dropdown + mark-read).
/// </summary>
public interface IAppNotificationAppService : IApplicationService
{
    Task<PagedResultDto<AppNotificationDto>> GetMyNotificationsAsync(PagedAndSortedResultRequestDto input);

    Task<int> GetMyUnreadCountAsync();

    Task MarkReadAsync(Guid id);

    Task MarkAllReadAsync();
}
