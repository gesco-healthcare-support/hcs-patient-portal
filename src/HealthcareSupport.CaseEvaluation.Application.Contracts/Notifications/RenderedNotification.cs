namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- output of
/// <see cref="INotificationTemplateRenderer"/>. Subject and bodies are
/// fully substituted (no <c>##Var##</c> placeholders remaining).
///
/// <para><see cref="BodySms"/> is null when the template's
/// <c>BodySms</c> column is empty -- callers branch on this to skip the
/// SMS leg without throwing.</para>
/// </summary>
public class RenderedNotification
{
    public string Subject { get; init; } = string.Empty;

    public string BodyEmail { get; init; } = string.Empty;

    public string? BodySms { get; init; }

    public RenderedNotification()
    {
    }

    public RenderedNotification(string subject, string bodyEmail, string? bodySms)
    {
        Subject = subject;
        BodyEmail = bodyEmail;
        BodySms = bodySms;
    }
}
