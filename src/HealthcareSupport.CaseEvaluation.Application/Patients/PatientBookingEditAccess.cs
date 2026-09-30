using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// #598 (2026-09-28) -- pure predicate for who may EDIT a patient record through the booking flow
/// (<c>PatientsAppService.UpdatePatientForAppointmentBookingAsync</c>, and the patient edit that
/// <c>AppointmentsAppService.SubmitAsync</c> applies). Allowed: an internal caller (admin / Intake
/// Staff / Staff Supervisor / IT Admin, via <see cref="BookingFlowRoles.IsInternalUserCaller"/>), or
/// the record's own login. Everyone else -- including an attorney or claim examiner who booked for
/// this patient -- may book against the record but not change it.
///
/// <para><b>Why there is no "party to the appointment" pathway.</b> Every party relationship an
/// external caller could hold is one they can create for themselves: booking with an existing
/// patient's id makes the caller that appointment's creator, and the email-and-role rule matches
/// any email the booker types onto the appointment. A rule a caller can grant themselves protects
/// nothing, so this rule accepts only relationships the caller cannot manufacture.</para>
///
/// <para><b>Same body as <see cref="SsnRevealAccess.CanReveal"/>, deliberately not shared.</b> The
/// two rules guard different things (reading a secret versus writing the record) and may diverge;
/// coupling them would make a change to one silently change the other.</para>
///
/// <para>An unclaimed patient (null login) has no owner, so only internal callers may edit it --
/// the owner branch needs both ids present and equal.</para>
/// </summary>
internal static class PatientBookingEditAccess
{
    internal static bool CanEdit(
        IEnumerable<string?>? callerRoles,
        Guid? callerIdentityUserId,
        Guid? patientIdentityUserId)
    {
        if (BookingFlowRoles.IsInternalUserCaller(callerRoles))
        {
            return true;
        }

        return callerIdentityUserId.HasValue
            && patientIdentityUserId.HasValue
            && callerIdentityUserId.Value == patientIdentityUserId.Value;
    }
}
