namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 -- everything the booking form copies from an earlier appointment into a
/// re-evaluation, in one answer. A caller who is not a party to the earlier appointment cannot make
/// the per-appointment reads the form used to make (each one checks access), so this carries the
/// whole graph instead.
/// </summary>
public class ReEvaluationSourceDto
{
    /// <summary>
    /// The earlier appointment with its patient, employer, injuries, claim examiner and insurance.
    /// For a caller who is not a party, the Authorized Users, the patient's login and the booker's
    /// login are left out and the Social Security number is empty.
    /// </summary>
    public AppointmentWithNavigationPropertiesDto Source { get; set; } = null!;

    /// <summary>The applicant attorney as booked, or null when the appointment has none.</summary>
    public ApplicantAttorneyDetailsDto? ApplicantAttorney { get; set; }

    /// <summary>The defense attorney as booked, or null when the appointment has none.</summary>
    public DefenseAttorneyDetailsDto? DefenseAttorney { get; set; }

    /// <summary>
    /// True when the caller is a party to the earlier appointment and could already read it; false
    /// when the confirmation number and date of birth are what let them load it.
    /// </summary>
    public bool CallerIsParty { get; set; }

    /// <summary>
    /// 2026-11-08 (W2) -- the confirmation number the booker typed. <see cref="Source"/> is the newest
    /// re-evaluable case in that case's chain, which may be a later one (a re-evaluation of it, or a
    /// rescheduled replacement); <see cref="FilledFromNewer"/> says when it is.
    /// </summary>
    public string RequestedConfirmationNumber { get; set; } = null!;

    /// <summary>True when <see cref="Source"/> is a later case than the one whose number was typed.</summary>
    public bool FilledFromNewer { get; set; }

    /// <summary>
    /// 2026-11-08 (W2) -- whether a re-evaluation may follow on from <see cref="Source"/> (approved, or a
    /// re-evaluation the patient did not attend). Always true for a caller who is not a party; a party
    /// gets its own case back even when it cannot be, so the form can say why rather than "not found".
    /// The submit checks again.
    /// </summary>
    public bool CanBeReEvaluated { get; set; }
}
