namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Constants for the changes feed the Case Tracker pulls (#927). The numbers were agreed with the Case Tracker
/// side on 2026-09-16 and recorded on the issue; the header and the separate token on 2026-09-24.
/// </summary>
public static class CaseTrackerFeedConsts
{
    /// <summary>
    /// Header carrying the feed token. Deliberately NOT <see cref="CaseTrackerIntegrationConsts.IntegrationTokenHeaderName"/>:
    /// the feed is the most exposed, always-on connection and is read-only, while the integration token also
    /// authorises attendance, which closes appointments. One leak must not grant both, and each must rotate alone.
    /// </summary>
    public const string FeedTokenHeaderName = "X-Feed-Token";

    /// <summary>
    /// Configuration key holding the expected feed token. A SECRET: User Secrets locally, the managed store or
    /// the env file in production, never committed, never logged. Blank means every request is refused.
    /// </summary>
    public const string FeedTokenConfigurationKey = "CaseTracker:FeedToken";

    /// <summary>
    /// Configuration key listing who is emailed about feed problems, separated by <c>;</c> or <c>,</c>. Not a
    /// secret. Technical recipients rather than intake staff: a stalled consumer can only be fixed by whoever
    /// runs the two systems.
    /// </summary>
    public const string AlertRecipientsConfigurationKey = "CaseTracker:FeedAlertRecipients";

    /// <summary>
    /// The outbox column the feed pages on: a SQL Server <c>rowversion</c>, mapped as an EF shadow property
    /// because only the feed's SQL reads it.
    /// </summary>
    public const string ChangeVersionColumn = "ChangeVersion";

    /// <summary>Rows per response, at most.</summary>
    public const int PageSize = 200;

    /// <summary>No request from an office for this long raises the silence alert (fifteen missed polls).</summary>
    public const int SilenceMinutes = 15;

    /// <summary>A row waiting this long while the position has not moved for as long raises the stall alert.</summary>
    public const int StallMinutes = 30;

    /// <summary>
    /// Requests per hour per office: four times the one-a-minute poll, leaving room for retries and catch-up.
    /// Counted in memory, per API instance -- exact while there is one instance, and to move to a shared store
    /// if the API is ever scaled out.
    /// </summary>
    public const int RequestsPerHourPerOffice = 240;

    /// <summary>
    /// Requests per hour per source address that do NOT carry a valid feed token. Keeps a caller without the
    /// token from spending an office's allowance, which is counted only after the token check.
    /// </summary>
    public const int UnauthenticatedRequestsPerHourPerIp = 60;
}
