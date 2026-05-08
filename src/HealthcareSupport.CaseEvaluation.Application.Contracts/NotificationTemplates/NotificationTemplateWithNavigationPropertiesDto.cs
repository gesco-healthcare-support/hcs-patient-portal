namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Combines <see cref="NotificationTemplateDto"/> with its joined
/// <see cref="NotificationTemplateTypeDto"/> for editor-list rendering.
/// Mirrors the existing With-Nav projection pattern used by
/// <c>WcabOfficeWithNavigationPropertiesDto</c>,
/// <c>LocationWithNavigationPropertiesDto</c>, etc.
/// </summary>
public class NotificationTemplateWithNavigationPropertiesDto
{
    public NotificationTemplateDto NotificationTemplate { get; set; } = null!;

    public NotificationTemplateTypeDto? NotificationTemplateType { get; set; }
}
