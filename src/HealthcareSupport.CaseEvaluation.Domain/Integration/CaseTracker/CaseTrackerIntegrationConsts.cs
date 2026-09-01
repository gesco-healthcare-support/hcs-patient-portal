namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Constants for the INBOUND direction: the reconcile GET the Case Tracker calls on us. The mirror
/// image of <see cref="CaseTrackerClient"/>'s outbound <c>X-Intake-Token</c>, and deliberately a
/// DIFFERENT secret -- one leaked token must not grant both read access to our appointments and write
/// access to their intake.
/// </summary>
public static class CaseTrackerIntegrationConsts
{
    /// <summary>Header the Case Tracker presents to authenticate a reconcile read.</summary>
    public const string IntegrationTokenHeaderName = "X-Integration-Token";

    /// <summary>
    /// Configuration key holding the expected token. A SECRET: supplied per environment via .NET User
    /// Secrets locally and a managed store in production, never committed. The value must not appear
    /// in any repository file, log line, or exception message.
    /// </summary>
    public const string TokenConfigurationKey = "CaseTracker:IntegrationToken";
}
