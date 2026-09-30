namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Column bounds for <c>AppNotification</c> (QA item 7). Title/Body are short,
/// NON-PHI staff-facing strings (confirmation number + generic phrasing); Url is
/// a relative SPA deep-link.
/// </summary>
public static class AppNotificationConsts
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 500;
    public const int UrlMaxLength = 400;
}
