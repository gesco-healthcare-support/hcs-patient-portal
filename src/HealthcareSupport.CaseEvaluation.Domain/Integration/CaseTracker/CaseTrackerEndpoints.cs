namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Relative paths on the Case Tracker API, appended to the configured
/// <c>CaseTracker:BaseUrl</c> (which already includes their <c>/evaluators-api-service</c> base).
/// </summary>
public static class CaseTrackerEndpoints
{
    /// <summary>Creates or updates an appointment in their staff review queue.</summary>
    public const string Intake = "api/intake/appointments";
}
