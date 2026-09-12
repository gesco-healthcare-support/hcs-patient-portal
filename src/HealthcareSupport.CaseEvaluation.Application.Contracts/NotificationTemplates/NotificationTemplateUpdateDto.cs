using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Input DTO for <c>NotificationTemplatesAppService.UpdateAsync</c>.
///
/// IT Admin can edit Subject + BodyEmail + BodySms + IsActive. The
/// remaining fields (<c>TemplateCode</c>, <c>TemplateTypeId</c>,
/// <c>Description</c>) are immutable on the update path because:
/// <list type="bullet">
///   <item><c>TemplateCode</c> is the lookup key used by every notification
///         handler (<c>FindByCodeAsync</c>); renaming it would silently
///         break every wired-up event.</item>
///   <item><c>TemplateTypeId</c> categorises Email vs SMS as host-scoped
///         lookup; changing it would shift the row's category without
///         migrating bodies.</item>
///   <item><c>Description</c> is admin-internal documentation and not
///         user-facing; deferred to a future admin-tooling phase.</item>
/// </list>
///
/// Carries <see cref="ConcurrencyStamp"/> for optimistic concurrency
/// (mirrors Phase 3's <c>SystemParameter</c> pattern). OLD did not have
/// a concurrency column; treated as OLD-bug-fix exception.
/// </summary>
public class NotificationTemplateUpdateDto : IHasConcurrencyStamp
{
    [CanBeNull]
    [StringLength(NotificationTemplateConsts.SubjectMaxLength)]
    public string? Subject { get; set; }

    [Required]
    public string BodyEmail { get; set; } = null!;

    [Required]
    public string BodySms { get; set; } = null!;

    public bool IsActive { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}
