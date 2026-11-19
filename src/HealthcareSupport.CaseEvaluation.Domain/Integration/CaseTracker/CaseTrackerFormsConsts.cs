namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>Limits for the Case Tracker's blank forms and office list, apart from the feed's own.</summary>
public static class CaseTrackerFormsConsts
{
    /// <summary>
    /// Blank-form downloads an office may make per hour, both forms together. Small on purpose: the Case Tracker
    /// asks about once a day and holds the file by ETag. Its own bucket, so it can never spend the feed's
    /// <see cref="CaseTrackerFeedConsts.RequestsPerHourPerOffice"/>.
    /// </summary>
    public const int FormRequestsPerHourPerOffice = 30;

    /// <summary>Office-list requests per hour across the whole portal (one list, not per office).</summary>
    public const int OfficeListRequestsPerHour = 30;

    /// <summary>The most rendered forms kept in memory. A form is a few MB, and there are only two kinds per office.</summary>
    public const int MaxCachedForms = 64;
}
