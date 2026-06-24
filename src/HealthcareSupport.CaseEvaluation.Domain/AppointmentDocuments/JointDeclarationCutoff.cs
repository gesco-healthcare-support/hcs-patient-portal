using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- pure cutoff predicate for the JDF
/// auto-cancel Hangfire job. An AME appointment with no uploaded JDF
/// auto-cancels when the appointment's due date is within
/// <c>SystemParameter.JointDeclarationUploadCutoffDays</c> of today.
///
/// <para>Mirrors OLD's spec line 419: "In case if the document is
/// pending as of specified number of days before the appointment due
/// date, the appointment will be auto-cancelled and a notification
/// email will be sent to all the stakeholders related to the
/// appointment."</para>
///
/// <para><c>public static</c> -- lives in Domain alongside the
/// runtime job (<c>JointDeclarationAutoCancelJob</c>). Pure: no DI,
/// no IO, deterministic by injected <c>nowUtc</c>. Domain's
/// <c>InternalsVisibleTo</c> covers <c>Domain.Tests</c> +
/// <c>TestBase</c> only, so this stays public for cross-project
/// reach (Application calls it indirectly via the job).</para>
/// </summary>
public static class JointDeclarationCutoff
{
    /// <summary>
    /// Returns true when the appointment's due date is at or past the
    /// cutoff window (inclusive). When <paramref name="dueDateUtc"/>
    /// is null, returns false -- no due date means no cutoff to
    /// enforce, mirroring OLD's behavior of skipping rows without a
    /// committed schedule.
    /// </summary>
    /// <param name="dueDateUtc">
    /// The appointment's <c>DueDate</c> value (UTC midnight per the
    /// canonical NEW storage shape).
    /// </param>
    /// <param name="cutoffDays">
    /// The <c>SystemParameter.JointDeclarationUploadCutoffDays</c>
    /// value. A negative or zero value disables the gate (returns
    /// false) -- matches OLD's implicit "if cutoff is not configured,
    /// don't auto-cancel" behavior.
    /// </param>
    /// <param name="nowUtc">
    /// Current UTC instant. Injected for deterministic tests.
    /// </param>
    public static bool IsAtOrPastCutoff(DateTime? dueDateUtc, int cutoffDays, DateTime nowUtc)
    {
        if (!dueDateUtc.HasValue)
        {
            return false;
        }
        if (cutoffDays <= 0)
        {
            return false;
        }
        var cutoffBoundary = dueDateUtc.Value.AddDays(-cutoffDays);
        return nowUtc >= cutoffBoundary;
    }
}
