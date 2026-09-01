using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Per-tenant editable email + SMS template addressed by stable
/// <c>TemplateCode</c>. Mirrors OLD's <c>Template</c> table (Phase 1.3,
/// 2026-05-01) with the OLD <c>TemplateCode</c> int enum replaced by a
/// stable string code that survives schema migrations.
///
/// Notification handlers across the workflow load by code via
/// <see cref="INotificationTemplateRepository.FindByCodeAsync"/>, render
/// the body against a strongly-typed model, and send via ABP's
/// <c>IEmailSender</c> / <c>ISmsSender</c>.
/// </summary>
[Audited]
public class NotificationTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string TemplateCode { get; set; } = null!;

    public virtual Guid TemplateTypeId { get; set; }

    [CanBeNull]
    public virtual string? Subject { get; set; }

    [NotNull]
    public virtual string BodyEmail { get; set; } = null!;

    [NotNull]
    public virtual string BodySms { get; set; } = null!;

    [CanBeNull]
    public virtual string? Description { get; set; }

    public virtual bool IsActive { get; set; }

    protected NotificationTemplate()
    {
    }

    public NotificationTemplate(
        Guid id,
        Guid? tenantId,
        string templateCode,
        Guid templateTypeId,
        string? subject,
        string bodyEmail,
        string bodySms,
        string? description = null,
        bool isActive = true)
    {
        Id = id;
        TenantId = tenantId;
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));
        Check.Length(templateCode, nameof(templateCode), NotificationTemplateConsts.TemplateCodeMaxLength);
        Check.Length(subject, nameof(subject), NotificationTemplateConsts.SubjectMaxLength);
        Check.NotNull(bodyEmail, nameof(bodyEmail));
        Check.NotNull(bodySms, nameof(bodySms));
        Check.Length(description, nameof(description), NotificationTemplateConsts.DescriptionMaxLength);
        TemplateCode = templateCode;
        TemplateTypeId = templateTypeId;
        Subject = subject;
        BodyEmail = bodyEmail;
        BodySms = bodySms;
        Description = description;
        IsActive = isActive;
    }
}
