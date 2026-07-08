namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// In-app notification category (QA item 7). Each value maps to an icon + tint
/// on the frontend. Deliberately small and inbound-focused: the staff-facing
/// events that need action or awareness (a new request arrived, a change
/// request was submitted, a user asked a question, a document came in, a
/// sent-back request was corrected). Outbound/staff-initiated statuses are
/// intentionally excluded -- staff performed those and the dashboard already
/// has a recent-activity feed.
/// </summary>
public enum AppNotificationType
{
    AppointmentRequested = 1,
    ChangeRequestSubmitted = 2,
    QuerySubmitted = 3,
    DocumentUploaded = 4,
    InfoRequestResubmitted = 5,
}
