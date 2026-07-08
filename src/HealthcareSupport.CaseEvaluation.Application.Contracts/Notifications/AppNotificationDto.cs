using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: one in-app notification as shown in the bell dropdown. Carries the
/// type (frontend maps it to an icon/tint), the staff-facing title/body, an
/// optional relative deep-link, and read state.
/// </summary>
public class AppNotificationDto : EntityDto<Guid>
{
    public AppNotificationType NotificationType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadTime { get; set; }
    public DateTime CreationTime { get; set; }
}
