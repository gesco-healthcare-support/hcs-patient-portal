using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- pure cutoff predicate for the JDF
/// overdue Hangfire job. An AME appointment with no uploaded JDF is
/// overdue when its due date is within
/// <c>SystemParameter.JointDeclarationUploadCutoffDays</c> of today.
///
/// <para>The predicate itself is unchanged since Phase 14; only the
/// CONSEQUENCE changed. It came from OLD's spec line 419, which said
/// such an appointment "will be auto-cancelled and a notification
/// email will be sent to all the stakeholders". That is no longer what
/// happens: since 2026-08-08 the appointment is flagged for staff and
/// nothing is cancelled. Kept here as provenance for the WINDOW, not
/// as a description of current behaviour.</para>
///
/// <para><c>public static</c> -- lives in Domain alongside the
/// runtime job (<c>JointDeclarationOverdueJob</c>). Pure: no DI,
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
    /// do nothing" behavior.
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
