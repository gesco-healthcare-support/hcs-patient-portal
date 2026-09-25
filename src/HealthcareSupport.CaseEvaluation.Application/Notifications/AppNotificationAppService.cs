using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: serves the signed-in staff user's own in-app notifications for the
/// bell. Every query filters on <c>RecipientUserId == CurrentUser.Id</c>; combined
/// with the IMultiTenant filter (current office) this enforces per-user +
/// per-office isolation with no method accepting a recipient id. Bare
/// <c>[Authorize]</c> is the correct gate: only internal staff ever have rows
/// (the raiser fans out only to staff) and the bell renders only in the internal
/// shell, so an external caller simply gets an empty list.
/// </summary>
[Authorize]
public class AppNotificationAppService : CaseEvaluationAppService, IAppNotificationAppService
{
    private readonly IRepository<AppNotification, Guid> _notificationRepository;

    public AppNotificationAppService(IRepository<AppNotification, Guid> notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public virtual async Task<PagedResultDto<AppNotificationDto>> GetMyNotificationsAsync(
        PagedAndSortedResultRequestDto input)
    {
        var userId = CurrentUser.GetId();
        var query = (await _notificationRepository.GetQueryableAsync())
            .Where(x => x.RecipientUserId == userId);

        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<AppNotificationDto>(totalCount, items.Select(Map).ToList());
    }

    public virtual async Task<int> GetMyUnreadCountAsync()
    {
        var userId = CurrentUser.GetId();
        return await _notificationRepository.CountAsync(x => x.RecipientUserId == userId && !x.IsRead);
    }

    public virtual async Task MarkReadAsync(Guid id)
    {
        var userId = CurrentUser.GetId();
        // Per-user isolation: only the owner's row resolves (and the IMultiTenant
        // filter scopes to the current office); another user's / office's id yields
        // null -> silent no-op rather than touching a row that is not the caller's.
        var notification = await _notificationRepository.FirstOrDefaultAsync(
            x => x.Id == id && x.RecipientUserId == userId);
        if (notification == null)
        {
            return;
        }

        notification.MarkRead(Clock.Now);
        await _notificationRepository.UpdateAsync(notification, autoSave: true);
    }

    public virtual async Task MarkAllReadAsync()
    {
        var userId = CurrentUser.GetId();
        var query = (await _notificationRepository.GetQueryableAsync())
            .Where(x => x.RecipientUserId == userId && !x.IsRead);
        var unread = await AsyncExecuter.ToListAsync(query);
        if (unread.Count == 0)
        {
            return;
        }

        var now = Clock.Now;
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }
        await _notificationRepository.UpdateManyAsync(unread, autoSave: true);
    }

    private static AppNotificationDto Map(AppNotification n) => new()
    {
        Id = n.Id,
        NotificationType = n.NotificationType,
        Title = n.Title,
        Body = n.Body,
        Url = n.Url,
        IsRead = n.IsRead,
        ReadTime = n.ReadTime,
        CreationTime = n.CreationTime,
    };
}
