namespace HealthcareSupport.CaseEvaluation.Enums;

/// <summary>
/// Classifies an appointment type for the booking-form dropdown filter.
/// Ports OLD's EvaluationTypeEnum -- the value held by
/// spm.AppointmentTypes.ReEvalId -- which decided dropdown visibility per
/// booking context: an initial booking shows Normal + Both; a re-evaluation
/// booking shows Re + Both.
/// </summary>
public enum EvaluationType
{
    Normal = 0,
    Re = 1,
    Both = 2,
}
