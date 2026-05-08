namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Read-only projection of <see cref="NotificationTemplate"/> with its
/// joined <see cref="NotificationTemplateType"/>. Mirrors the existing
/// <c>{Entity}WithNavigationProperties</c> pattern used by
/// <c>WcabOfficeWithNavigationProperties</c>,
/// <c>LocationWithNavigationProperties</c>, etc. Populated by the EF Core
/// repository via an explicit LEFT JOIN so the editor list page can
/// render template type without a second round-trip.
/// </summary>
public class NotificationTemplateWithNavigationProperties
{
    public NotificationTemplate NotificationTemplate { get; set; } = null!;

    public NotificationTemplateType? NotificationTemplateType { get; set; }
}
