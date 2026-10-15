using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// The ONE definition of which appointments an external booker is a party to, per role. Both the
/// wizard's patient typeahead (<c>AppointmentsAppService.GetPatientLookupAsync</c> and the booker
/// lookups beside it) and the by-id booking read (<see cref="PatientBookingReadAccess"/>) filter
/// through it, so the patients a caller can find and the patients a caller can open by id cannot
/// drift apart. Two copies of an access rule diverge silently; nothing fails when they do.
///
/// <list type="bullet">
///   <item>Applicant and Defense Attorney: the caller booked the appointment (<c>CreatorId</c>, or
///   <c>BookedByUserId</c> on rows from before the creator stamp), or is the linked attorney on it.</item>
///   <item>Claim Examiner: the appointment names the caller's email as its claim examiner, compared
///   case-insensitively. A Claim Examiner has no link table, so the email column is the only link.</item>
/// </list>
/// </summary>
internal static class BookingPartyAppointments
{
    internal static IQueryable<Appointment> ForApplicantAttorney(
        IQueryable<Appointment> appointments,
        IQueryable<AppointmentApplicantAttorney> links,
        Guid userId)
    {
        return appointments.Where(a => (a.CreatorId ?? a.BookedByUserId) == userId
            || links.Any(l => l.AppointmentId == a.Id && l.IdentityUserId == userId));
    }

    internal static IQueryable<Appointment> ForDefenseAttorney(
        IQueryable<Appointment> appointments,
        IQueryable<AppointmentDefenseAttorney> links,
        Guid userId)
    {
        return appointments.Where(a => (a.CreatorId ?? a.BookedByUserId) == userId
            || links.Any(l => l.AppointmentId == a.Id && l.IdentityUserId == userId));
    }

    /// <summary>Empty for a caller with no email, who cannot be matched to any appointment.</summary>
    internal static IQueryable<Appointment> ForClaimExaminer(IQueryable<Appointment> appointments, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return appointments.Where(a => false);
        }

        var emailLower = email.Trim().ToLower();
        return appointments.Where(a => a.ClaimExaminerEmail != null && a.ClaimExaminerEmail.ToLower() == emailLower);
    }
}
