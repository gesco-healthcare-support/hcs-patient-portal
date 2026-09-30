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
    public static bool CanCreateReval(
        AppointmentStatusType sourceStatus,
        EvaluationKind sourceKind,
        bool callerIsItAdmin)
    {
        // Strict OLD parity: admin override surfaces a different message but
        // does NOT bypass the gate. See remarks.
        if (sourceStatus == AppointmentStatusType.Approved)
        {
            return true;
        }

        // Phase 5 (2026-08-07), decision 1 -- a DELIBERATE break from the OLD
        // parity rule in the remarks above, recorded rather than slipped in. A
        // re-evaluation that no-showed may be re-booked as another re-eval,
        // because an earlier evaluation already established the need for one. A
        // FIRST evaluation that no-showed may not: nothing has established that
        // need yet, so the client submits a new appointment request instead.
        return IsAttendanceOutcome(sourceStatus) && sourceKind == EvaluationKind.ReEvaluation;
    }

    /// <summary>
    /// Whether a status means the appointment produced NO evaluation -- the patient
    /// never arrived (<see cref="AppointmentStatusType.NoShow"/>) or arrived but was
    /// not seen (<see cref="AppointmentStatusType.NotSeen"/>).
    ///
    /// <para>THE single definition of that set. Both statuses are authored in the
    /// Case Tracker; anything reasoning about "the appointment happened but produced
    /// nothing" asks here rather than restating the pair, so a third cause later is
    /// one edit instead of a hunt.</para>
    /// </summary>
    public static bool IsAttendanceOutcome(AppointmentStatusType status)
    {
        return status is AppointmentStatusType.NoShow or AppointmentStatusType.NotSeen;
    }

    /// <summary>
    /// Returns the error code for a Reval rejection. The two OLD-parity branches map
    /// to OLD's distinct messages (line 168 vs line 172); phase 5 adds a third that
    /// is not about approval at all.
    /// </summary>
    /// <summary>
    /// Item 4 (2026-08-17) -- Re-book is allowed only when the source appointment did NOT
    /// happen: cancelled (either billing outcome) or an attendance outcome.
    ///
    /// <para>Deliberately NOT <c>Rejected</c> -- a rejected request never became an
    /// appointment, and re-submitting it is the existing ReSubmit flow, which reuses the
    /// original confirmation number rather than minting a new one.</para>
    ///
    /// <para>Deliberately NOT <c>Approved</c> either: an approved appointment is still
    /// expected to happen, so there is nothing to replace. Re-booking one would strand a
    /// live appointment.</para>
    /// </summary>
    public static bool CanCreateReBook(AppointmentStatusType sourceStatus)
    {
        return sourceStatus is AppointmentStatusType.CancelledNoBill
            or AppointmentStatusType.CancelledLate
            || IsAttendanceOutcome(sourceStatus);
    }

    /// <summary>
    /// Returns the error code for a re-book rejection. Mirrors
    /// <see cref="ResolveRevalRejectionCode"/>: internal staff get a distinct message, but
    /// the override is NOT a free pass -- both codes describe a refusal.
    /// </summary>
    public static string ResolveReBookRejectionCode(
        AppointmentStatusType sourceStatus,
        bool callerIsInternal)
    {
        return callerIsInternal
            ? CaseEvaluationDomainErrorCodes.AppointmentReBookSourceNotEligibleStaffHint
            : CaseEvaluationDomainErrorCodes.AppointmentReBookSourceNotEligible;
    }

    public static string ResolveRevalRejectionCode(
        AppointmentStatusType sourceStatus,
        EvaluationKind sourceKind,
        bool callerIsItAdmin)
    {
        // Checked BEFORE the admin split and independently of it: the admin hint
        // says "approve an appointment and try again", which is impossible advice
        // here -- a no-showed appointment can never return to Approved. Telling an
        // admin to do the impossible is worse than telling them nothing.
        if (IsAttendanceOutcome(sourceStatus) && sourceKind == EvaluationKind.Evaluation)
        {
            return CaseEvaluationDomainErrorCodes.AppointmentRevalSourceIncompleteFirstEvaluation;
        }

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
            // CHANGED 2026-08-22 (Adrian, via modal). Re-submit used to carry the source's number
            // forward, for OLD parity. It could not: the unique index on
            // (TenantId, RequestConfirmationNumber) filtered on IsDeleted = 0 is still satisfied by
            // the rejected source row, so every re-submit died on that constraint. No test ever
            // caught it because they all assert refusals.
            //
            // A fresh number is also the answer this file already gives for re-book, for the same
            // stated reason: the source still exists and keeps its number, so sharing one makes the
            // two indistinguishable in our lists and in the Case Tracker's folder labels. The link
            // back is carried on RescheduledFromAppointmentId instead.
            AppointmentLifecycleFlow.ReSubmit => newlyGeneratedConfirmationNumber,
            AppointmentLifecycleFlow.Reval => newlyGeneratedConfirmationNumber,
            // Item 4 (2026-08-17): a re-book mints its own number. The source still exists
            // as a cancelled / no-showed / not-seen record and keeps its number, so sharing
            // one would make the two indistinguishable in our lists and in the Case
            // Tracker's folder labels.
            AppointmentLifecycleFlow.ReBook => newlyGeneratedConfirmationNumber,
            _ => throw new System.ArgumentOutOfRangeException(nameof(flow), flow, "Unknown AppointmentLifecycleFlow."),
        };
    }
}

/// <summary>
/// Discriminator passed to <see cref="AppointmentLifecycleValidators.ResolveConfirmationNumber"/>
/// so the helper does not need a boolean flag at the call site.
///
/// <para>As of 2026-08-22 ALL THREE flows mint a freshly generated confirmation number. ReSubmit
/// used to carry the source's forward; it could not, because the unique index on
/// (TenantId, RequestConfirmationNumber) is still satisfied by the source row. The flows now differ
/// only in which link column records the source, and in which eligibility gate applies.</para>
/// </summary>
public enum AppointmentLifecycleFlow
{
    ReSubmit = 1,
    Reval = 2,

    /// <summary>
    /// Item 4 (2026-08-17) -- book again after an appointment that did NOT happen
    /// (cancelled, no-showed or not-seen).
    ///
    /// <para>Deliberately a third flow rather than a widened Reval gate. Reval means
    /// "follow-up to an exam that happened"; this is "the exam never happened, book it
    /// again". Conflating them would corrupt <c>EvaluationKind</c>, which the Case Tracker
    /// uses to label a case folder. A re-book yields <c>EvaluationKind.Evaluation</c> --
    /// a first evaluation that finally takes place -- which
    /// <c>EvaluationKindPolicy.FromLifecycleFlow</c> already produces for anything that is
    /// not <see cref="Reval"/>, so no change was needed there.</para>
    /// </summary>
    ReBook = 3,
}
