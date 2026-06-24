using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11e (2026-05-04) -- pure predicates for the OLD-parity Re-Request
/// (Re-Submit) and Re-eval (Reval) booking flows. Mirrors
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// lines 162-184 (validation block) and 240-275 (Add path branching).
///
/// Phase 11g (2026-05-04) -- promoted from <c>internal</c> (Application
/// project) to <c>public</c> (Domain project) so
/// <see cref="AppointmentManager"/> can compose the predicates without an
/// architectural inversion. The helpers remain pure and free of
/// repository / DI concerns; the Manager orchestrates repository
/// lookups around them.
/// </summary>
public static class AppointmentLifecycleValidators
{
    /// <summary>
    /// Re-Submit (OLD <c>IsReRequestForm</c>) is allowed only when the
    /// source appointment is in status <see cref="AppointmentStatusType.Rejected"/>.
    /// OLD validation message (verbatim, line 181):
    /// "You not allowed to re apply appointment".
    /// </summary>
    public static bool CanResubmit(AppointmentStatusType sourceStatus)
    {
        return sourceStatus == AppointmentStatusType.Rejected;
    }

    /// <summary>
    /// Reval (OLD <c>IsRevolutionForm</c>) is allowed when the source
    /// appointment is in status <see cref="AppointmentStatusType.Approved"/>.
    /// OLD additionally allows IT Admin to invoke Reval on a non-Approved
    /// source as an admin-override path (line 167-174); the override is
    /// expressed here via the <paramref name="callerIsItAdmin"/> flag so
    /// the caller does not have to duplicate the role check at every
    /// invocation site.
    /// </summary>
    /// <remarks>
    /// Per OLD line 171-173, when the caller IS IT Admin but the source is
    /// still not Approved, OLD shows the message "You can not Re-eval this
    /// appointment request because it's not yet approved. Please approve an
    /// appointment and try again." -- i.e. the admin override is NOT a free
    /// pass; it surfaces a different message but still rejects. We mirror
    /// this by returning false for non-Approved + non-admin AND for
    /// non-Approved + admin alike, and let the caller pick the right error
    /// code (<see cref="ResolveRevalRejectionCode"/>).
    /// </remarks>
    public static bool CanCreateReval(AppointmentStatusType sourceStatus, bool callerIsItAdmin)
    {
        // Strict OLD parity: admin override surfaces a different message but
        // does NOT bypass the gate. See remarks.
        return sourceStatus == AppointmentStatusType.Approved;
    }

    /// <summary>
    /// Returns the OLD-parity error code for a Reval rejection. The two
    /// branches map to OLD's two distinct messages (line 168 vs line 172).
    /// </summary>
    public static string ResolveRevalRejectionCode(bool callerIsItAdmin)
    {
        return callerIsItAdmin
            ? CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApprovedAdminHint
            : CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApproved;
    }

    /// <summary>
    /// Re-Submit reuses the source appointment's confirmation number
    /// verbatim (OLD line 263-266: <c>appointment.RequestConfirmationNumber
    /// = appointment.RequestConfirmationNumber;</c> -- the OLD code is a
    /// no-op self-assign because the entity already carries the source's
    /// number). NEW must explicitly carry the source's number forward
    /// because the new appointment is a brand-new aggregate, not the same
    /// entity instance OLD mutated.
    ///
    /// Reval generates a fresh confirmation number (OLD line 268).
    /// </summary>
    public static string ResolveConfirmationNumber(
        AppointmentLifecycleFlow flow,
        string sourceConfirmationNumber,
        string newlyGeneratedConfirmationNumber)
    {
        if (string.IsNullOrWhiteSpace(sourceConfirmationNumber))
        {
            throw new System.ArgumentException(
                "sourceConfirmationNumber must be supplied for both ReSubmit and Reval flows.",
                nameof(sourceConfirmationNumber));
        }

        if (string.IsNullOrWhiteSpace(newlyGeneratedConfirmationNumber))
        {
            throw new System.ArgumentException(
                "newlyGeneratedConfirmationNumber must be supplied so the Reval path has a fresh number to use.",
                nameof(newlyGeneratedConfirmationNumber));
        }

        return flow switch
        {
            AppointmentLifecycleFlow.ReSubmit => sourceConfirmationNumber,
            AppointmentLifecycleFlow.Reval => newlyGeneratedConfirmationNumber,
            _ => throw new System.ArgumentOutOfRangeException(nameof(flow), flow, "Unknown AppointmentLifecycleFlow."),
        };
    }
}

/// <summary>
/// Discriminator passed to <see cref="AppointmentLifecycleValidators.ResolveConfirmationNumber"/>
/// so the helper does not need a boolean flag at the call site. The two
/// flows differ only in whether the source confirmation number is carried
/// forward (<see cref="ReSubmit"/>) or replaced with a freshly generated
/// one (<see cref="Reval"/>).
/// </summary>
public enum AppointmentLifecycleFlow
{
    ReSubmit = 1,
    Reval = 2,
}
