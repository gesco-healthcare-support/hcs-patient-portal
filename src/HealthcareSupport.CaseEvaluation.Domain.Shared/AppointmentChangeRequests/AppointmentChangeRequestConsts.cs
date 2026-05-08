namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

public static class AppointmentChangeRequestConsts
{
    /// <summary>
    /// Cap for free-text reason / notes fields. Aligns with
    /// Appointments.AppointmentConsts.ReasonMaxLength so a copy-down from
    /// the change request to the parent appointment cannot truncate.
    /// </summary>
    public const int ReasonMaxLength = 1000;
}
