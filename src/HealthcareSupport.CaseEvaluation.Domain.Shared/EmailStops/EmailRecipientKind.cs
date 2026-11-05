namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- who a person is on a case, for the staff "Who gets emails for this case" card. One person
/// can hold several kinds (for example the booker who is also an applicant paralegal and an Authorized
/// User). Integer values travel on the wire; never renumber.
/// </summary>
public enum EmailRecipientKind
{
    Patient = 1,
    ApplicantAttorney = 2,
    ApplicantParalegal = 3,
    DefenseAttorney = 4,
    DefenseParalegal = 5,
    ClaimExaminer = 6,
    Booker = 7,
    AuthorizedUser = 8,
}
