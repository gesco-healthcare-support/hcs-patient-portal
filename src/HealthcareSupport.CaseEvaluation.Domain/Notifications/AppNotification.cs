using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: an in-app notification for a SINGLE internal staff user, created
/// when an inbound event (new request, change request, query, document upload,
/// info-request resubmit) arrives in their office. Per-user fan-out -- one row
/// per recipient -- so read state is per-user (marking mine read never hides
/// yours). IMultiTenant, so it is created + read inside the office DB.
///
/// <para>HIPAA: <see cref="Title"/>/<see cref="Body"/> carry the confirmation
/// number + generic phrasing, NOT patient PHI beyond what staff already see in
/// their queue; <see cref="Url"/> is a relative SPA deep-link to a page the
/// recipient is authorized for.</para>
/// </summary>
public class AppNotification : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>The internal staff user this row belongs to (per-user fan-out).</summary>
    public virtual Guid RecipientUserId { get; protected set; }

    public virtual AppNotificationType NotificationType { get; protected set; }

    public virtual string Title { get; protected set; } = null!;

    public virtual string Body { get; protected set; } = null!;

    /// <summary>Relative SPA deep-link (e.g. <c>/appointments/view/{id}</c>). Optional.</summary>
    public virtual string? Url { get; protected set; }

    public virtual bool IsRead { get; protected set; }

    public virtual DateTime? ReadTime { get; protected set; }

    protected AppNotification()
    {
    }

    public AppNotification(
        Guid id,
        Guid recipientUserId,
        AppNotificationType notificationType,
        string title,
        string body,
        string? url = null,
        Guid? tenantId = null)
        : base(id)
    {
        RecipientUserId = recipientUserId;
        NotificationType = notificationType;
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), AppNotificationConsts.TitleMaxLength);
        Body = Check.NotNullOrWhiteSpace(body, nameof(body), AppNotificationConsts.BodyMaxLength);
        SetUrl(url);
        TenantId = tenantId;
        IsRead = false;
    }

    private void SetUrl(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Check.Length(url, nameof(url), AppNotificationConsts.UrlMaxLength, 0);
        }
        Url = url;
    }

    /// <summary>Idempotent: marks the notification read once, stamping the read time.</summary>
    public virtual void MarkRead(DateTime readTime)
    {
        if (IsRead)
        {
            return;
        }
        IsRead = true;
        ReadTime = readTime;
    }
}
