using System;
using JetBrains.Annotations;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Filter + paging input for
/// <c>NotificationTemplatesAppService.GetListAsync</c>. Mirrors OLD's
/// <c>spm.spTemplates</c> SP signature (orderBy + sort + page + filter)
/// but uses ABP's standard <see cref="PagedAndSortedResultRequestDto"/>
/// surface.
/// </summary>
public class GetNotificationTemplatesInput : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// Free-text contains-match against <c>TemplateCode</c>. Mirrors OLD's
    /// SP <c>SearchQuery</c> parameter.
    /// </summary>
    [CanBeNull]
    public string? FilterText { get; set; }

    [CanBeNull]
    public Guid? TemplateTypeId { get; set; }

    [CanBeNull]
    public bool? IsActive { get; set; }
}
