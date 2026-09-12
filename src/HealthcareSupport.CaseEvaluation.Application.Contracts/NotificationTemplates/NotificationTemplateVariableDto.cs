namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// One insertable <c>##Var##</c> placeholder for the notification-template
/// editor (B-B2, 2026-06-16). <see cref="Token"/> is the raw variable name the
/// editor wraps as <c>##Token##</c> on insert; <see cref="Label"/> is the
/// humanized display text for the chip.
/// </summary>
public class NotificationTemplateVariableDto
{
    public string Token { get; set; } = null!;

    public string Label { get; set; } = null!;
}
