using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
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
    /// <summary>
    /// 2026-11-02: the extra-property key that marks a row whose subject or body a staff member saved,
    /// in the template editor, as something other than the shipped default. The seeder leaves a marked
    /// row alone; before this, every DbMigrator run (which every deploy performs) put the shipped
    /// subject and body back, silently discarding the staff member's edit.
    /// </summary>
    public const string EditedByStaffPropertyName = "EditedByStaff";

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

    /// <summary>True when a staff member's saved subject or body differs from the shipped default.</summary>
    public bool IsEditedByStaff() => this.GetProperty<bool>(EditedByStaffPropertyName);

    /// <summary>
    /// Records whether the editor's last save left the row different from the shipped default. Saving the
    /// shipped content back clears the mark, so later shipped improvements reach the row again.
    /// </summary>
    public void MarkEditedByStaff(bool differsFromShippedDefault)
    {
        if (differsFromShippedDefault)
        {
            this.SetProperty(EditedByStaffPropertyName, true);
        }
        else
        {
            this.RemoveProperty(EditedByStaffPropertyName);
        }
    }
}
