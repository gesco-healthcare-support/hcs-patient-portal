using System;
using System.Globalization;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Relative paths on the Case Tracker API, appended to the configured
/// <c>CaseTracker:BaseUrl</c> (which already includes their <c>/evaluators-api-service</c> base).
/// </summary>
public static class CaseTrackerEndpoints
{
    /// <summary>Creates or updates an appointment in their staff review queue.</summary>
    public const string Intake = "api/intake/appointments";

    /// <summary>
    /// Upserts entries in one appointment's document list. The appointment id is in the PATH rather
    /// than the body, so the outbox row's stored target path is self-contained and the drain needs
    /// no knowledge of message shapes to send it.
    /// </summary>
    public static string DocumentUpdate(Guid appointmentId) =>
        string.Create(CultureInfo.InvariantCulture, $"{Intake}/{appointmentId:D}/documents");
}
