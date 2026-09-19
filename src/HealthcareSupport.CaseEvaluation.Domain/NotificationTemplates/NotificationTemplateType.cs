using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Lookup for the channel a notification template is sent through (Email,
/// SMS). Mirrors OLD's <c>TemplateType</c> table verbatim (Phase 1.3,
/// 2026-05-01). Per-office under database-per-office (B4): NotificationTemplate
/// (IMultiTenant) FKs to it, so each office database carries its own seeded copy
/// (Email + SMS) -- a "seeded fixed copy" catalog, like State / AppointmentType.
/// </summary>
public class NotificationTemplateType : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }

    [NotNull]
    public virtual string Name { get; set; } = null!;

    public virtual bool IsActive { get; set; }

    protected NotificationTemplateType()
    {
    }

    public NotificationTemplateType(Guid id, string name, bool isActive = true)
    {
        Id = id;
        Check.NotNullOrWhiteSpace(name, nameof(name));
        Check.Length(name, nameof(name), NotificationTemplateTypeConsts.NameMaxLength);
        Name = name;
        IsActive = isActive;
    }
}
