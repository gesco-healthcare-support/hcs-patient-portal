namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// W2-10: party role tag attached to <see cref="SendAppointmentEmailArgs"/>
/// when the recipient is resolved by <c>IAppointmentRecipientResolver</c>.
/// Used by future per-recipient template selection (different copy for the
/// patient vs. defense attorney vs. carrier contact, etc.) without changing
/// the resolver. MVP renders one body per <c>NotificationKind</c> regardless
/// of role; the field is forward-compat scaffolding per Adrian's Q4.
/// </summary>
public enum RecipientRole
{
    Patient = 1,
    ApplicantAttorney = 2,
    DefenseAttorney = 3,
    ClaimExaminer = 4,
    InsuranceCarrierContact = 5,
    OfficeAdmin = 6,
    Employer = 7,
}
