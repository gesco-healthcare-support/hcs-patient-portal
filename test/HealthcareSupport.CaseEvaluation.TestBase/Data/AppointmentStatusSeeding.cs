using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// #926 -- <c>Appointment.AppointmentStatus</c> is writable only inside the Domain assembly (and its
/// declared friends). Test assemblies that arrange a status without walking the whole approval flow
/// go through this one helper, so the places a test sidesteps the state machine are greppable.
/// </summary>
public static class AppointmentStatusSeeding
{
    public static void SeedStatus(this Appointment appointment, AppointmentStatusType status)
    {
        appointment.AppointmentStatus = status;
    }
}
