namespace HealthcareSupport.CaseEvaluation.Enums
{
    /// <summary>
    /// Why an inbound Case Tracker attendance report was refused (#1043).
    ///
    /// <para>Only the two arms that answer 404 from INSIDE the office are listed. The catch-all arm already
    /// logs a warning carrying its exception, and an unrecognised office cannot be attributed to one, so
    /// neither is a reason a human could act on.</para>
    ///
    /// <para>This is what the alert says went wrong. The 404 sent back to the caller stays bodyless and
    /// ambiguous, deliberately, so a token holder cannot enumerate offices or appointment ids -- the ambiguity
    /// is owed to the CALLER, not to us.</para>
    /// </summary>
    public enum CaseTrackerInboundRefusalReason
    {
        /// <summary>The office has the Case Tracker integration switched off.</summary>
        IntegrationDisabled = 0,

        /// <summary>No appointment with that id exists in the office.</summary>
        AppointmentNotFound = 1,
    }
}
