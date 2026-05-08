using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Lookup for the channel a notification template is sent through (Email,
/// SMS). Mirrors OLD's <c>TemplateType</c> table verbatim (Phase 1.3,
/// 2026-05-01). Host-scoped: shared across tenants; only IT Admin (host
/// scope) can manage. Two seeded rows: Email + SMS.
/// </summary>
public class NotificationTemplateType : FullAuditedEntity<Guid>
{
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
