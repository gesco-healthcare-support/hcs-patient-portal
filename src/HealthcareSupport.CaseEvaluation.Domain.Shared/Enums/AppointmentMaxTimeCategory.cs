namespace HealthcareSupport.CaseEvaluation.Enums;

/// <summary>
/// Selects which per-tenant max-time horizon (SystemParameter
/// AppointmentMaxTimePQME / AME / OTHER) applies when validating how far out
/// an appointment of this type may be booked. Replaces the brittle
/// appointment-type-name substring match with a stored, data-driven
/// classification.
/// </summary>
public enum AppointmentMaxTimeCategory
{
    Pqme = 0,
    Ame = 1,
    Other = 2,
}
