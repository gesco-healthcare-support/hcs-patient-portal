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
}
