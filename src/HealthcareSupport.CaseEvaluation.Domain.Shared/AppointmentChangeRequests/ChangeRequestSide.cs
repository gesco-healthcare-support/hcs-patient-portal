namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- which "side" submitted a change request. The opposing
/// side is the one whose consent is solicited via the tokenized Yes/No email.
/// Side A = Patient + Applicant Attorney; Side B = Defense Attorney + Claim
/// Examiner. (Patient and CE always exist in practice, so each side always has a
/// representative.)
/// </summary>
public enum ChangeRequestSide
{
    SideA = 1,
    SideB = 2,
}
